using System;
using System.Threading.Tasks;

namespace BrowseJobs;

public static class ResumeModifierHelper
{
    public static async Task<string> ModifyResumeForJob(IGrokApiClient apiClient, string jobRequirements,
        string resumeFilePath, string outputFilePath = null)
    {
        if (string.IsNullOrWhiteSpace(jobRequirements))
            throw new ArgumentNullException(nameof(jobRequirements), "Job requirements cannot be null or empty.");
        if (string.IsNullOrWhiteSpace(resumeFilePath))
            throw new ArgumentNullException(nameof(resumeFilePath), "Resume file path cannot be null or empty.");
        if (apiClient == null)
            throw new ArgumentNullException(nameof(apiClient), "API client cannot be null.");

        var builder = new ResumeBuilder(apiClient)
            .WithJobRequirements(jobRequirements)
            .WithResume(resumeFilePath);

        await builder.ExtractKeywordsAsync();
        await builder.ModifyResumeAsync();

        if (!string.IsNullOrEmpty(outputFilePath)) builder.SaveToFile(outputFilePath);

        return builder.Build();
    }
}