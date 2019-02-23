using System;

namespace ConvertVideos
{
	public class MovieMetadata
	{
		public string VideoCodecName { get; set; }
		public string VideoFrameRate { get; set; }
		public float VideoDuration { get; set; }
		public int VideoNumberOfFrames { get; set; }
		public DateTime? VideoCreationTime { get; set; }
		public string AudioCodecName { get; set; }
		public double AudioSampleRate { get; set; }
		public int AudioChannelCount { get; set; }
		public int Rotation { get; set; }
        public double? Latitude { get; set; }
        public string LatitudeRef { get; set; }
        public double? Longitude { get; set; }
        public string LongitudeRef { get; set; }


		public int RawHeight { get; set; }
		public int RawWidth { get; set; }
		public long RawSize { get; set; }
		public string RawUrl { get; set; }

		public int FullHeight { get; set; }
		public int FullWidth { get; set; }
		public long FullSize { get; set; }
		public string FullUrl { get; set; }

		public int ScaledHeight { get; set; }
		public int ScaledWidth { get; set; }
		public long ScaledSize { get; set; }
		public string ScaledUrl { get; set; }

		public int ThumbHeight { get; set; }
		public int ThumbWidth { get; set; }
		public long ThumbSize { get; set; }
		public string ThumbUrl { get; set; }

		public int ThumbSqHeight { get; set; }
		public int ThumbSqWidth { get; set; }
		public long ThumbSqSize { get; set; }
		public string ThumbSqUrl { get; set; }
	}
}

