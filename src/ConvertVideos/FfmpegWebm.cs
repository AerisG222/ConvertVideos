namespace ConvertVideos;

public class FfmpegWebm
    : Ffmpeg
{
    public override string OutputFileExtension { get { return ".webm"; } }


    public override void Convert(string infile, string outfile)
    {
        var args = string.Concat("-i \"", infile, "\" -f webm -vcodec libvpx -acodec libvorbis -aq 50 \"", outfile, "\"");

        ExecuteFfmpeg(args);
    }


    public override void Convert(string infile, string outfile, int newWidth, int newHeight)
    {
        var args = string.Concat("-i \"", infile, "\" -s ", newWidth, "x", newHeight, " -f webm -vcodec libvpx -acodec libvorbis -aq 50 \"", outfile, "\"");

        ExecuteFfmpeg(args);
    }
}

