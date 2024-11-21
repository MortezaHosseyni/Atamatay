using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace Atamatay.Services
{
    public interface IGptService
    {
        Task<string> Post(string worldName, string playerDetails);
    }
    public class GptService(HttpClient httpClient) : IGptService
    {
        private const string Host = "https://api.openai.com/v1/chat/completions";

        private readonly HttpClient _httpClient = httpClient;

        public async Task<string> Post(string worldName, string playerDetails)
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();
            var apiKey = config["GPTToken"];

            using var client = new HttpClient();

            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

            var requestBody = new
            {
                model = "gpt-4o-mini",
                messages = new[]
                {
                    new { role = "system", content = "You are a DM (Dungeon Master)." },
                    new { role = "user", content = $"Lets play RPG/FRP D&D game.\nWorld name is '{worldName}'.\nPlayers: {playerDetails}\nChoose name and abilities for these races and start the story." }
                },
                temperature = 0.7
            };

            var jsonPayload = System.Text.Json.JsonSerializer.Serialize(requestBody);

            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            try
            {
                var response = await client.PostAsync(Host, content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    using var jsonDoc = JsonDocument.Parse(responseContent);
                    var contentField = jsonDoc.RootElement
                        .GetProperty("choices")[0]
                        .GetProperty("message")
                        .GetProperty("content")
                        .GetString();

                    return contentField;
                }
                else
                {
                    Console.WriteLine($"Error: {response.StatusCode}");
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Details: {errorContent}");
                    return errorContent;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception occurred:");
                Console.WriteLine(ex.Message);
                return ex.Message;
            }
        }
    }
}
