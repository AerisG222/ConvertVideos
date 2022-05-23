namespace ConvertVideos;

// http://www.twm-kd.com/linux/encoding-videos-with-ffmpeg-and-h-264/
// https://www.virag.si/2012/01/web-video-encoding-tutorial-with-ffmpeg-0-9/
// http://ffmpeg.org/trac/ffmpeg/wiki/x264EncodingGuide
public class FfmpegH264
    : Ffmpeg
{
    public override string OutputFileExtension { get { return ".mp4"; } }

    // note: for some videos, the audio in the source was screwy, and caused encoding issues - to address this, added the following parameter (just before the outfile):
    // -b:a 32k
    // however, i prefer not to keep this in here, so if you run into this issue again, try adding it back to see if it fixes it!
    public override void Convert(string infile, string outfile)
    {
        var args = string.Concat("-i \"", infile, "\" -vcodec libx264 -preset slow -crf 20 -strict -2 -acodec aac \"", outfile, "\"");
        //var args = string.Concat("-i \"", infile, "\" -vcodec libx264 -preset slow -crf 20 -strict -2 -acodec aac -b:a 32k \"", outfile, "\"");

        ExecuteFfmpeg(args);
    }

    public override void Convert(string infile, string outfile, int newWidth, int newHeight)
    {
        var args = string.Concat("-i \"", infile, "\" -vcodec libx264 -preset slow -crf 20 -vf scale=\"", newWidth, ":", newHeight, "\" -strict -2 -acodec aac \"", outfile, "\"");
        //var args = string.Concat("-i \"", infile, "\" -vcodec libx264 -preset slow -crf 20 -vf scale=\"", newWidth, ":", newHeight, "\" -strict -2 -acodec aac -b:a 32k \"", outfile, "\"");

        ExecuteFfmpeg(args);
    }
}
