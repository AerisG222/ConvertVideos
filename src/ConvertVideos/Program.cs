using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NExifTool;
using NMagickWand;


namespace ConvertVideos
{
	public class Program
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
		const float THUMB_SQ_ASPECT = THUMB_SQ_WIDTH / THUMB_SQ_HEIGHT;
		const string DEST_EXTENSION = "mp4";
		static readonly string[] SOURCE_EXTENSIONS = new string[] { ".flv", ".vob", ".mpg", ".mpeg", ".avi", ".3gp", ".m4v", ".mp4", ".mov" };
		static readonly ExifTool _exifTool = new ExifTool(new ExifToolOptions());

		Options _opts;
		object _lockObj = new object();


		bool HasSetTeaserVideo { get; set; }
		StreamWriter Writer { get; set; }


		string WebVideoDirectoryRoot
		{
			get
			{
				string[] dirComponents = Path.GetDirectoryName(_opts.VideoDirectory).Split('/');
				string dir = dirComponents[dirComponents.Length - 1];

				return $"/movies/{_opts.Year}/{dir}/";
			}
		}


		string WebRawDirectory
		{
			get
			{
				return $"{WebVideoDirectoryRoot}{DIR_RAW}/";
			}
		}


		string WebFullsizeDirectory
		{
			get
			{
				return $"{WebVideoDirectoryRoot}{DIR_FULL}/";
			}
		}


		string WebScaledDirectory
		{
			get
			{
				return $"{WebVideoDirectoryRoot}{DIR_SCALED}/";
			}
		}


		string WebThumbnailDirectory
		{
			get
			{
				return $"{WebVideoDirectoryRoot}{DIR_THUMBNAILS}/";
			}
		}


		string WebThumbSqDirectory
		{
			get
			{
				return $"{WebVideoDirectoryRoot}{DIR_THUMB_SQ}/";
			}
		}


		public Program(Options opts)
		{
			_opts = opts;
		}


		public static void Main (string[] args)
		{
			var opts = new Options();
            opts.Parse(args);

			var app = new Program(opts);

			app.Execute();
		}


		void Execute()
		{
			if(!Directory.Exists(_opts.VideoDirectory))
			{
				throw new DirectoryNotFoundException($"The video directory specified, {_opts.VideoDirectory}, does not exist.  Please specify a directory containing images.");
			}

			if(File.Exists(_opts.OutputFile))
			{
				throw new IOException($"The specified output file, {_opts.OutputFile}, already exists.  Please remove it before running this process.");
			}

			Ffmpeg.FfmpegPath = FFMPEG_PATH;
			Ffmpeg.FfprobePath = FFPROBE_PATH;

			PrepareOutputDirectories();

			IList<string> filesToSize = GetMovieFileList();

			using(var fs = new FileStream(_opts.OutputFile, FileMode.CreateNew))
			using(Writer = new StreamWriter(fs))
			{
				Writer.WriteLine($"INSERT INTO video.category (name, year, is_private) VALUES ({SqlString(_opts.CategoryName)}, {_opts.Year}, {_opts.IsPrivate.ToString().ToUpper()});");
				Writer.WriteLine();

				Parallel.ForEach(filesToSize, ProcessMovie);

				Writer.WriteLine();
				WriteCategoryUpdateTotals();
			}
		}


		void ProcessMovie(string movie)
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

			lock(_lockObj)
			{
				Writer.WriteLine(
					"INSERT INTO video.video (category_id, " +
					    $"thumb_height, thumb_width, thumb_path, thumb_size, " +
						$"thumb_sq_height, thumb_sq_width, thumb_sq_path, thumb_sq_size, " +
					    $"full_height, full_width, full_path, full_size, " +
						$"scaled_height, scaled_width, scaled_path, scaled_size, " +
				    	$"raw_height, raw_width, raw_path, raw_size, " +
						$"is_private, duration, create_date, " +
						$"gps_latitude, gps_latitude_ref_id, gps_longitude, gps_longitude_ref_id) VALUES (" +
				        $"(SELECT currval('video.category_id_seq')), " +
						$"{mm.ThumbHeight}, " +
						$"{mm.ThumbWidth}, " +
						$"{SqlString(mm.ThumbUrl)}, " +
						$"{mm.ThumbSize}, " +
						$"{mm.ThumbSqHeight}, " +
						$"{mm.ThumbSqWidth}, " +
						$"{SqlString(mm.ThumbSqUrl)}, " +
						$"{mm.ThumbSqSize}, " +
						$"{mm.FullHeight}, " +
						$"{mm.FullWidth}, " +
						$"{SqlString(mm.FullUrl)}, " +
						$"{mm.FullSize}, " +
						$"{mm.ScaledHeight}, " +
						$"{mm.ScaledWidth}, " +
						$"{SqlString(mm.ScaledUrl)}, " +
						$"{mm.ScaledSize}, " +
						$"{mm.RawHeight}, " +
						$"{mm.RawWidth}, " +
						$"{SqlString(mm.RawUrl)}, " +
						$"{mm.RawSize}, " +
						$"{_opts.IsPrivate.ToString().ToUpper()}, " +
						$"{SqlNumber(mm.VideoDuration)}, " +
						$"{SqlTimestamp(mm.VideoCreationTime)}, " +
						$"{SqlNumber(mm.Latitude)}, " +
						$"{SqlString(mm.LatitudeRef)}, " +
						$"{SqlNumber(mm.Longitude)}, " +
						$"{SqlString(mm.LongitudeRef)} " +
						$");");

				if(!HasSetTeaserVideo)
				{
					Writer.WriteLine();
					Writer.WriteLine(
						$"UPDATE video.category " +
						$"   SET teaser_image_path = {SqlString(mm.ThumbUrl)}, " +
						$"       teaser_image_height = {mm.ThumbHeight}, " +
						$"       teaser_image_width = {mm.ThumbWidth}, " +
						$"       teaser_image_size = {mm.ThumbSize}, " +
						$"       teaser_image_sq_path = {SqlString(mm.ThumbSqUrl)}, " +
						$"       teaser_image_sq_height = {mm.ThumbSqHeight}, " +
						$"       teaser_image_sq_width = {mm.ThumbSqWidth}, " +
						$"       teaser_image_sq_size = {mm.ThumbSqSize} " +
						$" WHERE id = (SELECT currval('video.category_id_seq'));");
					Writer.WriteLine();

					HasSetTeaserVideo = true;
				}
			}
		}


		void WriteCategoryUpdateTotals()
		{
			Writer.WriteLine(
				"UPDATE video.category c " +
				"   SET video_count = (SELECT COUNT(1) FROM video.video WHERE category_id = c.id), " +
				"       create_date = (SELECT create_date FROM video.video WHERE id = (SELECT MIN(id) FROM video.video where category_id = c.id AND create_date IS NOT NULL)), " +
				"       gps_latitude = (SELECT gps_latitude FROM video.video WHERE id = (SELECT MIN(id) FROM video.video WHERE category_id = c.id AND gps_latitude IS NOT NULL)), " +
				"       gps_latitude_ref_id = (SELECT gps_latitude_ref_id FROM video.video WHERE id = (SELECT MIN(id) FROM video.video WHERE category_id = c.id AND gps_latitude IS NOT NULL)), " +
				"       gps_longitude = (SELECT gps_longitude FROM video.video WHERE id = (SELECT MIN(id) FROM video.video WHERE category_id = c.id AND gps_latitude IS NOT NULL)), " +
				"       gps_longitude_ref_id = (SELECT gps_longitude_ref_id FROM video.video WHERE id = (SELECT MIN(id) FROM video.video WHERE category_id = c.id AND gps_latitude IS NOT NULL)), " +
				"       total_duration = (SELECT SUM(duration) FROM video.video WHERE category_id = c.id), " +
				"       total_size_thumb = (SELECT SUM(thumb_size) FROM video.video WHERE category_id = c.id), " +
				"       total_size_thumb_sq = (SELECT SUM(thumb_sq_size) FROM video.video WHERE category_id = c.id), " +
				"       total_size_scaled = (SELECT SUM(scaled_size) FROM video.video WHERE category_id = c.id), " +
				"       total_size_full = (SELECT SUM(full_size) FROM video.video WHERE category_id = c.id), " +
				"       total_size_raw = (SELECT SUM(raw_size) FROM video.video WHERE category_id = c.id) " +
				" WHERE id = (SELECT currval('video.category_id_seq'));"
			);
		}


		void GenerateThumbnail(Ffmpeg ffmpeg, string localSourceFile, string localThumbnailFile, MovieMetadata mm)
		{
			ffmpeg.ExtractFrame(localSourceFile, localThumbnailFile);

			using(var wand = new MagickWand(localThumbnailFile))
            {
				wand.GetLargestDimensionsKeepingAspectRatio(THUMB_WIDTH, THUMB_HEIGHT, out uint width, out uint height);
                wand.ScaleImage(width, height);

                // sharpen after potentially resizing
                // http://www.imagemagick.org/Usage/resize/#resize_unsharp
                wand.UnsharpMaskImage(0, 0.7, 0.7, 0.008);

                wand.WriteImage(localThumbnailFile, true);

				mm.ThumbHeight = (int) height;
				mm.ThumbWidth = (int) width;
            }
		}


		void GenerateThumbSq(Ffmpeg ffmpeg, string localSourceFile, string localThumbSqFile, MovieMetadata mm)
		{
			ffmpeg.ExtractFrame(localSourceFile, localThumbSqFile);

			using(var wand = new MagickWand(localThumbSqFile))
            {
                var width = (double)wand.ImageWidth;
                var height = (double)wand.ImageHeight;
                var aspect = width / height;

                if(aspect >= THUMB_SQ_ASPECT)
                {
                    var newWidth = (width / height) * THUMB_SQ_HEIGHT;

                    // scale image to final height
                    wand.ScaleImage((uint) newWidth, THUMB_SQ_HEIGHT);

                    // crop sides as needed
                    wand.CropImage(THUMB_SQ_WIDTH, THUMB_SQ_HEIGHT, (int) (newWidth - THUMB_SQ_WIDTH) / 2, 0);
                }
                else
                {
                    var newHeight = THUMB_SQ_WIDTH / (width / height);

                    // scale image to final width
                    wand.ScaleImage(THUMB_SQ_WIDTH, (uint) newHeight);

                    // crop top and bottom as needed
                    wand.CropImage(THUMB_SQ_WIDTH, THUMB_SQ_HEIGHT, 0, (int) (newHeight - THUMB_SQ_HEIGHT) / 2);
                }

                // sharpen after potentially resizing
                // http://www.imagemagick.org/Usage/resize/#resize_unsharp
                wand.UnsharpMaskImage(0, 0.7, 0.7, 0.008);

                wand.WriteImage(localThumbSqFile, true);

				mm.ThumbSqHeight = THUMB_SQ_HEIGHT;
				mm.ThumbSqWidth = THUMB_SQ_WIDTH;
            }
		}


		void PopulateVideoMetadata(string localSourceFile, MovieMetadata mm)
        {
			var tags = _exifTool.GetTagsAsync(localSourceFile).Result;

			if(mm.VideoCreationTime == null)
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
			Directory.CreateDirectory(Path.Combine(_opts.VideoDirectory, DIR_RAW));
			Directory.CreateDirectory(Path.Combine(_opts.VideoDirectory, DIR_FULL));
			Directory.CreateDirectory(Path.Combine(_opts.VideoDirectory, DIR_SCALED));
			Directory.CreateDirectory(Path.Combine(_opts.VideoDirectory, DIR_THUMBNAILS));
			Directory.CreateDirectory(Path.Combine(_opts.VideoDirectory, DIR_THUMB_SQ));
		}


		long GetFileSize(string file)
		{
			FileInfo fi = new FileInfo(file);

			return fi.Length;
		}

		IList<string> GetMovieFileList()
		{
			var list = new List<string>();

			string[] files = Directory.GetFiles(_opts.VideoDirectory);

			list.AddRange(files.Where(f => SOURCE_EXTENSIONS.Contains(Path.GetExtension(f).ToLower())));

			return list;
		}


		static string SqlNumber(object num)
        {
            if(num == null)
            {
                return "NULL";
            }

            return num.ToString();
        }


		static string SqlString(string val)
		{
			if(val == null)
			{
				return "NULL";
			}
			else
			{
				return $"'{val.Replace("'", "''")}'";
			}
		}


		string SqlTimestamp(DateTime? dt)
        {
            if(dt == null)
            {
                return "NULL";
            }

            return SqlString(((DateTime)dt).ToString("yyyy-MM-dd HH:mm:sszzz"));
        }
	}
}
