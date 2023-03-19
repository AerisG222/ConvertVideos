using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConvertVideos.ResultWriter;
using Microsoft.Extensions.Hosting;
using NExifTool;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Transforms;

namespace ConvertVideos;

public class Worker
    : BackgroundService
{
    const string FFMPEG_PATH = "/usr/bin/ffmpeg";
    const string FFPROBE_PATH = "/usr/bin/ffprobe";
    const string DIR_RAW = "raw";
    const string DIR_FULL = "full";
    const string DIR_SCALED = "scaled";
    const string DIR_THUMBNAILS = "thumbnails";
    const string DIR_THUMB_SQ = "thumb_sq";
    const int FULL_MIN_DIMENSION = 480;
    const int SCALE_MIN_DIMENSION = 240;
    const int THUMB_WIDTH = 240;
    const int THUMB_HEIGHT = 160;
    const int THUMB_SQ_WIDTH = 160;
    const int THUMB_SQ_HEIGHT = 120;
    static readonly string[] SOURCE_EXTENSIONS = new string[] { ".flv", ".vob", ".mpg", ".mpeg", ".avi", ".3gp", ".m4v", ".mp4", ".mov" };
    static readonly ExifTool _exifTool = new ExifTool(new ExifToolOptions());

    readonly Options _opts;
    readonly IResultWriter _writer;
    readonly IHostApplicationLifetime _appLifetime;

    string WebVideoDirectoryRoot => $"/movies/{_opts.Year}/{_opts.VideoDirectory.Name}/";
    string WebRawDirectory => $"{WebVideoDirectoryRoot}{DIR_RAW}/";
    string WebFullsizeDirectory => $"{WebVideoDirectoryRoot}{DIR_FULL}/";
    string WebScaledDirectory => $"{WebVideoDirectoryRoot}{DIR_SCALED}/";
    string WebThumbnailDirectory => $"{WebVideoDirectoryRoot}{DIR_THUMBNAILS}/";
    string WebThumbSqDirectory => $"{WebVideoDirectoryRoot}{DIR_THUMB_SQ}/";

    public Worker(IHostApplicationLifetime appLifetime, Options opts, IResultWriter writer)
    {
        _appLifetime = appLifetime ?? throw new ArgumentNullException(nameof(appLifetime));
        _opts = opts ?? throw new ArgumentNullException(nameof(opts));
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Ffmpeg.FfmpegPath = FFMPEG_PATH;
        Ffmpeg.FfprobePath = FFPROBE_PATH;

        PrepareOutputDirectories();

        var files = GetMovieFiles();

        var opts = new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Max(Environment.ProcessorCount - 1, 1)
        };

        var results = new MovieMetadata[files.Count];

        Parallel.ForEach(files, opts, (file, state, index) => {
            results[index] = ProcessMovie(file);
        });

        _writer.WriteOutput(_opts.OutputFile.FullName, _opts.CategoryInfo, results);

        _appLifetime.StopApplication();

        return Task.CompletedTask;
    }

    MovieMetadata ProcessMovie(string movie)
    {
        Console.WriteLine($"Processing: {Path.GetFileName(movie)}");

        Ffmpeg ffmpeg = new FfmpegH264();

        MovieMetadata mm = ffmpeg.GatherMetadata(movie);
        var fullDimension = new ScaledDimensions(FULL_MIN_DIMENSION, mm.RawHeight, mm.RawWidth);
        var scaledDimension = new ScaledDimensions(SCALE_MIN_DIMENSION, mm.RawHeight, mm.RawWidth);

        var dir = Path.GetDirectoryName(movie);
        var file = Path.GetFileName(movie);
        var fileOut = $"{Path.GetFileNameWithoutExtension(movie)}{ffmpeg.OutputFileExtension}";
        var fileThumb = $"{Path.GetFileNameWithoutExtension(movie)}.jpg";

        var localRawFile = Path.Combine(dir, DIR_RAW, file);
        var localFullFile = Path.Combine(dir, DIR_FULL, fileOut);
        var localScaledFile = Path.Combine(dir, DIR_SCALED, fileOut);
        var localThumbnailFile = Path.Combine(dir, DIR_THUMBNAILS, fileThumb);
        var localThumbSqFile = Path.Combine(dir, DIR_THUMB_SQ, fileThumb);

        // move the raw file
        mm.RawUrl = Path.Combine(WebRawDirectory, file);
        File.Move(movie, localRawFile);
        mm.RawSize = GetFileSize(localRawFile);

        // convert for full size
        mm.FullHeight = fullDimension.ScaledHeight;
        mm.FullWidth = fullDimension.ScaledWidth;
        mm.FullUrl = Path.Combine(WebFullsizeDirectory, fileOut);
        ffmpeg.Convert(localRawFile, localFullFile, mm.FullWidth, mm.FullHeight);
        mm.FullSize = GetFileSize(localFullFile);

        // some sources seem to report bad durations - but the webm conversions seem clean, so use those to get the duration!
        MovieMetadata m2 = ffmpeg.GatherMetadata(localFullFile);
        mm.VideoDuration = m2.VideoDuration;

        // convert to scaled size
        mm.ScaledHeight = scaledDimension.ScaledHeight;
        mm.ScaledWidth = scaledDimension.ScaledWidth;
        mm.ScaledUrl = Path.Combine(WebScaledDirectory, fileOut);
        ffmpeg.Convert(localRawFile, localScaledFile, mm.ScaledWidth, mm.ScaledHeight);
        mm.ScaledSize = GetFileSize(localScaledFile);

        // generate thumbnail
        mm.ThumbUrl = Path.Combine(WebThumbnailDirectory, fileThumb);
        GenerateThumbnail(ffmpeg, localRawFile, localThumbnailFile, mm);
        mm.ThumbSize = GetFileSize(localThumbnailFile);

        // generate thumb_sq
        mm.ThumbSqUrl = Path.Combine(WebThumbSqDirectory, fileThumb);
        GenerateThumbSq(ffmpeg, localRawFile, localThumbSqFile, mm);
        mm.ThumbSqSize = GetFileSize(localThumbSqFile);

        PopulateVideoMetadata(localRawFile, mm);

        return mm;
    }

    int GetThumbnailSeconds(MovieMetadata mm)
    {
        var frameAtSecond = 2;

        if (mm.VideoDuration < 2)
        {
            frameAtSecond = 0;
        }

        return frameAtSecond;
    }

    void GenerateThumbnail(Ffmpeg ffmpeg, string localSourceFile, string localThumbnailFile, MovieMetadata mm)
    {
        ffmpeg.ExtractFrame(localSourceFile, localThumbnailFile, GetThumbnailSeconds(mm));

        using var image = Image.Load(localThumbnailFile);

        image.Mutate(ctx => ctx
            .Resize(new ResizeOptions()
            {
                Mode = ResizeMode.Max,
                Position = AnchorPositionMode.Center,
                Size = new Size(THUMB_WIDTH, THUMB_HEIGHT),
                Sampler = LanczosResampler.Lanczos3
            })
        );

        image.SaveAsJpeg(localThumbnailFile);

        mm.ThumbHeight = image.Height;
        mm.ThumbWidth = image.Width;
    }

    void GenerateThumbSq(Ffmpeg ffmpeg, string localSourceFile, string localThumbSqFile, MovieMetadata mm)
    {
        ffmpeg.ExtractFrame(localSourceFile, localThumbSqFile, GetThumbnailSeconds(mm));

        using var image = Image.Load(localThumbSqFile);

        image.Mutate(ctx => ctx
            .Resize(new ResizeOptions()
            {
                Mode = ResizeMode.Crop,
                Position = AnchorPositionMode.Center,
                Size = new Size(THUMB_SQ_WIDTH, THUMB_SQ_HEIGHT),
                Sampler = LanczosResampler.Lanczos3
            })
        );

        image.SaveAsJpeg(localThumbSqFile);

        mm.ThumbSqHeight = image.Height;
        mm.ThumbSqWidth = image.Width;
    }

    void PopulateVideoMetadata(string localSourceFile, MovieMetadata mm)
    {
        var tags = _exifTool.GetTagsAsync(localSourceFile).Result;

        if (mm.VideoCreationTime == null)
        {
            mm.VideoCreationTime = tags.SingleOrDefaultPrimaryTag("CreateDate")?.TryGetDateTime();
        }

        mm.Latitude = tags.SingleOrDefaultPrimaryTag("GPSLatitude")?.TryGetDouble();
        mm.LatitudeRef = tags.SingleOrDefaultPrimaryTag("GPSLatitudeRef")?.Value?.Substring(0, 1);
        mm.Longitude = tags.SingleOrDefaultPrimaryTag("GPSLongitude")?.TryGetDouble();
        mm.LongitudeRef = tags.SingleOrDefaultPrimaryTag("GPSLongitudeRef")?.Value?.Substring(0, 1);
    }

    void PrepareOutputDirectories()
    {
        Directory.CreateDirectory(Path.Combine(_opts.VideoDirectory.FullName, DIR_RAW));
        Directory.CreateDirectory(Path.Combine(_opts.VideoDirectory.FullName, DIR_FULL));
        Directory.CreateDirectory(Path.Combine(_opts.VideoDirectory.FullName, DIR_SCALED));
        Directory.CreateDirectory(Path.Combine(_opts.VideoDirectory.FullName, DIR_THUMBNAILS));
        Directory.CreateDirectory(Path.Combine(_opts.VideoDirectory.FullName, DIR_THUMB_SQ));
    }

    long GetFileSize(string file)
    {
        FileInfo fi = new FileInfo(file);

        return fi.Length;
    }

    IList<string> GetMovieFiles()
    {
        return _opts
            .VideoDirectory
            .EnumerateFiles()
            .Where(f => SOURCE_EXTENSIONS.Contains(f.Extension.ToLower()))
            .Select(f => f.FullName)
            .ToList();
    }
}
