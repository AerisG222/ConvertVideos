using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using ConvertVideos.Processor;
using ConvertVideos.ResultWriter;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NExifTool;

namespace ConvertVideos;

public class Program
{
    public static async Task Main(string[] args)
    {
        var host = CreateHostBuilder(args).Build();
        var log = host.Services.GetRequiredService<ILogger<Program>>();
        var sw = new Stopwatch();

        try
        {
            log.LogInformation("Starting to process videos at {Time}", DateTime.Now);

            sw.Start();
            await host.RunAsync();
            sw.Stop();
        }
        catch(Exception ex)
        {
            log.LogError(ex, "Error encountered running application: {Error}", ex.Message);
            Environment.Exit(1);
        }

        log.LogInformation("Completed processing videos, took {Seconds} seconds", sw.Elapsed.TotalSeconds);

        Environment.Exit(0);
    }

    static IHostBuilder CreateHostBuilder(string[] args)
    {
        var opts = new Options();
        opts.Parse(args);

        ValidateOptions(opts);

        return Host
            .CreateDefaultBuilder()
            .ConfigureServices((hostContext, services) =>
            {
                services
                    .AddSingleton(opts)
                    .AddSingleton(new ExifTool(new ExifToolOptions()))
                    .AddSingleton<IResultWriter, PgSqlResultWriter>()
                    .AddSingleton<IVideoProcessor, VideoProcessor>()
                    .AddHostedService<Worker>();
            });
    }

    static void ValidateOptions(Options opts)
    {
        if (!opts.VideoDirectory.Exists)
        {
            throw new DirectoryNotFoundException($"The video directory specified, {opts.VideoDirectory}, does not exist.  Please specify a directory containing images.");
        }

        if (opts.OutputFile.Exists)
        {
            throw new IOException($"The specified output file, {opts.OutputFile}, already exists.  Please remove it before running this process.");
        }
    }
}
