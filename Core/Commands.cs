using Discord.Interactions;
using Discord.WebSocket;
using Discord;
using Hartsy.Core;
using System.Drawing;

namespace HartsyBot.Core
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
                // If something goes wrong, send an error message
                await RespondAsync($"An error occurred: {ex.Message}", ephemeral: true);
            }
        }

        [SlashCommand("user_info", "Get information about the user.")]
        public async Task UserInfoCommand(
        [Summary("user", "The user to get information about.")] SocketGuildUser? targetUser = null)
        {
            try
            {
                var user = targetUser ?? (Context.User as SocketGuildUser);
                var contextUser = Context.User as SocketGuildUser;
                if (targetUser != null && contextUser.Roles.Any(x => x.Name == "HARTSY Staff "))
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
                        .WithFooter("This action was attempted by " + user.Username)
                        .WithTimestamp(DateTimeOffset.Now)
                        .Build();

                    await RespondAsync(embed: embed, ephemeral: true);
                    return;
                }

                var userInfo = await _supabaseClient.GetUserByDiscordId(user.Id.ToString());
                var subscriptionInfo = userInfo != null ? await _supabaseClient.GetSubscriptionByUserId(userInfo.Id ?? "0") : null;

                if (userInfo != null)
                {
                    var embed = new EmbedBuilder()
                        .WithTitle($"{userInfo.Username}'s Information")
                        .WithThumbnailUrl(userInfo.Avatar_URL ?? user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl())
                        .AddField("Full Name", userInfo.Name ?? "N/A", true)
                        .AddField("Email", userInfo.Email ?? "N/A", true)
                        //.AddField("Subscription Level", subscriptionInfo != null ? subscriptionInfo.PlanName ?? "Active" : "No Subscription", true)
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

        [SlashCommand("generate", "Generate an image. THIS WILL USE GPUTs")]
        public async Task ImageGenerationCommand(
            [Summary("text", "The text you want to appear in the image.")] string text,
            [Summary("template", "Choose a template for the image.")]
            [Autocomplete(typeof(TemplateAutocompleteHandler))] string template,
            [Summary("additional_details", "Describe other aspects to add to the prompt.")] string description = null)
        {
            await DeferAsync(ephemeral: true);
            // if additional details is over 15 characters long, return an error message
            if ((description != null && description.Length > 60) || text.Length > 25)
            {
                var embed = new EmbedBuilder()
                    .WithTitle("Input Length Error")
                    .WithDescription("Your input text or description exceeds the allowed character limit. Please adhere to the following constraints:" +
                                     "\n- Text must be 25 characters or less." +
                                     "\n- Description must be 60 characters or less." +
                                     "\n\nAttempting to bypass or manipulate the system by tricking or 'jailbreaking' the AI is strictly prohibited " +
                                     "and against the community guidelines. Violations may result in actions taken against your account.")
                    .WithColor(Discord.Color.Red)
                    .WithFooter("This is a reminder to use the system responsibly and ethically.")
                    .WithCurrentTimestamp()
                    .Build();

                await FollowupAsync(embed: embed, ephemeral: true);
                return;
            }
            var user = Context.User as SocketGuildUser;
            var userInfo = await _supabaseClient.GetUserByDiscordId(user.Id.ToString());
            if (userInfo == null)
            {
                await HandleSubscriptionFailure(Context);
                return;
            }
            var subStatus = userInfo.PlanName ?? "Member";
            Console.WriteLine($"User: {user.Username}, Subscription: {subStatus}");
            var credits = userInfo.Credit ?? 0;
            Console.WriteLine("Credits: " + credits);

            // Check if the user has a valid subscription and enough credits
            if (subStatus != null && userInfo.Credit > 0)
            {
                bool creditUpdated = await _supabaseClient.UpdateUserCredit(user.Id.ToString(), credits);
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

                // Proceed with image generation
                
                await GenerateImageWithCredits(Context, text, template, description);
            }
            else
            {
                // Handle the lack of subscription or insufficient credits
                Console.WriteLine("Error in ImageGenerationCommand: User does not have a valid subscription or enough credits.");
                //await FollowupAsync("You do not have a valid subscription or enough credits to generate an image.", ephemeral: true);
                await HandleSubscriptionFailure(Context);
            }
        }

        public async Task AddSubRole(SocketGuildUser user, string subStatus)
        {
            var subRole = user.Guild.Roles.FirstOrDefault(role => role.Name.Equals(subStatus, StringComparison.OrdinalIgnoreCase));
            if (subRole != null && !user.Roles.Contains(subRole))
            {
                await user.AddRoleAsync(subRole);
            }
        }

        public async Task GenerateImageWithCredits(SocketInteractionContext interaction, string text, string template, string description)
        {

            var user = interaction.User as SocketGuildUser;
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(template))
            {
                Console.WriteLine("Text, template, or description is null or empty in GenerateImageWithCredits.");
                Console.WriteLine($"Text: {text}, Template: {template}");
                return;
            }
            var channel = interaction.Channel as SocketTextChannel;
            if (channel == null)
            {
                Console.WriteLine("Channel is null before calling GenerateFromTemplate.");
                return;
            }
            if (user == null)
            {
                Console.WriteLine("User is null before calling GenerateFromTemplate.");
                return;
            }
            await GenerateFromTemplate(text, template, channel, user, description);
        }

        public async Task HandleSubscriptionFailure(IInteractionContext context)
        {
            var user = context.User;
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

        public async Task GenerateFromTemplate(string text, string template, SocketTextChannel channel, SocketGuildUser user, string description = null)
        {
            string prompt = string.Empty;
            string TemplateInfo = string.Empty;
            string imageUrl = string.Empty;
            string projectRoot = Directory.GetParent(Environment.CurrentDirectory).Parent.Parent.FullName;
            string waitImageFilePath = Path.Combine(projectRoot, "images", "wait.gif");
            // Fetch the templates from the database
            var templates = await _supabaseClient.GetTemplatesAsync();
            if (templates != null && templates.TryGetValue(template, out var templateDetails))
            {
                string positiveText = templateDetails.Positive.Replace("__TEXT_REPLACE__", text);
                prompt = $"{positiveText}, {description}";
                TemplateInfo = templateDetails.Description;
                imageUrl = templateDetails.ImageUrl;
            }

            var username = user.Username;

            // Create a placeholder embed
            var embed = new EmbedBuilder()
                .WithAuthor(user)
                .WithTitle("Thank you for generating your image with Hartsy.AI")
                .WithDescription($"Generating an image described by **{username}**\n\n" +
                                 $"**Template Used:** {template}\n\n`{TemplateInfo}`\n\n")
                .WithImageUrl($"attachment://wait.gif")
                .WithThumbnailUrl($"{imageUrl}")
                .WithColor(Discord.Color.DarkerGrey)
                .WithCurrentTimestamp()
                .Build();

            var previewMsg = await channel.SendFileAsync(waitImageFilePath, embed: embed);

            // Generate the image and update the embed with each received image
            await foreach (var (imageBase64, isFinal) in _stableSwarmAPI.GenerateImage(prompt))
            {
                Console.WriteLine($"Received image. Final: {isFinal}");
                var filePath = await _stableSwarmAPI.ConvertAndSaveImage(imageBase64, username, previewMsg.Id, "png", isFinal);

                if (!string.IsNullOrEmpty(filePath))
                {
                    // TODO: Create a seperate method for image editing. Inclide a 2x2 grid of images.
                    // resize image to 1024x768
                    // Load the original image
                    using (var image = System.Drawing.Image.FromFile(filePath))
                    {
                        // Define a new file path for the resized image
                        var newFilePath = Path.Combine(Path.GetDirectoryName(filePath), $"resized-{Path.GetFileName(filePath)}");

                        // Resize the image
                        using (var resizedImage = new Bitmap(image, new Size(1024, 768)))
                        {
                            // Save the resized image to the new file path
                            resizedImage.Save(newFilePath, System.Drawing.Imaging.ImageFormat.Png);
                        }

                        // Update filePath to point to the new resized image
                        filePath = newFilePath;
                    }
                    // Filename used in the attachment
                    string filename = Path.GetFileName(filePath);

                    var updatedEmbed = previewMsg.Embeds.FirstOrDefault()?.ToEmbedBuilder() ?? new EmbedBuilder();
                    updatedEmbed.WithImageUrl($"attachment://{filename}");
                    var fileAttachment = new FileAttachment(filePath);

                    if (isFinal)
                    {
                        updatedEmbed.WithDescription($"Generated an image for **{username}**\n\n**Text:** {text}\n\n**Extra Description:** {description}" +
                        $"\n\n**Template Used:** {template}\n\n`{TemplateInfo}`");
                        updatedEmbed.WithColor(Discord.Color.Green);
                        updatedEmbed.WithFooter("Visit Hartsy.AI to generate more!");

                        var userId = user.Id;
                        string customId = $"regenerate:{userId}";
                        string deleteCustomId = $"delete:{userId}";
                        string showcaseCustomId = $"showcase:{userId}";
                        string reportCustomId = $"report:{userId}";
                        var components = new ComponentBuilder()
                        .WithButton("Regenerate", customId, ButtonStyle.Success)
                        .WithButton("Add to Showcase", showcaseCustomId, ButtonStyle.Primary)
                        .WithButton("Report", reportCustomId, ButtonStyle.Secondary, emote: new Emoji("\u26A0")) // ⚠
                        .WithButton(" ", deleteCustomId, ButtonStyle.Danger, emote: new Emoji("\uD83D\uDDD1"))// 🗑
                        .Build();

                        await previewMsg.ModifyAsync(m =>
                        {
                            m.Embed = updatedEmbed.Build();
                            m.Attachments = new[] { fileAttachment };
                            m.Components = components;
                        });
                    }
                    else
                    {
                        await previewMsg.ModifyAsync(m =>
                        {
                            m.Embed = updatedEmbed.Build();
                            m.Attachments = new[] { fileAttachment };
                        });
                    }
                    if (isFinal) break;  // Exit the loop if the final image has been processed
                }
            }
        }
    }
}
