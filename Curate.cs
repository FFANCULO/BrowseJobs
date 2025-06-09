using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Newtonsoft.Json;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace BrowseJobs;

// Interface for Grok API interaction

// Concrete implementation of Grok API client

// Builder for constructing the modified resume
public class ResumeBuilder
{
    private readonly IGrokApiClient _apiClient;
    private string _jobRequirements;
    private List<string> _keywords;
    private string _modifiedResume;
    private string _resumeContent;

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

        _resumeContent = File.ReadAllText(resumeFilePath, Encoding.UTF8);
        return this;
    }

    public static async Task<List<string>> ExtractFromFileAsync(string filePath)
    {
        byte[] jsonBytes = await File.ReadAllBytesAsync(filePath);
        return ExtractFromBytes(jsonBytes);
    }

    public static List<string> ExtractFromBytes(ReadOnlySpan<byte> utf8Json)
    {
        var reader = new Utf8JsonReader(utf8Json);

        string? contentRaw = null;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.PropertyName && reader.ValueTextEquals("content"))
            {
                reader.Read(); // to string
                contentRaw = reader.GetString();
                break;
            }
        }

        if (string.IsNullOrWhiteSpace(contentRaw))
            throw new Exception("content not found");

        // Remove markdown fences
        if (contentRaw.StartsWith("```"))
        {
            contentRaw = contentRaw
                .Replace("```json", "")
                .Replace("```", "")
                .Trim();
        }

        return JsonSerializer.Deserialize<List<string>>(contentRaw) ?? new List<string>();
    }


    public async Task<ResumeBuilder> ExtractKeywordsAsync()
    {
        if (string.IsNullOrWhiteSpace(_jobRequirements))
            throw new InvalidOperationException("Job requirements must be set before extracting keywords.");

        var prompt = $@"
            Analyze the following job requirements and extract key skills, qualifications, and job titles as a list of keywords. Focus on terms critical for ATS compliance, such as specific technologies, degrees, and experience levels. Return the keywords as a JSON list.

            Job Requirements:
            {_jobRequirements}
            ";

        var response = await _apiClient.CallApiAsync(prompt);
        try

        {
           

            // Step 1: Parse the outer JSON dynamically
            dynamic? outer = JsonConvert.DeserializeObject<ExpandoObject>(response);

            // Step 2: Grab the inner JSON string inside content
            string innerJson = outer?.choices[0].message.content ?? String.Empty;

            // Step 3: Parse that inner JSON string (which is itself a JSON object)
            dynamic? inner = JsonConvert.DeserializeObject<ExpandoObject>(innerJson);

            StringBuilder builder = new StringBuilder();
            var keywords = (List<object>)inner?.keywords! ?? new List<object>();
            List<string> kerds = keywords.Select(k => k.ToString() ?? "").ToList();
            builder.AppendJoin(" ", keywords);
            // Step 4: Access the keywords list
            
            Console.WriteLine($"- {builder}");

            _keywords = kerds;
        }
        catch (Exception)
        {
            Console.WriteLine("Failed to parse keywords. Using fallback list.");
            _keywords = new List<string>
            {
                "Software Engineer", "Python", "Django", "JavaScript", "APIs", "unit testing", "code reviews",
                "Bachelor’s degree", "3+ years experience", "problem-solving"
            };
        }

        return this;
    }

    public async Task<ResumeBuilder> ModifyResumeAsync()
    {
        if (string.IsNullOrWhiteSpace(_resumeContent))
            throw new InvalidOperationException("Resume content must be set before modification.");
        if (_keywords == null || _keywords.Count == 0)
            throw new InvalidOperationException("Keywords must be extracted before modifying resume.");

        var prompt = $@"
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
        if (string.IsNullOrEmpty(_modifiedResume)) throw new InvalidOperationException("Failed to modify resume.");

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