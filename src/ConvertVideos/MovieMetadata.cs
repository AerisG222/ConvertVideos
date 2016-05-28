using System;

namespace ConvertVideos
{
	public class MovieMetadata
	{
		public string VideoCodecName { get; set; }
		public int VideoWidth { get; set; }
		public int VideoHeight { get; set; }
		public string VideoFrameRate { get; set; }
		public float VideoDuration { get; set; }
		public int VideoNumberOfFrames { get; set; }
		public DateTime? VideoCreationTime { get; set; }
		public string AudioCodecName { get; set; }
		public double AudioSampleRate { get; set; }
		public int AudioChannelCount { get; set; }

		public string OrigUrl { get; set; }
		public string RawUrl { get; set; }
		public string FullUrl { get; set; }
		public string ScaledUrl { get; set; }
		public string ThumbUrl { get; set; }

		public int FullHeight { get; set; }
		public int FullWidth { get; set; }

		public int ScaledHeight { get; set; }
		public int ScaledWidth { get; set; }
		
		public int ThumbHeight { get; set; }
		public int ThumbWidth { get; set; }
		
		public MovieMetadata ()
		{
			// do nothing
		}
	}
}

