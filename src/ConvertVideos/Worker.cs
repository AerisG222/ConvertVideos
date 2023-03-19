using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ConvertVideos.Processor;
using ConvertVideos.ResultWriter;

namespace ConvertVideos;

public class Worker
    : BackgroundService
{
    static readonly string[] SOURCE_EXTENSIONS = new string[] { ".flv", ".vob", ".mpg", ".mpeg", ".avi", ".3gp", ".m4v", ".mp4", ".mov" };

    readonly ILogger _log;
    readonly Options _opts;
    readonly IResultWriter _writer;
    readonly IHostApplicationLifetime _appLifetime;
    readonly IVideoProcessor _processor;

    public Worker(
        IHostApplicationLifetime appLifetime,
        ILogger<Worker> log,
        Options opts,
        IResultWriter writer,
        IVideoProcessor processor
    ) {
        _appLifetime = appLifetime ?? throw new ArgumentNullException(nameof(appLifetime));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _opts = opts ?? throw new ArgumentNullException(nameof(opts));
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _processor = processor ?? throw new ArgumentNullException(nameof(processor));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var files = GetMovieFiles().ToArray();
        var results = new MovieMetadata[files.Length];
        var opts = new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Max(Environment.ProcessorCount - 1, 1)
        };

        await Parallel.ForEachAsync(Enumerable.Range(0, files.Length), opts, async (index, token) => {
            var file = files[index];

            results[index] = await _processor.ProcessVideoAsync(file);
        });

        _writer.WriteOutput(_opts.OutputFile.FullName, _opts.CategoryInfo, results);

        _appLifetime.StopApplication();
    }

    IList<string> GetMovieFiles()
    {
        return _opts
            .VideoDirectory
            .EnumerateFiles()
            .Where(f => SOURCE_EXTENSIONS.Contains(f.Extension.ToLower()))
            .Select(f => f.FullName)
            .ToList();
    }
}
