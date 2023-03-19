using System;

namespace ConvertVideos;

public class Program
{
    public static void Main(string[] args)
    {
        var opts = new Options();
        opts.Parse(args);

        var worker = new Worker(opts);

        worker.Execute();

        Environment.Exit(0);
    }
}
