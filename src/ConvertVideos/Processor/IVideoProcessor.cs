using System.Threading.Tasks;

namespace ConvertVideos.Processor;

public interface IVideoProcessor
{
    Task<MovieMetadata> ProcessVideoAsync(string file);
}
