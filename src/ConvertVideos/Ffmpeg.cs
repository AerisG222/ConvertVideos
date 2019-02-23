using System;
using System.Collections.Generic;
using System.Diagnostics;


namespace ConvertVideos
{
    public abstract class Ffmpeg
    {
        public const string FIELD_CODEC_TYPE = "codec_type";

        public const string STREAM_START = "[STREAM]";
        public const string STREAM_END = "[/STREAM]";
        public const string CODEC_TYPE_AUDIO = "audio";
        public const string CODEC_TYPE_VIDEO = "video";

        public static string FfmpegPath { get; set; }
        public static string FfprobePath { get; set; }


        // abstract methods - to be implemented by specific codec writers
        public abstract string OutputFileExtension { get; }
        public abstract void Convert(string infile, string outfile);
        public abstract void Convert(string infile, string outfile, int newWidth, int newHeight);


        public void CreateThumbnail(string file, string outfile, int thumbWidth, int thumbHeight)
        {
            var args = $"-i \"{file}\" -s {thumbWidth}x{thumbHeight} -ss 00:00:02 -vframes 1 \"{outfile}\"";

            ExecuteFfmpeg(args);
        }


        public void ExtractFrame(string moviePath, string imagePath)
        {
            var args = $"-i \"{moviePath}\" -ss 00:00:02 -vframes 1 \"{imagePath}\"";

            ExecuteFfmpeg(args);
        }


        public MovieMetadata GatherMetadata(string file)
        {
            string data = CollectMovieMetadataFromFile(file);

            string[] lines = data.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

            bool inStreamSection = false;
            Dictionary<string, string> dict = new Dictionary<string, string>();
            Dictionary<string, string> vDic = null;
            Dictionary<string, string> aDic = null;

            foreach (string line in lines)
            {
                if (!inStreamSection)
                {
                    if (string.Equals(line, STREAM_START, StringComparison.OrdinalIgnoreCase))
                    {
                        inStreamSection = true;

                        dict = new Dictionary<string, string>();
                        continue;
                    }
                    else
                    {
                        continue;
                    }
                }
                else
                {
                    if (string.Equals(line, STREAM_END, StringComparison.OrdinalIgnoreCase))
                    {
                        inStreamSection = false;

                        if (!dict.ContainsKey(FIELD_CODEC_TYPE))
                        {
                            throw new Exception("Expected to find codec_type!!!");
                        }

                        if (string.Equals(dict[FIELD_CODEC_TYPE], CODEC_TYPE_AUDIO, StringComparison.OrdinalIgnoreCase))
                        {
                            aDic = dict;
                        }
                        else if (string.Equals(dict[FIELD_CODEC_TYPE], CODEC_TYPE_VIDEO, StringComparison.OrdinalIgnoreCase))
                        {
                            vDic = dict;
                        }
                        else
                        {
                            Console.WriteLine("Unknown codec type: " + dict[FIELD_CODEC_TYPE] + " ignoring this!");
                        }

                        continue;
                    }
                    else
                    {
                        string[] vals = line.Split(new char[] { '=' });

                        if (vals.Length == 2)
                        {
                            dict.Add(vals[0].Trim(), vals[1].Trim());
                        }
                    }
                }
            }

            if (vDic == null)
            {
                throw new Exception("Expecting to find a video stream!");
            }

            return BuildMovieMetadata(vDic, aDic);
        }


        protected void ExecuteFfmpeg(string arguments)
        {
            Process ffmpeg = null;

            try
            {
                // capture the output, otherwise it won't make sense w/ many processes writing to stdout
                ffmpeg = new Process();

                ffmpeg.StartInfo.FileName = FfmpegPath;
                ffmpeg.StartInfo.Arguments = arguments;
                ffmpeg.StartInfo.UseShellExecute = false;
                ffmpeg.StartInfo.RedirectStandardOutput = true;
                ffmpeg.Start();

                ffmpeg.StandardOutput.ReadToEnd();

                ffmpeg.WaitForExit();
            }
            finally
            {
                ffmpeg.Dispose();
            }
        }


        static MovieMetadata BuildMovieMetadata(Dictionary<string, string> vDic, Dictionary<string, string> aDic)
        {
            var mm = new MovieMetadata();

            foreach (string key in vDic.Keys)
            {
                switch (key)
                {
                    case "codec_name":
                        mm.VideoCodecName = vDic[key];
                        break;
                    case "width":
                        mm.RawWidth = int.Parse(vDic[key]);
                        break;
                    case "height":
                        mm.RawHeight = int.Parse(vDic[key]);
                        break;
                    case "r_frame_rate":
                        mm.VideoFrameRate = vDic[key];
                        break;
                    case "duration":
                        float f;

                        if (float.TryParse(vDic[key], out f))
                        {
                            mm.VideoDuration = f;
                        }

                        break;
                    case "nb_frames":
                        int i;

                        if (int.TryParse(vDic[key], out i))
                        {
                            mm.VideoNumberOfFrames = i;
                        }

                        break;
                    case "TAG:creation_time":
                        DateTime dt;

                        if (DateTime.TryParse(vDic[key], out dt))
                        {
                            mm.VideoCreationTime = dt;
                        }

                        break;
                    case "TAG:rotate":
                        int rotation;

                        if (int.TryParse(vDic[key], out rotation))
                        {
                            mm.Rotation = rotation;
                        }

                        break;
                }
            }

            // if rotation is reported, swap width/height to compensate
            if (mm.Rotation != 0)
            {
                if (Math.Abs(mm.Rotation) == 90 || Math.Abs(mm.Rotation) == 270)
                {
                    var tmp = mm.RawWidth;

                    mm.RawWidth = mm.RawHeight;
                    mm.RawHeight = tmp;
                }
            }

            if(aDic != null)
            {
                foreach (string key in aDic.Keys)
                {
                    switch (key)
                    {
                        case "codec_name":
                            mm.AudioCodecName = aDic[key];
                            break;
                        case "channels":
                            mm.AudioChannelCount = int.Parse(aDic[key]);
                            break;
                        case "sample_rate":
                            mm.AudioSampleRate = float.Parse(aDic[key]);
                            break;
                    }
                }
            }

            return mm;
        }


        static string CollectMovieMetadataFromFile(string imagePath)
        {
            Process ffprobe = null;

            try
            {
                ffprobe = new Process();

                ffprobe.StartInfo.FileName = FfprobePath;
                ffprobe.StartInfo.Arguments = string.Concat("-show_streams \"", imagePath, "\"");
                ffprobe.StartInfo.UseShellExecute = false;
                ffprobe.StartInfo.RedirectStandardOutput = true;
                ffprobe.Start();

                string output = ffprobe.StandardOutput.ReadToEnd();

                ffprobe.WaitForExit();

                return output;
            }
            finally
            {
                ffprobe.Dispose();
            }
        }
    }
}

