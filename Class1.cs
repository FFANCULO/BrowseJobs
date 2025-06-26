// Program.cs
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using DocumentFormat.OpenXml.Packaging;
using OpenXmlPowerTools;
using HtmlToOpenXml;
using System.Xml.Linq;
using System.Text.Json;
using HtmlConverter = OpenXmlPowerTools.HtmlConverter;

class PlaceHolder
{
    static async Task Fart(string[] args)
    {
        string inputDocx = "resume.docx";
        string outputDocx = "resume_updated.docx";

        string html = ConvertDocxToHtml(inputDocx);
        string prompt = $"Rewrite the resume sections marked with editable=\"true\" while preserving HTML tags:\n\n{html}";

        string editedHtml = await SendToLlmAsync(prompt);
        ConvertHtmlToDocx(editedHtml, outputDocx);

        Console.WriteLine("Resume processed and saved to: " + outputDocx);
    }

    static string ConvertDocxToHtml(string docxPath)
    {
        using var doc = WordprocessingDocument.Open(docxPath, false);
        HtmlConverterSettings settings = new HtmlConverterSettings();
        XElement html = HtmlConverter.ConvertToHtml(doc, settings);
        return html.ToString();
    }

    static async Task<string> SendToLlmAsync(string prompt)
    {
        using var client = new HttpClient();
        client.BaseAddress = new Uri("https://api.openai.com/v1/");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "YOUR_API_KEY");

        var body = new
        {
            model = "gpt-4",
            messages = new[]
            {
                new { role = "user", content = prompt }
            }
        };

        string json = System.Text.Json.JsonSerializer.Serialize(body);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await client.PostAsync("chat/completions", content);
        string responseContent = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(responseContent);
        return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
    }

    
    static void ConvertHtmlToDocx(string html, string outputPath)
    {
        using var memoryStream = new MemoryStream();
        using (var wordDoc = WordprocessingDocument.Create(memoryStream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            MainDocumentPart mainPart = wordDoc.AddMainDocumentPart();
            mainPart.Document = new DocumentFormat.OpenXml.Wordprocessing.Document(
                new DocumentFormat.OpenXml.Wordprocessing.Body());

            var converter = new HtmlToOpenXml.HtmlConverter(mainPart);
            converter.ParseBody(html);
        }

        File.WriteAllBytes(outputPath, memoryStream.ToArray());
    }
}


