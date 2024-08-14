using Discord.Interactions;
using Discord.WebSocket;
using Discord;
using SixLabors.ImageSharp;
using Newtonsoft.Json.Linq;
using Hartsy.Core.SupaBase;
using Hartsy.Core.SupaBase.Models;
using Discord.Rest;

namespace Hartsy.Core.Commands
{
    ///<summary>User Commands</summary>
    public class UserCommands(SupabaseClient supabaseClient, HttpClient httpClient, StableSwarmAPI stableSwarmAPI) : Commands(supabaseClient, httpClient)
    {
        private readonly StableSwarmAPI _stableSwarmAPI = stableSwarmAPI;

        /// <summary>Responds with help information about how to use the bot, including available commands.</summary>
        [SlashCommand("help", "Learn how to use the bot")]
        public async Task HelpCommand()
        {
            try
            {
                EmbedBuilder embed = BuildEmbed(
                    "Hartsy.AI Bot Help",
                    "Hartsy.AI is the premier Stable Diffusion platform for generating images with text directly in Discord." +
                    "\n\nOur custom Discord bot enables users to generate images with text using our fine-tuned templates, " +
                    "choose your favorite images to send to #showcase for community voting, and potentially get featured weekly on the server." +
                    "\n\nDiscover more and subscribe at: https://hartsy.ai",
                    Discord.Color.Blue,
                    "For more information, visit Hartsy.AI",
                    Context.Guild.IconUrl,
                    new Dictionary<string, string>
                    {
                        { "Available Slash Commands", "Checked the pinned messages for a more detailed explanation of these commands." },
                        { "/generate", "Generate an image based on the text you provide, select a template, and optionally add extra prompt information. Example: " +
                        "`/generate_logo text:\"Your Text\" template:\"Template Name\" additions:\"Extra Prompt\"`" },
                        { "/user_info", "Check the status of your subscription and see how many tokens you have left for image generation. Example: `/user_info`" },
                        { "/help", "Shows this help message. Example: `/help`" }
                    }
                );
                await RespondAsync(embed: embed.Build());
            }
            catch (Exception ex)
            {
                await RespondAsync($"An error occurred: {ex.Message}", ephemeral: true);
            }
        }

        /// <summary>Retrieves and displays information about a specified user or the user who invoked the command.</summary>
        /// <param name="targetUser">The user to get information about. If not specified, the invoking user's information is displayed.</param>
        [SlashCommand("user_info", "Get information about the user.")]
        public async Task UserInfoCommand(
        [Summary("user", "The user to get information about.")] SocketGuildUser? targetUser = null)
        {
            try
            {
                SocketGuildUser? contextUser = Context.User as SocketGuildUser;
                SocketUser? user = targetUser ?? contextUser;
                if (targetUser != null && contextUser?.Roles.Any(x => x.Name == "HARTSY Staff ") == true)
                {
                    Embed embed = BuildEmbed(
                        "Restricted Action",
                        "Only admins are allowed to specify a user. Just run the command without specifying a user, and it will automatically show your info.\n\n" +
                        "If you're trying to access specific features and you're not in our database, it's likely because you haven't linked your Discord account to Hartsy.AI. " +
                        "Please log into Hartsy.AI using your Discord account to link it.",
                        Discord.Color.Red,
                        $"This action was attempted by {user?.Username}",
                        user!.GetAvatarUrl() ?? user.GetDefaultAvatarUrl()
                    ).Build();
                    await RespondAsync(embed: embed, ephemeral: true);
                    return;
                }
                Users? userInfo = await _supabaseClient.GetUserByDiscordId(user?.Id.ToString() ?? "");
                Users? subscriptionInfo = userInfo != null ? await _supabaseClient.GetSubscriptionByUserId(userInfo.Id ?? "0") : null;
                if (userInfo != null)
                {
                    Dictionary<string, string> fields = new()
                    {
                        { "Full Name", userInfo.Name ?? "N/A" },
                        { "Email", userInfo.Email ?? "N/A" },
                        { "Credit Limit", userInfo.Credit?.ToString() ?? "N/A" },
                        { "Likes", userInfo.Likes?.ToString() ?? "0" }
                    };
                    if (subscriptionInfo != null)
                    {
                        fields.Add("Subscription Status", subscriptionInfo.PlanName ?? "N/A");
                    }
                    Embed embed = BuildEmbed(
                        $"{userInfo.Username}'s Information",
                        null!,
                        Discord.Color.Blue,
                        "",
                        user!.GetAvatarUrl() ?? user.GetDefaultAvatarUrl(),
                        fields
                    ).Build();
                    await RespondAsync(embed: embed, ephemeral: true);
                }
                else
                {
                    Embed embed = BuildEmbed(
                        "User Information Not Found",
                        "We couldn't find your information in our database. This usually means that your Discord account is not linked with Hartsy.AI.\n\n" +
                        "To link your account, please visit [Hartsy.AI](https://hartsy.ai) and log in using your Discord credentials. " +
                        "This process will sync your account with our services, allowing you full access to the features available.",
                        Discord.Color.Red,
                        "",
                        user!.GetAvatarUrl() ?? user!.GetDefaultAvatarUrl()
                    ).Build();
                    await RespondAsync(embed: embed, ephemeral: true);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in UserInfoCommand: {ex.Message}");
                SocketUser user = Context.User;
                Embed embed = ErrorEmbed(user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl()
                ).Build();
                await RespondAsync(embed: embed, ephemeral: true);
            }
        }

        /// <summary>Generates an image based on the provided text and template, consuming GPUTs in the process.</summary>
        /// <param name="text">The text to include in the image.</param>
        /// <param name="template">The template to use for the image generation.</param>
        /// <param name="description">Additional details to add to the prompt.</param>
        [SlashCommand("generate", "Generate an image. THIS WILL USE GPUTs")]
        public async Task ImageGenerationCommand(
            [Summary("text", "The text you want to appear in the image.")] string text,
            [Summary("template", "Choose a template for the image.")]
            [Autocomplete(typeof(TemplateAutocompleteHandler))] string template,
            [Summary("additional_details", "Describe other aspects to add to the prompt.")] string? description = null)
        {
            await DeferAsync(ephemeral: true);
            SocketGuildUser user = (SocketGuildUser)Context.User;
            // if additional details is over 120 characters long, return an error message
            if (description != null && description.Length > 120 || text.Length > 25)
            {
                Embed embed = BuildEmbed(
                    "Input Length Error",
                    "Your input text or description exceeds the allowed character limit. Please adhere to the following constraints:" +
                    "\n- Text must be 25 characters or less." +
                    "\n- Description must be 120 characters or less." +
                    "\n\nAttempting to bypass or manipulate the system by tricking or 'jailbreaking' the AI is strictly prohibited " +
                    "and against the community guidelines. Violations may result in actions taken against your account.",
                    Discord.Color.Red,
                    "This is a reminder to use the system responsibly and ethically.",
                    user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl()
                ).Build();

                await FollowupAsync(embed: embed, ephemeral: true);
                return;
            }
            Users? userInfo = await _supabaseClient.GetUserByDiscordId(user?.Id.ToString() ?? "");
            if (userInfo == null)
            {
                await HandleSubscriptionFailure(Context);
                return;
            }
            string subStatus = userInfo.PlanName ?? "Member";
            int credits = userInfo.Credit ?? 0;
            // Check if the user has a valid subscription and enough credits
            if (subStatus != null && userInfo.Credit > 0)
            {
                int newCredit = credits - 1;
                // Attempt to update user credit then check if the update was successful before proceeding
                bool isCreditUpdated = await _supabaseClient.UpdateUserCredit(user?.Id.ToString() ?? "", newCredit);
                if (!isCreditUpdated)
                {
                    Console.WriteLine("Error updating user credits. Aborting image generation.");
                    await HandleSubscriptionFailure(Context);
                    return;
                }
                Dictionary<string, string> fields = new()
                {
                    {
                        "Generate Command",
                        "This command allows you to generate images based on the text and template you provide. Each generation will use one GPUT from your account." +
                        "\n\nGo to [Hartsy.ai](https://hartsy.ai) to check sub status or add GPUTs"
                    }
                };
                Embed embed = BuildEmbed(
                    "Image Generation",
                    $"You have {credits} GPUT. You will have {newCredit} GPUT after this image is generated.",
                    Discord.Color.Gold,
                    "Ask an admin for ways to earn FREE GPUTs!",
                    user!.GetAvatarUrl() ?? user!.GetDefaultAvatarUrl(),
                    fields
                ).Build();
                await FollowupAsync(embed: embed, ephemeral: true);
                // Add the role to the user if they do not have it
                await AddSubRole(user, subStatus);
                SocketTextChannel? channel = Context.Channel as SocketTextChannel;
                // Proceed with image generation
                await GenerateFromTemplate(text, template, channel, user, description);
            }
            else
            {
                // Handle the lack of subscription or insufficient credits
                Console.WriteLine("Warning: User does not have a valid subscription or enough credits.");
                await HandleSubscriptionFailure(Context);
            }
        }

        /// <summary>Adds a subscription role to the user based on their subscription status.</summary>
        /// <param name="user">The user to add the subscription role to.</param>
        /// <param name="subStatus">The subscription status of the user.</param>
        public static async Task AddSubRole(SocketGuildUser? user, string? subStatus)
        {
            SocketRole? subRole = user?.Guild.Roles.FirstOrDefault(role => role.Name.Equals(subStatus, StringComparison.OrdinalIgnoreCase));
            if (subRole != null && !user!.Roles.Contains(subRole))
            {
                await user.AddRoleAsync(subRole);
            }
        }

        /// <summary>Notifies the user of subscription failure due to lack of valid subscription or insufficient credits.</summary>
        /// <param name="context">The interaction context to respond to.</param>
        public static async Task HandleSubscriptionFailure(IInteractionContext context)
        {
            IUser user = context.User;
            Embed embed = BuildEmbed(
                "Access Denied",
                $"{user.Mention} You either do not have a valid subscription, you have insufficient credits, or You have not properly linked your account. " +
                "Please visit [Hartsy.AI](https://hartsy.ai) to check sub status, purchase more credits, or login with Discord.",
                Discord.Color.Red,
                "Speak with an admin for more information",
                user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl()
            ).Build();
            MessageComponent button = new ComponentBuilder()
                .WithButton("Click to Subscribe or Add Credits", null, ButtonStyle.Link, url: "https://hartsy.ai")
                .Build();
            try
            {
                await context.Interaction.FollowupAsync(embed: embed, components: button, ephemeral: true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in HandleSubscriptionFailure: {ex.Message}");
                await context.Channel.SendMessageAsync(embed: embed, components: button);
            }
        }

        /// <summary>Generates an image from a given text and template, and posts it in the specified channel.</summary>
        /// <param name="text">The text to generate the image from.</param>
        /// <param name="templateName">The template to use for generating the image.</param>
        /// <param name="channel">The channel to post the generated image in.</param>
        /// <param name="user">The user who initiated the image generation.</param>
        /// <param name="description">Additional description to refine the image generation.</param>
        /// <param name="initimage">Initial image for image-to-image generation, if applicable.</param>
        public async Task GenerateFromTemplate(string? text, string templateName, SocketTextChannel? channel,
            SocketGuildUser? user, string? description = null, string? initimage = null)
        {
            string prompt = string.Empty;
            string TemplateInfo = string.Empty;
            string imageUrl = string.Empty;
            string username = user!.Username;
            Embed? embed = null;
            Dictionary<string, object>? payload = null;
            Dictionary<string, Template>? templates = await _supabaseClient.GetTemplatesAsync();
            if (templates != null && templates.TryGetValue(templateName, out Template? templateDetails))
            {
                string positiveText = templateDetails.Positive?.Replace("__TEXT_REPLACE__", text) ?? "";
                prompt = $"{positiveText}, {description}";
                TemplateInfo = templateDetails?.Description ?? "";
                imageUrl = templateDetails?.ImageUrl ?? "";
                embed = new EmbedBuilder()
                    .WithAuthor(user)
                    .WithTitle("Thank you for generating your image with Hartsy.AI")
                    .WithDescription($"Generating an image described by **{username}**\n\n" +
                                     $"**Template Used:** {templateName}\n\n`{TemplateInfo}`\n\n")
                    .WithImageUrl("https://github.com/kalebbroo/Hartsy/blob/main/images/wait.gif?raw=true")
                    .WithThumbnailUrl(imageUrl)
                    .WithColor(Discord.Color.Red)
                    .WithCurrentTimestamp()
                    .Build();
                JArray? lorasData = templateDetails!.Loras;
                // Initialize variables to hold the comma-separated names and weights
                string loraname = "";
                string loraweight = "";
                if (lorasData != null)
                {
                    // Project 'name' and 'weight' into separate lists
                    List<string> loraNames = lorasData.Select(lora => ((JObject)lora)["name"]!.ToString()).ToList();
                    List<string> loraWeights = lorasData.Select(lora => ((JObject)lora)["weight"]!.ToString()).ToList();
                    // Join the lists into comma-separated strings
                    loraname = string.Join(", ", loraNames);
                    loraweight = string.Join(", ", loraWeights);
                }
                // Construct the payload
                payload = new Dictionary<string, object>
                    {
                        {"prompt", prompt},
                        {"negativeprompt", templateDetails.Negative ?? ""},
                        {"images", 1},
                        {"batchsize", 4},
                        {"donotsave", true},
                        {"model", templateDetails.Checkpoint ?? ""},
                        {"loras", loraname},
                        {"loraweights", loraweight},
                        {"width", 1024},
                        {"height", 768},
                        {"cfgscale", templateDetails.Cfg ?? 6.5},
                        {"steps", templateDetails.Steps ?? 28},
                        {"seed", templateDetails.Seed ?? -1},
                        {"sampler", templateDetails.Sampler ?? "dpmpp_3m_sde_gpu"},
                        {"scheduler", templateDetails.Scheduler ?? "karras"},
                        {"initimage", initimage!},
                        {"init_image_creativity", 0.7},
                    };
            }
            RestUserMessage previewMsg = await channel!.SendMessageAsync(embed: embed);
            ulong messageId = previewMsg.Id;
            await foreach (var (image, isFinal) in _stableSwarmAPI.GetImages(payload!, username, messageId))
            {
                if (image == null)
                {
                    continue;
                }
                using MemoryStream ms = new();
                image.SaveAsJpeg(ms);
                ms.Position = 0;
                FileAttachment file = new(ms, "image_grid.jpeg");
                EmbedBuilder updatedEmbed = previewMsg.Embeds.FirstOrDefault()?.ToEmbedBuilder() ?? new EmbedBuilder();
                updatedEmbed.WithImageUrl($"attachment://image_grid.jpeg");
                updatedEmbed.WithColor(Discord.Color.Red);
                if (isFinal)
                {
                    updatedEmbed.AddField("Text", text)
                    .AddField("Description", description)
                    .AddField("Template", templateName)
                    .AddField("TemplateInfo", TemplateInfo)
                    .WithFooter("Click Save to Gallery button to see the fullsize image")
                    .WithColor(Discord.Color.Green);
                    await previewMsg.ModifyAsync(m =>
                    {
                        m.Embed = updatedEmbed.Build();
                        m.Attachments = new[] { file };
                        m.Components = new Optional<MessageComponent>(GenerateComponents(user.Id, "template").Build()); // Add generation type to custom ID
                    });
                    break; // Exit the loop after handling the final image
                }
                else
                {
                    await previewMsg.ModifyAsync(m =>
                    {
                        m.Embed = updatedEmbed.Build();
                        m.Attachments = new[] { file };
                    });
                }
            }
        }

        /// <summary>Generates a set of interactive components (buttons) for message responses, allowing users to interact with the bot's features.</summary>
        /// <param name="userId">The user ID to associate with the components, used for identifying the user in interaction handlers.</param>
        /// <returns>A ComponentBuilder containing buttons for various bot functionalities like regenerate, showcase, report, and saving images.</returns>
        private static ComponentBuilder GenerateComponents(ulong userId, string type = "")
        {
            string customId = $"regenerate:{userId}:{type}";
            string deleteCustomId = $"delete:{userId}";
            string showcaseCustomId = $"choose_image:showcase:{userId}";
            string reportCustomId = $"report:{userId}";
            string i2iCustomId = $"choose_image:i2i:{userId}";
            string saveCustomId = $"choose_image:save:{userId}";
            string gifCustomId = $"choose_image:gif:{userId}";
            return new ComponentBuilder()
                .WithButton("*Regenerate*", customId, ButtonStyle.Success)
                .WithButton("Add to Showcase!", showcaseCustomId, ButtonStyle.Primary)
                .WithButton("Report", reportCustomId, ButtonStyle.Secondary, emote: new Emoji("\u26A0")) // ⚠
                .WithButton(" ", deleteCustomId, ButtonStyle.Danger, emote: new Emoji("\uD83D\uDDD1")) // 🗑
                .WithButton("Image2Image", i2iCustomId, ButtonStyle.Secondary, row: 1)
                .WithButton("Add To My Gallery", saveCustomId, ButtonStyle.Primary, row: 1)
                .WithButton("Generate GIF", gifCustomId, ButtonStyle.Success, row: 1);
        }

        /// <summary>Generates an image based on the provided prompt using the Flux Schnell model, consuming GPUTs in the process.</summary>
        /// <param name="prompt">The text to inspire the image creation.</param>
        /// <param name="aspect">The aspect ratio for the generated image.</param>
        /// <param name="description">Additional details to enhance the prompt.</param>
        [SlashCommand("flux", "Images with FLUX! THIS WILL USE GPUTs")]
        public async Task FluxGenerationCommand(
            [Summary("Prompt", "What do you want to generate?")] string prompt,
            [Summary("Aspect", "Choose the aspect ratio")]
            [Choice("1:1 (1024x1024)", "1:1")]
            [Choice("4:3 (1024x768)", "4:3")]
            [Choice("3:2 (1216x832)", "3:2")]
            [Choice("8:5 (1280x800)", "8:5")]
            [Choice("16:9 (1344x768)", "16:9")]
            [Choice("21:9 (1536x640)", "21:9")]
            [Choice("3:4 (768x1024)", "3:4")]
            [Choice("2:3 (832x1216)", "2:3")]
            [Choice("5:8 (800x1280)", "5:8")]
            [Choice("9:16 (768x1344)", "9:16")]
            [Choice("9:21 (640x1536)", "9:21")]
            string aspect)
        {
            await DeferAsync(ephemeral: true);
            SocketGuildUser user = (SocketGuildUser)Context.User;
            Users? userInfo = await _supabaseClient.GetUserByDiscordId(user?.Id.ToString() ?? "");
            if (userInfo == null)
            {
                await HandleSubscriptionFailure(Context);
                return;
            }
            string subStatus = userInfo.PlanName ?? "Member";
            int credits = userInfo.Credit ?? 0;
            // Check if the user has a valid subscription and enough credits
            if (subStatus != null && userInfo.Credit > 0)
            {
                int newCredit = credits - 1;
                // Attempt to update user credit then check if the update was successful before proceeding
                bool isCreditUpdated = await _supabaseClient.UpdateUserCredit(user?.Id.ToString() ?? "", newCredit);
                if (!isCreditUpdated)
                {
                    Console.WriteLine("Error updating user credits. Aborting image generation.");
                    await HandleSubscriptionFailure(Context);
                    return;
                }
                Dictionary<string, string> fields = new()
                {
                    {
                        "Flux Generation Command",
                        "This command allows you to create images based on the prompt you provide using the Flux Schnell model. Each generation will consume one GPUT from your account." +
                        "\n\nGo to [Hartsy.ai](https://hartsy.ai) to check your subscription status or add more GPUTs."
                    }
                };
                Embed embed = BuildEmbed(
                    "Flux Schnell Image Generation",
                    $"You have {credits} GPUT. You will have {newCredit} GPUT after this image is generated.",
                    Discord.Color.Gold,
                    "Ask an admin for ways to earn FREE GPUTs!",
                    user!.GetAvatarUrl() ?? user!.GetDefaultAvatarUrl(),
                    fields
                    ).Build();
                await FollowupAsync(embed: embed, ephemeral: true);
                // Add the role to the user if they do not have it
                await AddSubRole(user, subStatus);
                SocketTextChannel? channel = Context.Channel as SocketTextChannel;
                // Proceed with image generation
                await GenerateForFlux(prompt, aspect, channel, user);
            }
            else
            {
                // Handle the lack of subscription or insufficient credits
                Console.WriteLine("Warning: User does not have a valid subscription or enough credits.");
                await HandleSubscriptionFailure(Context);
            }
        }

        /// <summary>Generates images from flux.</summary>
        public async Task GenerateForFlux(string prompt, string aspect, SocketTextChannel? channel, SocketGuildUser? user)
        {
            string username = user!.Username;
            (int width, int height) = ConvertAspectRatio(aspect);
            Embed? embed = null;
            Dictionary<string, object>? payload = null;
            embed = new EmbedBuilder()
                .WithAuthor(user)
                .WithTitle("Thank you for generating your image with Hartsy.AI")
                .WithDescription($"Generating an image based on the prompt provided by **{username}**\n\n" +
                                 $"**Model Used:** Flux Schnell\n\n")
                .WithImageUrl("https://github.com/kalebbroo/Hartsy/blob/main/images/wait.gif?raw=true")
                .WithThumbnailUrl("https://r2.fluxaiimagegenerator.com/static/schnell.webp")
                .WithColor(Discord.Color.Red)
                .WithCurrentTimestamp()
                .Build();

            // Construct the payload
            payload = new Dictionary<string, object>
            {
                {"prompt", prompt},
                {"negativeprompt", ""},
                {"images", 1},
                {"batchsize", 4},
                {"donotsave", true},
                {"model", "flux1-schnell-bnb-nf4"},
                {"width", width},
                {"height", height},
                {"cfgscale", 1},
                {"steps", 4},
                {"seed", -1},
                {"exactbackendid", 3 },
            };
            RestUserMessage previewMsg = await channel!.SendMessageAsync(embed: embed);
            ulong messageId = previewMsg.Id;
            await foreach (var (image, isFinal) in _stableSwarmAPI.GetImages(payload!, username, messageId))
            {
                if (image == null)
                {
                    continue;
                }
                using MemoryStream ms = new();
                image.SaveAsJpeg(ms);
                ms.Position = 0;
                FileAttachment file = new(ms, "image_grid.jpeg");
                EmbedBuilder updatedEmbed = previewMsg.Embeds.FirstOrDefault()?.ToEmbedBuilder() ?? new EmbedBuilder();
                updatedEmbed.WithImageUrl($"attachment://image_grid.jpeg");
                updatedEmbed.WithColor(Discord.Color.Red);
                if (isFinal)
                {
                    updatedEmbed.AddField("Prompt", prompt, true)
                        .AddField("AspectRatio", aspect, true)
                        .WithFooter("Click Save to Gallery button to see the full-size image")
                        .WithColor(Discord.Color.Green);
                    await previewMsg.ModifyAsync(m =>
                    {
                        m.Embed = updatedEmbed.Build();
                        m.Attachments = new[] { file };
                        m.Components = new Optional<MessageComponent>(GenerateComponents(user.Id, "flux").Build()); // Add generation type to custom ID
                    });
                    break; // Exit the loop after handling the final image
                }
                else
                {
                    await previewMsg.ModifyAsync(m =>
                    {
                        m.Embed = updatedEmbed.Build();
                        m.Attachments = new[] { file };
                    });
                }
            }
        }

        private (int width, int height) ConvertAspectRatio(string aspect)
        {
            return aspect switch
            {
                "1:1" => (1024, 1024),
                "4:3" => (1152, 896),
                "3:2" => (1216, 832),
                "8:5" => (1216, 768),
                "16:9" => (1344, 768),
                "21:9" => (1536, 640),
                "3:4" => (896, 1152),
                "2:3" => (832, 1216),
                "5:8" => (768, 1216),
                "9:16" => (768, 1344),
                "9:21" => (640, 1536),
                _ => (1024, 768) // Default if aspect ratio doesn't match
            };
        }
    }
}
