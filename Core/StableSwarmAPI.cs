using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Supabase.Gotrue;
using Supabase.Interfaces;

namespace Hartsy.Core
{
    public class StableSwarmAPI
    {
        private static readonly HttpClient Client = new HttpClient();
        private ClientWebSocket _webSocket;
        private readonly string _swarmURL;
        private static string Session = "";


        public StableSwarmAPI()
        {
            _webSocket = new ClientWebSocket();
            _swarmURL = Environment.GetEnvironmentVariable("SWARM_URL");
        }

        private async Task EnsureWebSocketConnectionAsync()
        {
            if (_webSocket.State != WebSocketState.Open)
            {
                Uri serverUri = new Uri($"{_swarmURL.Replace("http", "ws")}/API/GenerateText2ImageWS");
                await _webSocket.ConnectAsync(serverUri, CancellationToken.None);
            }
        }

        async Task GetSession()
        {
            try
            {
                JObject sessData = await PostJson($"{_swarmURL}/API/GetNewSession", []);
                Session = sessData["session_id"].ToString();
                Console.WriteLine($"Session acquired successfully: {Session}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetSession: {ex.Message}");
                throw; // Rethrow the exception to handle it further up the call stack if necessary
            }
        }

        public async IAsyncEnumerable<(string imageBase64, bool isFinal)> GenerateImage(string prompt)
        {
            await GetSession();
            await EnsureWebSocketConnectionAsync();

            var request = new
            {
                session_id = Session,
                prompt = prompt,
                negativeprompt = "malformed letters, repeating letters, double letters",
                images = 1,
                donotsave = true,
                model = "starlightXLAnimated_v3.safetensors",
                loras = "Harrlogos_v2.0.safetensors",
                loraweights = 1,
                width = 1024,
                height = 768,
                cfgscale = 5.5,
                steps = 34,
                seed = -1,
                sampler = "dpmpp_3m_sde",
                scheduler = "karras",
            };

            string requestJson = JsonConvert.SerializeObject(request);
            ArraySegment<byte> buffer = new ArraySegment<byte>(Encoding.UTF8.GetBytes(requestJson));

            await _webSocket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);

            var responseBuffer = new ArraySegment<byte>(new byte[8192]);
            StringBuilder stringBuilder = new StringBuilder();
            int previewCount = 0;

            while (_webSocket.State == WebSocketState.Open)
            {
                WebSocketReceiveResult result;
                stringBuilder.Clear();

                do
                {
                    result = await _webSocket.ReceiveAsync(responseBuffer, CancellationToken.None);
                    var jsonStringFragment = Encoding.UTF8.GetString(responseBuffer.Array, responseBuffer.Offset, result.Count);
                    stringBuilder.Append(jsonStringFragment);
                } while (!result.EndOfMessage);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                    break;
                }
                else
                {
                    var jsonString = stringBuilder.ToString();
                    var responseData = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonString);

                    if (responseData != null)
                    {

                        if (responseData.ContainsKey("gen_progress"))
                        {
                            var genProgressData = JsonConvert.DeserializeObject<Dictionary<string, object>>(responseData["gen_progress"].ToString());
                            //Console.WriteLine($"Generation progress data: {JsonConvert.SerializeObject(genProgressData, Formatting.Indented)}");

                            // Print the total percent of completion
                            //if (genProgressData.ContainsKey("overall_percent"))
                            //{
                            //    var overallPercent = genProgressData["overall_percent"].ToString();
                            //    Console.WriteLine($"Generation progress: {overallPercent}%");
                            //}
                            if (genProgressData.ContainsKey("preview"))
                            {
                                previewCount++;
                                if (previewCount % 4 == 0)
                                {
                                    var previewData = genProgressData["preview"].ToString();
                                    Console.WriteLine("Preview data received");
                                    yield return (previewData, isFinal: false);
                                }
                            }
                        }

                        if (responseData.ContainsKey("image"))
                        {
                            string finalImage = responseData["image"].ToString();
                            Console.WriteLine("Final image received");
                            if (_webSocket.State != WebSocketState.Closed)
                            {
                                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Completed", CancellationToken.None);
                            }
                            yield return (finalImage, isFinal: true);
                        }
                    }
                }
            }
        }

        public async Task<string> ConvertAndSaveImage(string base64Data, string username, ulong messageId, string fileExtension, bool isFinal)
        {
            // Ensure the images directory exists
            string imagesDirectory = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "images");
            Directory.CreateDirectory(imagesDirectory);

            // Check if the base64 data contains the prefix and remove it
            var base64Prefix = "base64,";
            var dataIndex = base64Data.IndexOf(base64Prefix);
            if (dataIndex > -1)
            {
                base64Data = base64Data.Substring(dataIndex + base64Prefix.Length);
            }

            // Trim any whitespace characters from the base64 string
            base64Data = base64Data.Trim();

            // Construct the file path with a unique identifier
            string dateTimeFormat = "yyyy-MM-dd_HH-mm-ss-fff";
            string date = DateTime.UtcNow.ToString(dateTimeFormat);
            string identifier = isFinal ? "final" : Guid.NewGuid().ToString();
            string timeAndMessageId = $"{date}_{messageId}_{identifier}.{fileExtension}";
            string userDirectory = Path.Combine(imagesDirectory, username);
            string filePath = Path.Combine(userDirectory, timeAndMessageId);

            // Ensure the user's directory exists
            Directory.CreateDirectory(userDirectory);

            try
            {
                // Convert base64 string to an image and save it
                byte[] imageBytes = Convert.FromBase64String(base64Data);
                await File.WriteAllBytesAsync(filePath, imageBytes);
                return filePath;
            }
            catch (FormatException ex)
            {
                Console.WriteLine($"Error converting base64 to image: {ex.Message}");
                return null;
            }
        }

        private bool IsBase64String(string base64)
        {
            Span<byte> buffer = new Span<byte>(new byte[base64.Length]);
            return Convert.TryFromBase64String(base64, buffer, out _);
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
