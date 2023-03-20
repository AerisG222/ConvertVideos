using System.Collections.Generic;

namespace ConvertVideos.ResultWriter;

public interface IResultWriter
{
    void WriteOutput(string outputFile, CategoryInfo category, IEnumerable<MovieMetadata> movies);
}
