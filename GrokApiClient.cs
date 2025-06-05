using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace BrowseJobs;

public class GrokApiClient : IGrokApiClient
{
    public GrokApiClient(string apiKey, string apiUrl = "https://api.x.ai/v1/grok/complete")
    {
        ApiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
        ApiUrl = apiUrl ?? throw new ArgumentNullException(nameof(apiUrl));
        Client = new HttpClient();
    }

    public GrokApiClient()
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .Build();

        var apiKey = config["MySettings:ApiKey"] ?? null;
        Console.WriteLine($"API Key: {apiKey}");

        ApiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
        ApiUrl = "https://api.x.ai/v1/chat/completions";
        Client = new HttpClient();
    }

    public string ApiKey { get; }
    public string ApiUrl { get; }
    public HttpClient Client { get; }

    public async Task<string> CallApiAsync(string prompt)
    {
        return await XAi.CallGrok(prompt);

        var requestBody = new
        {
            prompt,
            max_tokens = 1000,
            temperature = 0.7
        };

        var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);

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