using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Mono.Options;


namespace ConvertVideos
{
	public class Program
	{
		private const string FFMPEG_PATH = "/usr/bin/ffmpeg";
		private const string FFPROBE_PATH = "/usr/bin/ffprobe";
		private const string DIR_RAW = "raw";
		private const string DIR_FULL = "full";
		private const string DIR_SCALED = "scaled";
		private const string DIR_THUMBNAILS = "thumbnails";
		private const int FULL_MIN_DIMENSION = 480;
		private const int SCALE_MIN_DIMENSION = 240;
		private const int THUMB_WIDTH = 240;
		private const int THUMB_HEIGHT = 160;
		private static readonly string[] SOURCE_EXTENSIONS = new string[] { ".flv", ".vob", ".mpg", ".mpeg", ".avi", ".3gp", ".m4v", ".mp4", ".mov" };
		private const string DEST_EXTENSION = "mp4";
		
		private string CategoryName { get; set; }
		private string OutputFile { get; set; }
		private string VideoDirectory { get; set; }
		private string WebDirectory { get; set; }
		private bool IsPrivate { get; set; }
		private int Year { get; set; }
		
		private object _lockObj = new object();
		
		
		private bool HasSetTeaserVideo { get; set; }
		private StreamWriter Writer { get; set; }
		
		
		private string WebVideoDirectoryRoot
		{
			get
			{
				string[] dirComponents = Path.GetDirectoryName(VideoDirectory).Split('/');
				string dir = dirComponents[dirComponents.Length - 1];
				
				return string.Concat("/movies/", Year, "/", dir, "/");
			}
		}
		
		
		private string WebRawDirectory
		{
			get
			{
				return string.Concat(WebVideoDirectoryRoot, DIR_RAW, "/");
			}
		}


		private string WebFullsizeDirectory
		{
			get
			{
				return string.Concat(WebVideoDirectoryRoot, DIR_FULL, "/");
			}
		}

		
		private string WebScaledDirectory
		{
			get
			{
				return string.Concat(WebVideoDirectoryRoot, DIR_SCALED, "/");
			}
		}
		
		
		private string WebThumbnailDirectory
		{
			get
			{
				return string.Concat(WebVideoDirectoryRoot, DIR_THUMBNAILS, "/");
			}
		}
		
		
		public static void Main (string[] args)
		{
			var app = new Program();
			
			int year = 0;
			
			OptionSet options = new OptionSet();
			options.Add("c|catname=", "The name for the category to represent the videos in the directory.", delegate (string v) { app.CategoryName = v; });
			options.Add("h|help", "This help screen.", delegate(string v) { ShowUsage(options); Environment.Exit(0); });
			options.Add("o|outfile=", "The path to the SQL file to generate.", delegate (string v) { app.OutputFile = v; });
			options.Add("v|viddir=", "The directory containing the source videos to resize: (/home/mmorano/Desktop/mypix/).", delegate (string v) { app.VideoDirectory = v; });
			options.Add("w|webdir=", "The full URL path to the image directory: (/images/2009/mypix/).", delegate (string v) { app.WebDirectory = v; });
			options.Add("x|private=", "Mark the category as private so only the admin can view these pictures.", delegate (string v) { app.IsPrivate = string.Equals(v, "y", StringComparison.InvariantCultureIgnoreCase); });
			options.Add("y|year=", "The year the pictures were taken.", delegate (string v) { if(int.TryParse(v, out year)) { app.Year = year; } });
			
			options.Parse(args);
			
			if(string.IsNullOrEmpty(app.OutputFile) ||
			   string.IsNullOrEmpty(app.VideoDirectory) ||
			   string.IsNullOrEmpty(app.WebDirectory) ||
			   string.IsNullOrEmpty(app.CategoryName) ||
			   app.Year == 0)
			{
				ShowUsage(options);
				Environment.Exit(1);
			}

			app.Execute();
		}
		
		
		private void Execute()
		{
			if(!Directory.Exists(VideoDirectory))
			{
				throw new DirectoryNotFoundException(string.Concat("The video directory specified, ", VideoDirectory, ", does not exist.  Please specify a directory containing images."));
			}
			
			if(File.Exists(OutputFile))
			{
				throw new IOException(string.Concat("The specified output file, ", OutputFile, ", already exists.  Please remove it before running this process."));
			}
			
			Ffmpeg.FfmpegPath = FFMPEG_PATH;
			Ffmpeg.FfprobePath = FFPROBE_PATH;
			
			PrepareOutputDirectories();
			
			IList<string> filesToSize = GetMovieFileList();
		
			try
			{
				Writer = new StreamWriter(OutputFile);

				Writer.WriteLine(string.Concat("INSERT INTO video_category (name, year, is_private) VALUES (", SqlString(CategoryName), ", ", Year, ", ", IsPrivate.ToString().ToUpper(), ");"));
				Writer.WriteLine();
				Writer.WriteLine(@"SELECT @CATEGORY_ID := LAST_INSERT_ID();");
				Writer.WriteLine();

				Parallel.ForEach(filesToSize, ProcessMovie);
			}
			finally
			{
				if(Writer != null)
				{
					Writer.Close();
				}
			}
		}
		
		
		private void ProcessMovie(string movie)
		{
			Console.WriteLine("Processing: " + Path.GetFileName(movie));
			
			Ffmpeg ffmpeg = new FfmpegH264();  // switched from webm to h264 7/7/2013
			
			MovieMetadata mm = ffmpeg.GatherMetadata(movie);
			var fullDimension = new ScaledDimensions(FULL_MIN_DIMENSION, mm.VideoHeight, mm.VideoWidth);
			var scaledDimension = new ScaledDimensions(SCALE_MIN_DIMENSION, mm.VideoHeight, mm.VideoWidth);
			 
			string dir = Path.GetDirectoryName(movie);
			string file = Path.GetFileName(movie);
			string fileOut = string.Concat(Path.GetFileNameWithoutExtension(movie), ffmpeg.OutputFileExtension);
			string fileThumb = string.Concat(Path.GetFileNameWithoutExtension(movie), ".png");
			string rawDir = Path.Combine(dir, DIR_RAW);
			string fullDir = Path.Combine(dir, DIR_FULL);
			string scaledDir = Path.Combine(dir, DIR_SCALED);
			string thumbDir = Path.Combine(dir, DIR_THUMBNAILS);

			// move the raw file
			mm.RawUrl = Path.Combine(WebRawDirectory, file);
			File.Move(movie, Path.Combine(rawDir, file));

			// convert for full size
			mm.FullHeight = fullDimension.ScaledHeight;
			mm.FullWidth = fullDimension.ScaledWidth;
			mm.FullUrl = Path.Combine(WebFullsizeDirectory, fileOut);
			ffmpeg.Convert(Path.Combine(rawDir, file), Path.Combine(fullDir, fileOut), mm.FullWidth, mm.FullHeight);

			// some sources seem to report bad durations - but the webm conversions seem clean, so use those to get the duration!
			MovieMetadata m2 = ffmpeg.GatherMetadata(Path.Combine(fullDir, fileOut));
			mm.VideoDuration = m2.VideoDuration;

			// convert to scaled size
			mm.ScaledHeight = scaledDimension.ScaledHeight;
			mm.ScaledWidth = scaledDimension.ScaledWidth;
			mm.ScaledUrl = Path.Combine(WebScaledDirectory, fileOut);
			ffmpeg.Convert(Path.Combine(rawDir, file), Path.Combine(scaledDir, fileOut), mm.ScaledWidth, mm.ScaledHeight);

			// generate thumb
			var thumbWidth = THUMB_WIDTH;
			var thumbHeight = THUMB_HEIGHT;
			CalculateThumbSize(mm, ref thumbHeight, ref thumbWidth);

			mm.ThumbHeight = thumbHeight;
			mm.ThumbWidth = thumbWidth;
			mm.ThumbUrl = Path.Combine(WebThumbnailDirectory, fileThumb);
			ffmpeg.CreateThumbnail(Path.Combine(rawDir, file), Path.Combine(thumbDir, fileThumb), mm.ThumbWidth, mm.ThumbHeight);
			
			lock(_lockObj)
			{
				Writer.WriteLine("INSERT INTO video (video_category_id, thumb_height, thumb_width, full_height, full_width, scaled_height, scaled_width,"
				                                  + " raw_path, thumb_path, full_path, scaled_path, is_private, duration)"
				                         + " VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}, {10}, {11}, {12});",
			        "@CATEGORY_ID",
					mm.ThumbHeight,
					mm.ThumbWidth,
				    mm.FullHeight,
				    mm.FullWidth,
					mm.ScaledHeight,
					mm.ScaledWidth,
					SqlString(mm.RawUrl),
					SqlString(mm.ThumbUrl),
				    SqlString(mm.FullUrl),
					SqlString(mm.ScaledUrl),
				    IsPrivate.ToString().ToUpper(),
					mm.VideoDuration);
				
				if(!HasSetTeaserVideo)
				{
					Writer.WriteLine();
					Writer.WriteLine("UPDATE video_category SET teaser_image_path = {0}, teaser_image_height = {1}, teaser_image_width = {2} WHERE id = @CATEGORY_ID;",
					                 SqlString(mm.ThumbUrl), 
					                 mm.ThumbHeight, 
					                 mm.ThumbWidth);
					Writer.WriteLine();
					
					HasSetTeaserVideo = true;
				}
			}
		}


		private void CalculateThumbSize(MovieMetadata mm, ref int height, ref int width)
		{
			float idealAspect = (float)width / (float)height;
			float actualAspect = (float)mm.VideoWidth / (float)mm.VideoHeight;

			if(idealAspect >= actualAspect)
			{
				width = (int)(actualAspect * (float)height);
			}
			else
			{
				height = (int)((float)width / actualAspect);
			}
		}


		private void PrepareOutputDirectories()
		{
			Directory.CreateDirectory(Path.Combine(VideoDirectory, DIR_RAW));
			Directory.CreateDirectory(Path.Combine(VideoDirectory, DIR_FULL));
			Directory.CreateDirectory(Path.Combine(VideoDirectory, DIR_SCALED));
			Directory.CreateDirectory(Path.Combine(VideoDirectory, DIR_THUMBNAILS));
		}
		
		
		private IList<string> GetMovieFileList()
		{
			var list = new List<string>();
			
			string[] files = Directory.GetFiles(VideoDirectory);
			
			list.AddRange(files.Where(f => SOURCE_EXTENSIONS.Contains(Path.GetExtension(f).ToLower())));
			
			return list;
		}
		
		
		private static string SqlString(string val)
		{
			if(val == null)
			{
				return "NULL";
			}
			else
			{
				return string.Concat("'", val.Replace("'", "''"), "'");
			}
		}
		
		
		private static void ShowUsage(OptionSet options)
		{
			options.WriteOptionDescriptions(Console.Out);
		}
	}
}
