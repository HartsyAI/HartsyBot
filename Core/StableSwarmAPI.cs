using System.Net.WebSockets;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp;

namespace Hartsy.Core
{
    public class StableSwarmAPI
    {
        private static readonly HttpClient Client = new HttpClient();
        private readonly string _swarmURL;
        private static int batchCount = 0;
        private const int batchProcessFrequency = 2;

        public StableSwarmAPI()
        {
            _swarmURL = Environment.GetEnvironmentVariable("SWARM_URL");
        }

        public async Task<string> GetSession()
        {
            try
            {
                JObject sessData = await PostJson($"{_swarmURL}/API/GetNewSession", new JObject());
                string sessionId = sessData["session_id"].ToString();
                Console.WriteLine($"Session acquired successfully: {sessionId}");
                return sessionId;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetSession: {ex.Message}");
                throw;
            }
        }


        private async Task EnsureWebSocketConnectionAsync(ClientWebSocket webSocket)
        {
            if (webSocket.State != WebSocketState.Open)
            {
                Uri serverUri = new Uri($"{_swarmURL.Replace("http", "ws")}/API/GenerateText2ImageWS");
                await webSocket.ConnectAsync(serverUri, CancellationToken.None);
            }
        }

        private async Task SendRequestAsync(ClientWebSocket webSocket, object request)
        {
            string requestJson = JsonConvert.SerializeObject(request);
            ArraySegment<byte> buffer = new ArraySegment<byte>(Encoding.UTF8.GetBytes(requestJson));
            await webSocket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
        }

        private async Task<WebSocketReceiveResult> ReceiveMessage(ClientWebSocket webSocket, StringBuilder stringBuilder, ArraySegment<byte> responseBuffer)
        {
            WebSocketReceiveResult result;
            do
            {
                result = await webSocket.ReceiveAsync(responseBuffer, CancellationToken.None);
                var jsonStringFragment = Encoding.UTF8.GetString(responseBuffer.Array, responseBuffer.Offset, result.Count);
                stringBuilder.Append(jsonStringFragment);
            } while (!result.EndOfMessage);

            return result;
        }

        private async Task<Dictionary<string, object>> CreateRequestObject(Dictionary<string, object> payload)
        {
            payload["session_id"] = await GetSession();

            // Remove all entries where the value is null
            var keysToRemove = payload.Where(kvp => kvp.Value == null).Select(kvp => kvp.Key).ToList();
            foreach (var key in keysToRemove)
            {
                payload.Remove(key);
            }

            return payload;
        }

        public async IAsyncEnumerable<(Image<Rgba32> Image, bool IsFinal)> GetImages(Dictionary<string, object> payload, string username, ulong messageId)
        {
            var webSocket = new ClientWebSocket();
            await EnsureWebSocketConnectionAsync(webSocket);
            var request = await CreateRequestObject(payload);

            await SendRequestAsync(webSocket, request);

            var responseBuffer = new ArraySegment<byte>(new byte[8192]);
            StringBuilder stringBuilder = new StringBuilder();

            Dictionary<int, Dictionary<string, string>> previewImages = new Dictionary<int, Dictionary<string, string>>();
            Dictionary<int, Dictionary<string, string>> finalImages = new Dictionary<int, Dictionary<string, string>>();

            while (webSocket.State == WebSocketState.Open)
            {
                stringBuilder.Clear();
                WebSocketReceiveResult result = await ReceiveMessage(webSocket, stringBuilder, responseBuffer);

                if (result.MessageType == WebSocketMessageType.Close)
                    break;
                string jsonString = stringBuilder.ToString();
                string logString = ReplaceBase64(jsonString);
                Console.WriteLine("Response JSON (excluding base64 data): " + logString);
                var responseData = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonString);

                foreach (var kvp in responseData)
                {
                    if (responseData != null)
                    {
                        if (kvp.Value is JObject genProgressData)
                        {
                            if (genProgressData.ContainsKey("preview"))
                            {
                                bool isFinal = false;
                                int batchIndex = Convert.ToInt32(genProgressData["batch_index"]);
                                string base64WithPrefix = genProgressData["preview"].ToString();
                                string overall = genProgressData["overall_percent"].ToString();
                                string current = genProgressData["current_percent"].ToString();
                                string base64 = await RemovePrefix(base64WithPrefix);
                                previewImages[batchIndex] = new Dictionary<string, string> { { "base64", $"{base64}" } };
                                if (batchIndex == 3)
                                {
                                    batchCount++;
                                    Image<Rgba32> preview = await HandlePreview(previewImages, batchCount, username, messageId);
                                    if (preview == null)
                                    {
                                        continue;
                                    }
                                    yield return (preview, isFinal);
                                }
                                else
                                {
                                    yield return (null, isFinal);
                                }
                                // TODO: Do we waant to do something with the status data?
                            }
                            else if (responseData.ContainsKey("status") && responseData["status"] is Dictionary<string, object> statusData)
                            {
                                // List of expected status fields
                                var statusFields = new[] { "waiting_gens", "loading_models", "waiting_backends", "live_gens" };

                                foreach (var field in statusFields)
                                {
                                    // Safely get the value of each field, defaulting to 0 if not found
                                    statusData.TryGetValue(field, out object value);
                                }
                            }
                        }

                        if (responseData.ContainsKey("image"))
                        {
                            bool isFinal = true;
                            int batchIndex = Convert.ToInt32(responseData["batch_index"]);
                            string base64WithPrefix = responseData["image"].ToString();
                            string base64 = await RemovePrefix(base64WithPrefix);
                            finalImages[batchIndex] = new Dictionary<string, string> { { "base64", $"{base64}" } };
                            
                            if (batchIndex == 3)
                            {
                                Image<Rgba32> final = await HandleFinal(finalImages, username, messageId);
                                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "All final images received", CancellationToken.None);
                                yield return (final, isFinal);
                                break;

                            }
                        }
                    }
                }
            }
        }
        // DEBUG ONLY
        private string ReplaceBase64(string jsonString)
        {
            const string previewPrefix = "\"preview\":\"data:image/jpeg;base64,";
            const string imagePrefix = "\"image\":\"data:image/jpeg;base64,";

            string ReplaceBase64Content(string str, string prefix)
            {
                int start = str.IndexOf(prefix);
                while (start != -1)
                {
                    int end = str.IndexOf("\"", start + prefix.Length);
                    if (end != -1)
                    {
                        str = str.Remove(start, end - start + 1).Insert(start, $"{prefix}[BASE64_DATA]\"");
                    }
                    start = str.IndexOf(prefix, start + prefix.Length);
                }
                return str;
            }

            jsonString = ReplaceBase64Content(jsonString, previewPrefix);
            jsonString = ReplaceBase64Content(jsonString, imagePrefix);

            return jsonString;
        }

        /// <summary>Processes the preview images for a given batch.</summary>
        /// <param name="previewImages">A dictionary of image data where each key represents a batch index, 
        /// and the value is another dictionary containing image base64.</param>
        /// <param name="batchCount">How many batches have been processed.</param>
        /// <returns>A grid image of the preview images for the batch if count meets frequency; otherwise, null.</returns>
        private async Task<Image<Rgba32>> HandlePreview(Dictionary<int, Dictionary<string, string>> previewImages, int batchCount, string username, ulong messageId)
        {
            if (batchCount % batchProcessFrequency == 0)
            {
                Image<Rgba32> gridImage = await ImageGrid.CreateGridAsync(previewImages, username, messageId);
                return gridImage;
            }
            return null;
        }

        /// <summary>Processes the final images, generating a grid image from the base64.</summary>
        /// <param name="finalImages">A dictionary where each key represents a batch index, another dictionary containing base64.</param>
        /// <returns>A grid image composed of the final images.</returns>
        private async Task<Image<Rgba32>> HandleFinal(Dictionary<int, Dictionary<string, string>> finalImages, string username, ulong messageId)
        {
            Image<Rgba32> gridImage = await ImageGrid.CreateGridAsync(finalImages, username, messageId);
            return gridImage;
        }
        
        private async Task HandleStatus(Dictionary<int, Dictionary<string, string>> status)
        {
            Console.WriteLine("Status received");
        }

        /// <summary>Removes the Base64 prefix from a Base64 string if it exists.</summary>
        /// <param name="base64">The Base64 string that may contain a prefix.</param>
        /// <returns>A Base64 string without the prefix.</returns>
        public static Task<string> RemovePrefix(string base64)
        {
            if (string.IsNullOrWhiteSpace(base64))
            {
                throw new ArgumentException("Base64 string cannot be null or whitespace.", nameof(base64));
            }
            const string base64Prefix = "base64,";
            int base64StartIndex = base64.IndexOf(base64Prefix);
            if (base64StartIndex != -1)
            {
                base64 = base64.Substring(base64StartIndex + base64Prefix.Length);
            }
            return Task.FromResult(base64);
        }

        public static async Task<JObject> PostJson(string url, JObject jsonData)
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