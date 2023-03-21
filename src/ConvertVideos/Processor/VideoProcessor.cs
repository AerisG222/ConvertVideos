using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NExifTool;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Transforms;
using Xabe.FFmpeg;

namespace ConvertVideos.Processor;

public class VideoProcessor
    : IVideoProcessor
{
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

    readonly ExifTool _exifTool;
    readonly ILogger<VideoProcessor> _log;
    readonly Options _opts;

    string WebVideoDirectoryRoot => $"/movies/{_opts.Year}/{_opts.VideoDirectory.Name}/";
    string WebRawDirectory => $"{WebVideoDirectoryRoot}{DIR_RAW}/";
    string WebFullsizeDirectory => $"{WebVideoDirectoryRoot}{DIR_FULL}/";
    string WebScaledDirectory => $"{WebVideoDirectoryRoot}{DIR_SCALED}/";
    string WebThumbnailDirectory => $"{WebVideoDirectoryRoot}{DIR_THUMBNAILS}/";
    string WebThumbSqDirectory => $"{WebVideoDirectoryRoot}{DIR_THUMB_SQ}/";

    public VideoProcessor(
        ILogger<VideoProcessor> log,
        Options opts,
        ExifTool exifTool
    ) {
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _opts = opts ?? throw new ArgumentNullException(nameof(opts));
        _exifTool = exifTool ?? throw new ArgumentNullException(nameof(exifTool));

        PrepareOutputDirectories();
    }

    public async Task<MovieMetadata> ProcessVideoAsync(string movie)
    {
        _log.LogInformation("Processing: {Video}", Path.GetFileName(movie));

        var mm = await GetMovieMetadataAsync(movie);

        var fullDimension = new ScaledDimensions(FULL_MIN_DIMENSION, mm.RawHeight, mm.RawWidth);
        var scaledDimension = new ScaledDimensions(SCALE_MIN_DIMENSION, mm.RawHeight, mm.RawWidth);

        var dir = Path.GetDirectoryName(movie);
        var file = Path.GetFileName(movie);
        var fileOut = Path.ChangeExtension(file, ".mp4");
        var fileThumb = Path.ChangeExtension(file, ".jpg");

        var localRawFile = Path.Combine(dir, DIR_RAW, file);
        var localFullFile = Path.Combine(dir, DIR_FULL, fileOut);
        var localScaledFile = Path.Combine(dir, DIR_SCALED, fileOut);
        var localThumbnailFile = Path.Combine(dir, DIR_THUMBNAILS, fileThumb);
        var localThumbSqFile = Path.Combine(dir, DIR_THUMB_SQ, fileThumb);

        // move the raw file
        mm.RawUrl = Path.Combine(WebRawDirectory, file);
        File.Move(movie, localRawFile);

        // convert for full size
        mm.FullHeight = fullDimension.ScaledHeight;
        mm.FullWidth = fullDimension.ScaledWidth;
        mm.FullUrl = Path.Combine(WebFullsizeDirectory, fileOut);
        await ConvertVideoAsync(localRawFile, localFullFile, mm.FullWidth, mm.FullHeight);
        mm.FullSize = GetFileSize(localFullFile);

        // convert to scaled size
        mm.ScaledHeight = scaledDimension.ScaledHeight;
        mm.ScaledWidth = scaledDimension.ScaledWidth;
        mm.ScaledUrl = Path.Combine(WebScaledDirectory, fileOut);
        await ConvertVideoAsync(localRawFile, localScaledFile, mm.ScaledWidth, mm.ScaledHeight);
        mm.ScaledSize = GetFileSize(localScaledFile);

        // generate thumbnail
        mm.ThumbUrl = Path.Combine(WebThumbnailDirectory, fileThumb);
        (var height, var width) = await GenerateThumbnailAsync(localRawFile, localThumbnailFile);
        mm.ThumbHeight = height;
        mm.ThumbWidth = width;
        mm.ThumbSize = GetFileSize(localThumbnailFile);

        // generate thumb_sq
        mm.ThumbSqUrl = Path.Combine(WebThumbSqDirectory, fileThumb);
        (height, width) = await GenerateThumbSqAsync(localRawFile, localThumbSqFile);
        mm.ThumbSqHeight = height;
        mm.ThumbSqWidth = width;
        mm.ThumbSqSize = GetFileSize(localThumbSqFile);

        return mm;
    }

    async Task ConvertVideoAsync(string srcFile, string dstFile, int dstWidth, int dstHeight)
    {
        var mi = await FFmpeg.GetMediaInfo(srcFile);

        IStream videoStream = mi.VideoStreams.FirstOrDefault()
            ?.SetCodec(VideoCodec.h264)
            ?.SetSize(dstWidth, dstHeight);

        IStream audioStream = mi.AudioStreams.FirstOrDefault()
            ?.SetCodec(AudioCodec.aac);

        await FFmpeg.Conversions
            .New()
            .AddStream(videoStream, audioStream)
            .SetOutput(dstFile)
            .Start();
    }

    async Task<MovieMetadata> GetMovieMetadataAsync(string file)
    {
        var mediaInfo = await FFmpeg.GetMediaInfo(file);
        var video = mediaInfo.VideoStreams.First();

        var mm = new MovieMetadata()
        {
            RawHeight = video.Height,
            RawWidth = video.Width,
            RawSize = mediaInfo.Size,
            Rotation = video.Rotation ?? 0,
            VideoCreationTime = mediaInfo.CreationTime,
            VideoDuration = Convert.ToSingle(video.Duration.TotalSeconds),
        };

        PopulateExifData(file, mm);

        return mm;
    }

    Task<(int height, int width)> GenerateThumbnailAsync(string srcFile, string dstFile)
    {
        var opts = new ResizeOptions()
        {
            Mode = ResizeMode.Max,
            Position = AnchorPositionMode.Center,
            Size = new Size(THUMB_WIDTH, THUMB_HEIGHT),
            Sampler = LanczosResampler.Lanczos3
        };

        return GenerateThumbnailAsync(srcFile, dstFile, opts);
    }

    Task<(int height, int width)> GenerateThumbSqAsync(string srcFile, string dstFile)
    {
        var opts = new ResizeOptions()
        {
            Mode = ResizeMode.Crop,
            Position = AnchorPositionMode.Center,
            Size = new Size(THUMB_SQ_WIDTH, THUMB_SQ_HEIGHT),
            Sampler = LanczosResampler.Lanczos3
        };

        return GenerateThumbnailAsync(srcFile, dstFile, opts);
    }

    async Task<(int height, int width)> GenerateThumbnailAsync(string srcFile, string dstFile, ResizeOptions resizeOptions)
    {
        var mi = await FFmpeg.GetMediaInfo(srcFile);
        var video = mi.VideoStreams.First();

        var frameToExtract = video.Duration > TimeSpan.FromSeconds(2) ? 2 * video.Framerate : 0;

        await FFmpeg.Conversions
            .New()
            .AddStream(video)
            .ExtractNthFrame(Convert.ToInt32(frameToExtract), s => dstFile)
            .Start();

        using var image = Image.Load(dstFile);

        image.Mutate(ctx => ctx.Resize(resizeOptions));
        image.SaveAsJpeg(dstFile);

        return (image.Height, image.Width);
    }

    void PopulateExifData(string localSourceFile, MovieMetadata mm)
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

        var exifRotation = tags.SingleOrDefaultPrimaryTag("Rotation")?.TryGetInt32() ?? 0;

        FixupRotationHack(mm, exifRotation);
    }

    // there seems to be an issue when dealing with rotation from some devices - ffprobe does not
    // report the rotation, but exif tools can see it.  in this scenario, the width needs to be
    // swapped with the height so all the resizing and downstream changes are properly applied.
    void FixupRotationHack(MovieMetadata mm, int exifRotation)
    {
        if (mm.Rotation == 0 && exifRotation != 0)
        {
            if (Math.Abs(exifRotation) == 90 || Math.Abs(exifRotation) == 270)
            {
                var tmp = mm.RawWidth;

                mm.RawWidth = mm.RawHeight;
                mm.RawHeight = tmp;
            }
        }
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
}
