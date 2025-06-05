using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Microsoft.Extensions.Configuration;

namespace BrowseJobs;

class XAi
{
    public static async Task<string> CallGrok(string prompt)
    {

        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .Build();

        string apiKeyConfig = config["MySettings:ApiKey"] ?? throw new InvalidOperationException("MySettings:ApiKey cannot be found");
        Console.WriteLine($"API Key: {apiKeyConfig}");



        var apiKey = "xai-Spjy8ZfUf3RKB9TaIfu1kOD0se41eUGlZOvH5q3I3eoeFLHpvqBUDBlH0D2yhAb1QtjXazKn6BiorLar";

        var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

        var payload = new
        {
            model = "grok-3-latest",
            stream = false,
            temperature = 0.7,
            messages = new[]
            {
                new { role = "user", content = prompt }
            }
        };

        var json = JsonConvert.SerializeObject(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await httpClient.PostAsync("https://api.x.ai/v1/chat/completions", content);
        var responseString = await response.Content.ReadAsStringAsync();

        Console.WriteLine($"\ud83d\udd01 Response:\n{responseString}");

        return responseString;
    }
}