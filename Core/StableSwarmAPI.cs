using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Newtonsoft.Json.Linq;

namespace Hartsy.Core
{
    public class StableSwarmAPI
    {
        private static readonly HttpClient Client = new();
        private static string Session = "";
        // Swarm API base URL
        public static readonly string? BaseUrl = Environment.GetEnvironmentVariable("SWARM_URL");

        static StableSwarmAPI()
        {
            Client.DefaultRequestHeaders.Add("user-agent", "HartsyBot/1.0");
            Client.Timeout = TimeSpan.FromMinutes(4); // Adjust the timeout as needed
        }

        public class SessionInvalidException : Exception { }

        private static async Task GetSession()
        {
            try
            {
                JObject sessData = await PostJson($"{BaseUrl}/API/GetNewSession", []);
                Session = sessData["session_id"].ToString();
                Console.WriteLine($"Session acquired successfully: {Session}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetSession: {ex.Message}");
                throw; // Rethrow the exception to handle it further up the call stack if necessary
            }
        }

        public static async Task<List<string>> GenerateImage(string prompt)
        {
            // First, get a fresh session ID
            await GetSession();
            JObject request = new()
            {
                ["session_id"] = Session,
                ["prompt"] = prompt,
                ["negativeprompt"] = "malformed letters, repeating letters, double letters",
                ["images"] = 1,
                ["donotsave"] = true,
                ["model"] = "starlightXLAnimated_v3.safetensors",
                ["loras"] = "Harrlogos_v2.0.safetensors",
                ["loraweights"] = 1.2,
                ["width"] = 1024,
                ["height"] = 768,
                ["cfgscale"] = 4.5,
                ["steps"] = 35,
                ["seed"] = -1,
                ["sampler"] = "euler",
                ["scheduler"] = "karras",
            };

            try
            {
                JObject response = await PostJson($"{BaseUrl}/API/GenerateText2Image", request);
                if (response.TryGetValue("error_id", out JToken errorId) && errorId.ToString() == "invalid_session_id")
                {
                    throw new SessionInvalidException();
                }

                var images = ParseImages(response);
                Console.WriteLine($"Generated {images.Count} images.");
                return images;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GenerateImage: {ex.Message}");
                throw;
            }
        }

        private static Stream Base64ToStream(string base64String)
        {
            byte[] imageBytes = Convert.FromBase64String(base64String);
            return new MemoryStream(imageBytes);
        }

        private static List<string> ParseImages(JObject response)
        {
            var base64Images = new List<string>();
            foreach (var img in response["images"])
            {
                string base64Image = img.ToString();
                base64Images.Add(base64Image);
            }
            return base64Images;
        }
        public async Task<string> ConvertAndSaveImage(string base64Data, string username, ulong messageId, string fileExtension)
        {
            // Ensure the images directory exists
            string imagesDirectory = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "images");
            Directory.CreateDirectory(imagesDirectory);

            // Extract the base64 content from the data URI
            string base64Image = base64Data.Split(',').LastOrDefault();
            if (string.IsNullOrEmpty(base64Image))
            {
                Console.WriteLine("Invalid base64 data");
                return null;
            }

            // Construct the file path
            string date = DateTime.UtcNow.ToString("yyyy-MM-dd");
            string timeAndMessageId = $"{DateTime.UtcNow:HH-mm-ss}_{messageId}.{fileExtension}";
            string filePath = Path.Combine(imagesDirectory, username, date, timeAndMessageId);

            // Ensure the user's directory exists
            string userDirectory = Path.GetDirectoryName(filePath);
            Directory.CreateDirectory(userDirectory);

            // Convert base64 string to an image and save it
            byte[] imageBytes = Convert.FromBase64String(base64Image);
            await File.WriteAllBytesAsync(filePath, imageBytes);

            return filePath;
        }
        private static async Task<JObject> PostJson(string url, JObject jsonData)
        {
            try
            {
                var content = new StringContent(jsonData.ToString(), Encoding.UTF8, "application/json");
                using var response = await Client.PostAsync(url, content);
                string result = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"API Request Failed: {response.StatusCode} - {result}");
                }

                return JObject.Parse(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in PostJson: {ex.Message}");
                throw;
            }
        }
    }
}