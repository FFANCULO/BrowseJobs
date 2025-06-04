using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace BrowseJobs
{
    // Interface for Grok API interaction
    public interface IGrokApiClient
    {
        Task<string> CallApiAsync(string prompt);
    }

    // Concrete implementation of Grok API client
    public class GrokApiClient : IGrokApiClient
    {
        public string ApiKey { get; }
        public string ApiUrl { get; }
        public HttpClient Client { get; }

        public GrokApiClient(string apiKey, string apiUrl = "https://api.x.ai/v1/grok/complete")
        {
            ApiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
            ApiUrl = apiUrl ?? throw new ArgumentNullException(nameof(apiUrl));
            Client = new HttpClient();
        }

        public async Task<string> CallApiAsync(string prompt)
        {
            var requestBody = new
            {
                prompt,
                max_tokens = 1000,
                temperature = 0.7
            };

            var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");
            Client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ApiKey);

            try
            {
                var response = await Client.PostAsync(ApiUrl, content);
                response.EnsureSuccessStatusCode();
                var responseBody = await response.Content.ReadAsStringAsync();
                var json = JsonConvert.DeserializeObject<dynamic>(responseBody);
                return json?.choices[0].text.ToString() ?? new Exception("Fucked");
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"API call failed: {ex.Message}");
                return string.Empty;
            }
        }
    }

    // Builder for constructing the modified resume
    public class ResumeBuilder
    {
        private readonly IGrokApiClient _apiClient;
        private string _jobRequirements;
        private string _resumeFilePath;
        private string _resumeContent;
        private List<string> _keywords;
        private string _modifiedResume;

        public ResumeBuilder(IGrokApiClient apiClient)
        {
            _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
            _keywords = new List<string>();
        }

        public ResumeBuilder WithJobRequirements(string jobRequirements)
        {
            _jobRequirements = jobRequirements ?? throw new ArgumentNullException(nameof(jobRequirements));
            return this;
        }

        public ResumeBuilder WithResume(string resumeFilePath)
        {
            if (string.IsNullOrWhiteSpace(resumeFilePath))
                throw new ArgumentNullException(nameof(resumeFilePath), "Resume file path cannot be null or empty.");
            if (!File.Exists(resumeFilePath))
                throw new FileNotFoundException("Resume file not found.", resumeFilePath);

            _resumeFilePath = resumeFilePath;
            _resumeContent = File.ReadAllText(resumeFilePath, Encoding.UTF8);
            return this;
        }

        public async Task<ResumeBuilder> ExtractKeywordsAsync()
        {
            if (string.IsNullOrWhiteSpace(_jobRequirements))
                throw new InvalidOperationException("Job requirements must be set before extracting keywords.");

            string prompt = $@"
Analyze the following job requirements and extract key skills, qualifications, and job titles as a list of keywords. Focus on terms critical for ATS compliance, such as specific technologies, degrees, and experience levels. Return the keywords as a JSON list.

Job Requirements:
{_jobRequirements}
";

            string response = await _apiClient.CallApiAsync(prompt);
            try
            {
                _keywords = JsonConvert.DeserializeObject<List<string>>(response);
            }
            catch (JsonException)
            {
                Console.WriteLine("Failed to parse keywords. Using fallback list.");
                _keywords = new List<string> { "Software Engineer", "Python", "Django", "JavaScript", "APIs", "unit testing", "code reviews", "Bachelor’s degree", "3+ years experience", "problem-solving" };
            }

            return this;
        }

        public async Task<ResumeBuilder> ModifyResumeAsync()
        {
            if (string.IsNullOrWhiteSpace(_resumeContent))
                throw new InvalidOperationException("Resume content must be set before modification.");
            if (_keywords == null || _keywords.Count == 0)
                throw new InvalidOperationException("Keywords must be extracted before modifying resume.");

            string prompt = $@"
You are a professional resume writer specializing in ATS-compliant resumes. Rewrite the provided resume to align with the job requirements, incorporating the specified keywords naturally. Ensure the resume is concise, uses a chronological format, and includes standard headings (Contact Information, Summary, Experience, Education, Skills). Avoid graphics, tables, or complex formatting. Use action verbs and quantify achievements where possible.

Job Requirements:
{_jobRequirements}

Keywords to include:
{string.Join(", ", _keywords)}

Original Resume:
{_resumeContent}

Return the modified resume as plain text with clear section headings.
";

            _modifiedResume = await _apiClient.CallApiAsync(prompt);
            if (string.IsNullOrEmpty(_modifiedResume))
            {
                throw new InvalidOperationException("Failed to modify resume.");
            }

            return this;
        }

        public ResumeBuilder SaveToFile(string filePath)
        {
            if (string.IsNullOrEmpty(_modifiedResume))
                throw new InvalidOperationException("Resume must be modified before saving.");
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentNullException(nameof(filePath));

            File.WriteAllText(filePath, _modifiedResume, Encoding.UTF8);
            Console.WriteLine($"Modified resume saved to {filePath}");
            return this;
        }

        public string Build()
        {
            if (string.IsNullOrEmpty(_modifiedResume))
                throw new InvalidOperationException("Resume must be modified before building.");
            return _modifiedResume;
        }
    }

    // Helper class using the Builder
    public static class ResumeModifierHelper
    {
        public static async Task<string> ModifyResumeForJob(IGrokApiClient apiClient, string jobRequirements, string resumeFilePath, string outputFilePath = null)
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

            if (!string.IsNullOrEmpty(outputFilePath))
            {
                builder.SaveToFile(outputFilePath);
            }

            return builder.Build();
        }
    }

    class Program7
    {
        static async Task Main7(string[] args)
        {
            try
            {
                // Configure API client
                string apiKey = "your_grok_api_key_here"; // Replace with your xAI Grok API key
                IGrokApiClient apiClient = new GrokApiClient(apiKey);

                // Example inputs
                string jobRequirements = @"
Job Title: Software Engineer
Responsibilities: Develop web applications using Python, Django, and JavaScript. Collaborate with cross-functional teams to design APIs. Ensure code quality through unit testing and code reviews.
Requirements: Bachelor’s degree in Computer Science, 3+ years of experience with Python and Django, proficiency in JavaScript, and strong problem-solving skills.
";
                string resumeFilePath = "resume.txt"; // Ensure this file exists or update path
                string outputFilePath = "modified_resume.txt";

                // Create a sample resume file for demonstration (remove in production)
                await File.WriteAllTextAsync(resumeFilePath, @"
Name: John Doe
Contact: johndoe@email.com | 555-123-4567 | New York, NY
Summary: Experienced developer with a passion for building applications.
Experience:
  Developer, XYZ Corp, Jan 2020 - Present
  - Built web tools using Python and Flask.
  - Worked with teams to improve backend systems.
Education:
  B.S. in Computer Science, NYU, 2019
Skills: Python, Flask, SQL, teamwork
");

                // Call the helper with the Builder
                string modifiedResume = await ResumeModifierHelper.ModifyResumeForJob(apiClient, jobRequirements, resumeFilePath, outputFilePath);

                // Display result
                Console.WriteLine("\nModified Resume:\n");
                Console.WriteLine(modifiedResume);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
    }
}

/*
 * Notes for ATS Compliance:
 * - To convert to .docx, use DocumentFormat.OpenXml (Open XML SDK):
 *   Install via NuGet: `Install-Package DocumentFormat.OpenXml`
 *   Example:
 *     using DocumentFormat.OpenXml.Packaging;
 *     using DocumentFormat.OpenXml.Wordprocessing;
 *     using (WordprocessingDocument doc = WordprocessingDocument.Create("modified_resume.docx", WordprocessingDocumentType.Document))
 *     {
 *         MainDocumentPart mainPart = doc.AddMainDocumentPart();
 *         mainPart.Document = new Document();
 *         Body body = new Body();
 *         Paragraph para = new Paragraph();
 *         Run run = new Run();
 *         run.Append(new Text(modifiedResume));
 *         para.Append(run);
 *         body.Append(para);
 *         mainPart.Document.Append(body);
 *     }
 * - For PDF, use a library like iTextSharp or convert .docx to PDF.
 * - Ensure the final file uses standard fonts (e.g., Arial, Times New Roman) and avoids headers/footers.
 */