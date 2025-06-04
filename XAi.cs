using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace BrowseJobs;

class XAi
{
    public static async Task<string> CallGrok()
    {
        var apiKey = "xai-lb1ApQ4JOd0iI15J5xeCFa5pCNFhDr15A36gWbW7CCHV1n5gE1YLAjq0CFYnT7eDvqArZjUuZrW9CVI1";

        var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

        var payload = new
        {
            model = "grok-3-latest",
            stream = false,
            temperature = 0.7,
            messages = new[]
            {
                new { role = "user", content = "What is the meaning of life, the universe, and everything?" }
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