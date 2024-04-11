using Discord.Interactions;
using Discord.WebSocket;
using Discord;
using SixLabors.ImageSharp;
using System.Text.Json;
using Supabase.Gotrue;
using Supabase.Interfaces;
using Newtonsoft.Json.Linq;

namespace Hartsy.Core
{
    public class Commands : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly SupabaseClient _supabaseClient;
        private readonly StableSwarmAPI _stableSwarmAPI;

        public Commands()
        {
            _supabaseClient = new SupabaseClient();
            _stableSwarmAPI = new StableSwarmAPI();
        }

        /// <summary>Responds with help information about how to use the bot, including available commands.</summary>
        [SlashCommand("help", "Learn how to use the bot")]
        public async Task HelpCommand()
        {
            try
            {
                var embed = new EmbedBuilder()
                    .WithTitle("Hartsy.AI Bot Help")
                    .WithThumbnailUrl(Context.Guild.IconUrl)
                    .WithDescription("Hartsy.AI is the premier Stable Diffusion platform for generating images with text directly in Discord. " +
                    "\n\nOur custom Discord bot enables users to generate images with text using our fine-tuned templates, choose your favorite " +
                    "images to send to #showcase for community voting, and potentially get featured weekly on the server. \n\nDiscover more and subscribe at: https://hartsy.ai")
                    .AddField("Available Slash Commands", "Checked the pinned messages for a more detailed explanation of these commands.", false)
                    .AddField("/generate", "Generate an image based on the text you provide, select a template, and optionally add extra prompt " +
                    "information. Example: `/generate_logo text:\"Your Text\" template:\"Template Name\" additions:\"Extra Prompt\"`", false)
                    .AddField("/user_info", "Check the status of your subscription and see how many tokens you have left for image generation. Example: `/user_info`", false)
                    .AddField("/help", "Shows this help message. Example: `/help`", false)
                    .WithColor(Discord.Color.Blue)
                    .WithFooter(footer => footer.Text = "For more information, visit Hartsy.AI")
                    .WithCurrentTimestamp();

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
                IUser? user = targetUser ?? (Context.User as SocketGuildUser);
                SocketGuildUser? contextUser = Context.User as SocketGuildUser;
                if (targetUser != null && contextUser?.Roles.Any(x => x.Name == "HARTSY Staff ") == true)
                {
                    var embed = new EmbedBuilder()
                        .WithTitle("Restricted Action")
                        .WithDescription("Only admins are allowed to specify a user. " +
                                         "Just run the command without specifying a user, and it will automatically show your info.\n\n" +
                                         "If you're trying to access specific features and you're not in our database, " +
                                         "it's likely because you haven't linked your Discord account to Hartsy.AI. " +
                                         "Please log into Hartsy.AI using your Discord account to link it.")
                        .WithColor(Discord.Color.Red)
                        .WithThumbnailUrl(Context.Client.CurrentUser.GetAvatarUrl() ?? Context.Client.CurrentUser.GetDefaultAvatarUrl())
                        .WithFooter("This action was attempted by " + user?.Username)
                        .WithTimestamp(DateTimeOffset.Now)
                        .Build();

                    await RespondAsync(embed: embed, ephemeral: true);
                    return;
                }

                var userInfo = await _supabaseClient.GetUserByDiscordId(user?.Id.ToString() ?? "");
                var subscriptionInfo = userInfo != null ? await _supabaseClient.GetSubscriptionByUserId(userInfo.Id ?? "0") : null;

                if (userInfo != null)
                {
                    var embed = new EmbedBuilder()
                        .WithTitle($"{userInfo.Username}'s Information")
                        .WithThumbnailUrl(userInfo.Avatar_URL ?? user?.GetAvatarUrl() ?? user?.GetDefaultAvatarUrl())
                        .AddField("Full Name", userInfo.Name ?? "N/A", true)
                        .AddField("Email", userInfo.Email ?? "N/A", true)
                        .AddField("Credit Limit", userInfo.Credit?.ToString() ?? "N/A", true)
                        .AddField("Likes", userInfo.Likes?.ToString() ?? "0", true);

                    if (subscriptionInfo != null)
                    {
                        embed.AddField("Subscription Status", subscriptionInfo.PlanName ?? "N/A", true);
                    }

                    embed.WithColor(Discord.Color.Blue);
                    await RespondAsync(embed: embed.Build(), ephemeral: true);
                }
                else
                {
                    var embed = new EmbedBuilder()
                        .WithTitle("User Information Not Found")
                        .WithDescription("We couldn't find your information in our database. This usually means that your Discord account is not linked with Hartsy.AI.\n\n" +
                                         "To link your account, please visit [Hartsy.AI](https://hartsy.ai) and log in using your Discord credentials. " +
                                         "This process will sync your account with our services, allowing you full access to the features available.")
                        .WithColor(Discord.Color.Red)
                        .WithThumbnailUrl(Context.Client.CurrentUser.GetAvatarUrl() ?? Context.Client.CurrentUser.GetDefaultAvatarUrl())
                        .WithTimestamp(DateTimeOffset.Now)
                        .Build();

                    await RespondAsync(embed: embed, ephemeral: true);
                }
            }
            catch (Exception ex)
            {
                await RespondAsync($"An error occurred: {ex.Message}", ephemeral: true);
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
            // if additional details is over 15 characters long, return an error message
            if ((description != null && description.Length > 120) || text.Length > 25)
            {
                var embed = new EmbedBuilder()
                    .WithTitle("Input Length Error")
                    .WithDescription("Your input text or description exceeds the allowed character limit. Please adhere to the following constraints:" +
                                     "\n- Text must be 25 characters or less." +
                                     "\n- Description must be 120 characters or less." +
                                     "\n\nAttempting to bypass or manipulate the system by tricking or 'jailbreaking' the AI is strictly prohibited " +
                                     "and against the community guidelines. Violations may result in actions taken against your account.")
                    .WithColor(Discord.Color.Red)
                    .WithFooter("This is a reminder to use the system responsibly and ethically.")
                    .WithCurrentTimestamp()
                    .Build();

                await FollowupAsync(embed: embed, ephemeral: true);
                return;
            }
            SocketGuildUser user = (SocketGuildUser)Context.User;
            var userInfo = await _supabaseClient.GetUserByDiscordId(user?.Id.ToString() ?? "");
            if (userInfo == null)
            {
                await HandleSubscriptionFailure(Context);
                return;
            }
            var subStatus = userInfo.PlanName ?? "Member";
            var credits = userInfo.Credit ?? 0;

            // Check if the user has a valid subscription and enough credits
            if (subStatus != null && userInfo.Credit > 0)
            {
                await _supabaseClient.UpdateUserCredit(user?.Id.ToString() ?? "", credits);
                var embed = new EmbedBuilder()
                    .WithTitle("Image Generation")
                    .WithDescription($"You have {credits} GPUT. You will have {credits - 1} GPUT after this image is generated.")
                    .AddField("Generate Command", "This command allows you to generate images based on the text and template you provide. " +
                    "Each generation will use one GPUT from your account.")
                    .WithColor(Discord.Color.Gold)
                    .WithCurrentTimestamp()
                    .Build();

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
            var embed = new EmbedBuilder()
                .WithTitle("Access Denied")
                .WithDescription($"{user.Mention} You either do not have a valid subscription, you have insufficient credits, or You have not properly linked your account. " +
                "Please visit Hartsy.AI to manage your subscription, purchase more credits, or login with Discord.")
                .WithColor(Discord.Color.Red)
                .WithTimestamp(DateTimeOffset.Now)
                .Build();

            var button = new ComponentBuilder()
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

            var templates = await _supabaseClient.GetTemplatesAsync();
            if (templates != null && templates.TryGetValue(templateName, out var templateDetails))
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

                // Assuming `loras` is a JArray
                JArray? lorasData = templateDetails!.Loras;

                // Initialize variables to hold the comma-separated names and weights
                string loraname = "";
                string loraweight = "";

                if (lorasData != null)
                {
                    // Project 'name' and 'weight' into separate lists
                    var loraNames = lorasData.Select(lora => ((JObject)lora)["name"]!.ToString()).ToList();
                    var loraWeights = lorasData.Select(lora => ((JObject)lora)["weight"]!.ToString()).ToList();


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
                        {"loras", loraname ?? "an0tha0ne.safetensors"},
                        {"loraweights", 0.9}, // loraweight ?? 0.9
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
            var previewMsg = await channel!.SendMessageAsync(embed: embed);
            ulong messageId = previewMsg.Id;

            await foreach (var (image, isFinal) in _stableSwarmAPI.GetImages(payload!, username, messageId))
            {
                if (image == null)
                {
                    continue;
                }
                using var ms = new MemoryStream();
                image.SaveAsJpeg(ms);
                ms.Position = 0;
                FileAttachment file = new(ms, "image_grid.jpeg");

                EmbedBuilder updatedEmbed = previewMsg.Embeds.FirstOrDefault()?.ToEmbedBuilder() ?? new EmbedBuilder();
                updatedEmbed.WithImageUrl($"attachment://image_grid.jpeg");
                updatedEmbed.WithColor(Discord.Color.Red);

                if (isFinal)
                {
                    updatedEmbed.WithDescription($"Generated an image for **{username}**\n\n**Text:** {text}\n\n**Extra Description:** {description}" +
                        $"\n\n**Template Used:** {templateName}\n\n`{TemplateInfo}`\n\n**Click Save to Gallery button to see the fullsize image**");
                    updatedEmbed.WithFooter("Click Save to Gallery button to see the fullsize image");
                    updatedEmbed.WithColor(Discord.Color.Green);

                    await previewMsg.ModifyAsync(m =>
                    {
                        m.Embed = updatedEmbed.Build();
                        m.Attachments = new[] { file };
                        m.Components = new Optional<MessageComponent>(GenerateComponents(user.Id).Build());
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
        private static ComponentBuilder GenerateComponents(ulong userId)
        {
            string customId = $"regenerate:{userId}";
            string deleteCustomId = $"delete:{userId}";
            string showcaseCustomId = $"choose_image:showcase:{userId}";
            string reportCustomId = $"report:{userId}";
            string i2iCustomId = $"choose_image:i2i:{userId}";
            string saveCustomId = $"choose_image:save:{userId}";
            return new ComponentBuilder()
                .WithButton("Regenerate", customId, ButtonStyle.Success)
                .WithButton("Add to Showcase", showcaseCustomId, ButtonStyle.Primary)
                .WithButton("Report", reportCustomId, ButtonStyle.Secondary, emote: new Emoji("\u26A0")) // ⚠
                .WithButton(" ", deleteCustomId, ButtonStyle.Danger, emote: new Emoji("\uD83D\uDDD1")) // 🗑
                .WithButton("Image2Image", i2iCustomId, ButtonStyle.Secondary, row: 1)
                .WithButton("Save To Gallery", saveCustomId, ButtonStyle.Secondary, row: 1);
        }
    }
}
