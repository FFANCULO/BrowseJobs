using System.Threading.Tasks;

namespace BrowseJobs;

public interface IGrokApiClient
{
    Task<string> CallApiAsync(string prompt);
}