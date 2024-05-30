using Supabase;
using static Postgrest.Constants;
using dotenv.net;
using Hartsy.Core.SupaBase.Models;

namespace Hartsy.Core.SupaBase
{
    public class SupabaseClient
    {
        public Client? supabase;

        public SupabaseClient() => InitializeSupabase().GetAwaiter().GetResult();

        /// <summary>Initializes the Supabase client with the provided URL and key.</summary>
        /// <returns>A task representing the asynchronous operation of initializing the Supabase client.</returns>
        private async Task InitializeSupabase()
        {
            string? url = Environment.GetEnvironmentVariable("SUPABASE_URL");
            string? key = Environment.GetEnvironmentVariable("SUPABASE_SERVICE_KEY");
            if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(key))
            {
                string envFilePath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "../../../.env"));
                Console.WriteLine("Attempting to load .env file from: " + envFilePath);
                if (File.Exists(envFilePath))
                {
                    var envOptions = new DotEnvOptions(envFilePaths: [envFilePath]);
                    DotEnv.Load(envOptions);
                    url = Environment.GetEnvironmentVariable("SUPABASE_URL");
                    key = Environment.GetEnvironmentVariable("SUPABASE_SERVICE_KEY");
                }
            }
            SupabaseOptions options = new()
            {
                AutoConnectRealtime = true
            };
            supabase = new Client(url!, key, options);
            await supabase.InitializeAsync();
        }

        /// <summary>Checks if a Discord ID is linked to a user in the database.</summary>
        /// <param name="discordId">The Discord ID to check.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a boolean indicating if the Discord ID is linked.</returns>
        public async Task<bool> IsDiscordLinked(string discordId)
        {
            try
            {
                // Check if any user has the provided Discord ID
                Postgrest.Responses.ModeledResponse<Users> result = await supabase!
                    .From<Users>()
                    .Select("*") // Selects all fields; replace '*' with specific fields as needed
                    .Filter("provider_id", Operator.Equals, discordId)
                    .Get();
                // If the response contains any users, it means the user's Discord ID is linked
                bool isLinked = result.Models?.Count > 0;
                return isLinked;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception when checking Discord link: {ex.Message}");
                return false; // Return false in case of error
            }
        }

        /// <summary>Retrieves a user from the database by their Discord ID.</summary>
        /// <param name="discordId">The Discord ID of the user to retrieve.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the user object or null if not found.</returns>
        public async Task<Users?> GetUserByDiscordId(string discordId)
        {
            try
            {
                Users? response = await supabase!
                    .From<Users>()
                    .Select("*")
                    .Filter("provider_id", Operator.Equals, discordId)
                    .Single();
                if (response == null)
                {
                    Console.WriteLine($"No user found with Discord ID: {discordId}");
                    return null;
                }
                return response;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching user by Discord ID {discordId}: {ex.Message}");
                return null;
            }
        }

        /// <summary>Gets the subscription status of a user by their Discord ID.</summary>
        /// <param name="discordId">The Discord ID of the user to retrieve the subscription status for.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a dictionary with subscription status information or null if not found.</returns>
        public async Task<Dictionary<string, object>?> GetSubStatus(string discordId)
        {
            try
            {
                Users? user = await GetUserByDiscordId(discordId);
                if (user == null)
                {
                    Console.WriteLine("User not found.");
                    return null;
                }
                Dictionary<string, object> subStatus = new()
                {
                {"PlanName", user.PlanName ?? "No plan"},
                {"Credits", user.Credit ?? 0},
                // Add any other subscription info here
            };
                return subStatus;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching subscription status: {ex.Message}");
                return null;
            }
        }

        /// <summary>Retrieves subscription information for a user by their ID.</summary>
        /// <param name="userId">The ID of the user to retrieve subscription information for.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the subscription object or null if not found.</returns>
        public async Task<Users?> GetSubscriptionByUserId(string userId)
        {
            try
            {
                Console.WriteLine("\nAttempting to fetch subscription for user ID: " + userId + "\n");
                // Query to get subscription data for a specific user ID
                Users? response = await supabase!.From<Users>()
                .Select("*")
                .Filter("id", Operator.Equals, userId)
                .Single();
                if (response != null)
                {
                    return response;
                }
                else
                {
                    Console.WriteLine("No subscription found for user ID: " + userId);
                    return null; // Return null if no subscription is found
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while fetching subscription for user ID {userId}: {ex.Message}");
                return null; // Return null in case of error
            }
        }

        /// <summary>Retrieves all templates from the database.</summary>
        /// <returns>A task that represents the asynchronous operation. The task result contains a dictionary of templates keyed by their names, or null if no templates are found.</returns>
        public async Task<Dictionary<string, Template>?> GetTemplatesAsync()
        {
            try
            {
                Postgrest.Responses.ModeledResponse<Template> response = await supabase!.From<Template>().Select("*").Get();
                // Ensure we have models to work with
                if (response.Models != null && response.Models.Count != 0)
                {
                    // Convert the list of templates to a dictionary
                    Dictionary<string, Template> templatesDictionary = response.Models.ToDictionary(template => template.Name, template => template);
                    return templatesDictionary!;
                }
                else
                {
                    Console.WriteLine("No templates found.");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetTemplates: {ex.Message}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");
                return null;
            }
        }

        /// <summary>Adds a new generation record for a user in the database.</summary>
        /// <param name="discordId">The Discord ID of the user for whom the generation is being added.</param>
        /// <returns>A task representing the asynchronous operation of adding the generation record.</returns>
        public async Task AddGenerationAsync(string discordId)
        {
            try
            {
                string? url = Environment.GetEnvironmentVariable("RUNPOD_URL");
                Users? user = await GetUserByDiscordId(discordId);
                if (user == null)
                {
                    Console.WriteLine("User not found.");
                }
                string? userId = user!.Id;
                Generations newGeneration = new()
                {
                    UserId = userId,
                    Batch = 1,
                    //Duration = 1000,
                    Positive = "Example of positive prompt",
                    Negative = "Example of negative aspects",
                    Checkpoint = "StarlightXL.safetensors",
                    CreatedAt = DateTime.UtcNow,
                    ComfyEndpointId = 1,
                    ComfyPromptId = "e639da24-6ce1-46df-93b0-c5f20fe79c3b",
                    Width = 1024,
                    Height = 768,
                    //TemplateId = 123,
                    Status = "saved"
                };
                Postgrest.Responses.ModeledResponse<Generations> response = await supabase!.From<Generations>().Insert(newGeneration);
                if (response == null)
                {
                    Console.WriteLine($"Error inserting new generation: {response!.ResponseMessage}");
                }
                else
                {
                    Console.WriteLine("New generation row added successfully.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error adding generation: {ex.Message}");
            }
        }

        /// <summary>Adds a new image record for a user in the database.</summary>
        /// <param name="userId">The ID of the user for whom the image is being added.</param>
        /// <param name="imageUrl">The URL of the image being added.</param>
        /// <returns>A task representing the asynchronous operation of adding the image record.</returns>
        public async Task AddImage(string userId, string imageUrl)
        {
            try
            {
                if (userId == null)
                {
                    Console.WriteLine("User not found.");
                }
                Images newImage = new()
                {
                    UserId = Guid.Parse(userId!),
                    GenerationId = 815,
                    ImageUrl = imageUrl,
                    CreatedAt = DateTime.UtcNow,
                    LikesCount = 0,
                    IsPublic = false
                };
                Postgrest.Responses.ModeledResponse<Images> response = await supabase!.From<Images>().Insert(newImage);
                if (response == null)
                {
                    Console.WriteLine($"Error inserting new image: {response!.ResponseMessage}");
                }
                else
                {
                    Console.WriteLine("New image row added successfully.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error adding image: {ex.Message}");
            }
        }

        /// <summary>Adds a new template to the database.</summary>
        /// <param name="newTemplate">The template object to add to the database.</param>
        /// <returns>A task representing the asynchronous operation of adding the template.</returns>
        public async Task AddTemplate(Template newTemplate)
        {
            try
            {
                Postgrest.Responses.ModeledResponse<Template> response = await supabase!.From<Template>().Insert(newTemplate);
                if (response == null)
                {
                    Console.WriteLine($"Error inserting new template: {response!.ResponseMessage}");
                }
                else
                {
                    Console.WriteLine("New template row added successfully.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error adding template: {ex.Message}");
            }
        }

        /// <summary>Updates the credit count for a user in the database.</summary>
        /// <param name="userId">The ID of the user whose credits are being updated.</param>
        /// <param name="newCredit">The new credit amount to set.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a boolean indicating whether the update was successful.</returns>
        public async Task<bool> UpdateUserCredit(string userId, int newCredit)
        {
            try
            {
                Postgrest.Responses.ModeledResponse<Users> response = await supabase!.From<Users>()
                                             .Where(x => x.ProviderId == userId)
                                             .Set(x => x.Credit!, newCredit)
                                             .Update();
                // Check the result of the operation
                if (response.ResponseMessage!.IsSuccessStatusCode)
                {
                    return true;
                }
                else
                {
                    Console.WriteLine("Error updating user credit.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating user credit: {ex.Message}");
                return false;
            }
        }

        /// <summary>Uploads an image to Supabase storage and returns its path.</summary>
        /// <param name="userId">The ID of the user uploading the image.</param>
        /// <param name="imagePath">The local file path of the image to upload.</param>
        /// <returns>A task representing the asynchronous operation. The task result contains the path of the uploaded image in storage or null if the upload failed.</returns>
        public async Task<string> UploadImage(string userId, string imagePath)
        {
            try
            {
                var storage = supabase!.Storage;
                string fileName = Path.GetFileName(imagePath);
                string storagePath = $"{userId}/{fileName}";
                byte[] fileContents;
                using (FileStream stream = File.OpenRead(imagePath))
                {
                    using var memoryStream = new MemoryStream();
                    await stream.CopyToAsync(memoryStream);
                    // rename the file to avoid issues with colons in the filename
                    fileContents = memoryStream.ToArray();
                }
                string uploadResponse = await storage.From("generations")
                    .Upload(fileContents, storagePath);
                string url = storage.From("generations")
                    .GetPublicUrl(storagePath);
                Console.WriteLine($"Image uploaded successfully: {url}");
                return storagePath;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error uploading image: {ex.Message}");
                return null!;
            }
        }
    }
}
