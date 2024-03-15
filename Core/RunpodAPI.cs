using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Hartsy.Core
{
    public class RunpodAPI
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl = "https://api.runpod.ai/v1";
        private readonly string _apiKey;

        public RunpodAPI()
        {
            _httpClient = new HttpClient();
            _apiKey = Environment.GetEnvironmentVariable("RUNPOD_KEY") ?? throw new InvalidOperationException("Runpod API key is not set in environment variables.");
        }

        public async Task<string> CreateImageAsync(ulong discordId, string positive, string negative, string checkpoint, int batch = 1, int width = 1024, int height = 1024)
        {
            var payload = new
            {
                batch,
                positive,
                negative,
                checkpoint,
                width,
                height,
            };

            string requestUri = $"{_baseUrl}";

            try
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

                string jsonPayload = JsonSerializer.Serialize(payload);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(requestUri, content);

                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<RunpodResponse>(responseContent);

                return result?.Id.ToString() ?? string.Empty;
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine($"An error occurred connecting to Runpod API: {e.Message}");
                throw;
            }
            catch (Exception e)
            {
                Console.WriteLine($"An unexpected error occurred: {e.Message}");
                throw;
            }
        }

        private class RunpodResponse
        {
            public int Id { get; set; }
        }
    }
}