using Discord.Interactions;
using Discord.WebSocket;
using Discord;
using Hartsy.Core;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace HartsyBot.Core
{
    public class Commands : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly SupabaseClient _supabaseClient;
        private readonly RunpodAPI _runpodAPI;
        private readonly StableSwarmAPI _stableSwarmAPI;

        public Commands()
        {
            _supabaseClient = new SupabaseClient();
            _runpodAPI = new RunpodAPI();
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

        [SlashCommand("setup_rules", "Set up rules for the server.")]
        public async Task SetupRulesCommand()
        {
            // Check if the user has the "HARTSY Staff" role
            var user = Context.User as SocketGuildUser;
            var hasHartsyStaffRole = user.Roles.Any(role => role.Name.Equals("HARTSY Staff", StringComparison.OrdinalIgnoreCase));

            if (!hasHartsyStaffRole)
            {
                await RespondAsync("Only admins can perform this command. Report this with information on how you are even able to see this command!", ephemeral: true);
                return;
            }
            try
            {
                var rulesChannel = Context.Guild.TextChannels.FirstOrDefault(x => x.Name == "rules");
                if (rulesChannel == null)
                {
                    await RespondAsync("Rules channel not found.");
                    return;
                }

                // Initialize default text
                string defaultDescription = "Default description text",
                    defaultServerRules = "Default server rules text",
                    defaultCodeOfConduct = "Default code of conduct text",
                    defaultOurStory = "Default our story text",
                    defaultButtonFunction = "Default button function description text";

                // Extract text from the last message if available
                var messages = await rulesChannel.GetMessagesAsync(1).FlattenAsync();
                var lastMessage = messages.FirstOrDefault();
                if (lastMessage != null && lastMessage.Embeds.Count != 0)
                {
                    var embed = lastMessage.Embeds.First();
                    defaultDescription = embed.Description ?? defaultDescription;
                    defaultServerRules = embed.Fields.Length > 0 ? embed.Fields[0].Value : defaultServerRules;
                    defaultCodeOfConduct = embed.Fields.Length > 1 ? embed.Fields[1].Value : defaultCodeOfConduct;
                    defaultOurStory = embed.Fields.Length > 2 ? embed.Fields[2].Value : defaultOurStory;
                    defaultButtonFunction = embed.Fields.Length > 3 ? embed.Fields[3].Value : defaultButtonFunction;
                }

                // Prepare the modal with default text
                var rulesModal = new RulesModal(defaultDescription, defaultServerRules, defaultCodeOfConduct, defaultOurStory, defaultButtonFunction);

                // Respond with the modal
                await RespondWithModalAsync<RulesModal>("setup_rules_modal", rulesModal);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
                throw;
            }
        }

        [ModalInteraction("setup_rules_modal")]
        public async Task OnRulesModalSubmit(RulesModal modal)
        {
            await DeferAsync(ephemeral: true);
            try
            {
                // Extract the data from the modal
                var description = modal.Description;
                var server_rules = modal.Server_rules;
                var codeOfConduct = modal.CodeOfConduct;
                var ourStory = modal.OurStory;
                var ButtonFunction = modal.ButtonFunction;

                var imagePath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "images", "server_rules.png");
                var stream = new FileStream(imagePath, FileMode.Open);

                // Construct the embed with fields from the modal
                var embed = new EmbedBuilder()
                    .WithTitle("Welcome to the Hartsy.AI Discord Server!")
                    .WithDescription(modal.Description)
                    .AddField("Server Rules", modal.Server_rules)
                    .AddField("Code of Conduct", modal.CodeOfConduct, true)
                    .AddField("Our Story", modal.OurStory, true)
                    .AddField("What Does This Button Do?", modal.ButtonFunction, true)
                    .WithColor(Discord.Color.Blue)
                    .WithCurrentTimestamp()
                    .WithImageUrl("attachment://server_rules.png")
                    .WithFooter("Click the buttons to add roles")
                    .Build();

                // Find the 'rules' channel
                var rulesChannel = Context.Guild.TextChannels.FirstOrDefault(x => x.Name == "rules");
                if (rulesChannel != null)
                {
                    // Check for the last message in the 'rules' channel
                    var messages = await rulesChannel.GetMessagesAsync(1).FlattenAsync();
                    var lastMessage = messages.FirstOrDefault();

                    // If there's an existing message, delete it
                    if (lastMessage != null)
                    {
                        await lastMessage.DeleteAsync();
                    }

                    // Define the buttons
                    var buttonComponent = new ComponentBuilder()
                        .WithButton("I Read the Rules", "read_rules", ButtonStyle.Success)
                        .WithButton("Don't Notify Me", "notify_me", ButtonStyle.Primary)
                        .Build();

                    // Send the new embed with buttons to the 'rules' channel
                    await rulesChannel.SendFileAsync(stream, "server_rules.png", text: null, embed: embed, components: buttonComponent);
                    await FollowupAsync("Rules have been updated!", ephemeral: true);
                }
                else
                {
                    await FollowupAsync("Rules channel not found.", ephemeral: true);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in modal submission: {ex.Message}");
                await FollowupAsync("An error occurred while processing your request.", ephemeral: true);
            }
        }

        public class RulesModal : IModal
        {
            public string Title => "Server Rules";

            [InputLabel("Description")]
            [ModalTextInput("description_input", TextInputStyle.Paragraph,
            placeholder: "Enter a brief description", maxLength: 300)]
            public string? Description { get; set; }

            [InputLabel("Server Rules")]
            [ModalTextInput("server_rules", TextInputStyle.Paragraph,
            placeholder: "List the server rules here", maxLength: 800)]
            public string? Server_rules { get; set; }

            [InputLabel("Code of Conduct")]
            [ModalTextInput("code_of_conduct_input", TextInputStyle.Paragraph,
            placeholder: "Describe the code of conduct", maxLength: 400)]
            public string? CodeOfConduct { get; set; }

            [InputLabel("Our Story")]
            [ModalTextInput("our_story_input", TextInputStyle.Paragraph,
            placeholder: "Share the story of your community", maxLength: 400)]
            public string? OurStory { get; set; }

            [InputLabel("What Does This Button Do?")]
            [ModalTextInput("button_function_description_input", TextInputStyle.Paragraph,
            placeholder: "Explain the function of this button", maxLength: 200)]
            public string? ButtonFunction { get; set; }

            // Constructors
            public RulesModal() { /* ... */ }
            public RulesModal(string description, string server_rules, string codeOfConduct, string ourStory, string buttonFunction)
            {
                // Initialize with provided values
                Description = description;
                Server_rules = server_rules;
                CodeOfConduct = codeOfConduct;
                OurStory = ourStory;
                ButtonFunction = buttonFunction;
            }
        }

        [SlashCommand("generate", "Generate an image from a prompt")]
        public async Task ImageGenerationCommand(
            [Summary("text", "The text you want to appear in the image.")] string text,
            [Summary("template", "Choose a template for the image.")]
            [Autocomplete(typeof(TemplateAutocompleteHandler))] string template,
            [Summary("additional_details", "Describe other aspects to add to the prompt.")] string description = null)
        {
            await DeferAsync(ephemeral: true);
            var user = Context.User as SocketGuildUser;
            var userInfo = await _supabaseClient.GetUserByDiscordId(user.Id.ToString());
            if (userInfo == null)
            {
                await HandleSubscriptionFailure(Context);
                return;
            }
            var subStatus = userInfo.PlanName ?? "Free";
            Console.WriteLine($"User: {user.Username}, Subscription: {subStatus}");
            var credits = userInfo.Credit ?? 0;
            Console.WriteLine("Credits: " + credits);

            // Check if the user has a valid subscription and enough credits
            if (subStatus != null && userInfo.Credit > 0)
            {
                bool creditUpdated = await _supabaseClient.UpdateUserCredit(user.Id.ToString(), credits);
                await FollowupAsync($"You have {credits} GPUT. You will have {credits - 1} GPUT after this image is generated.", ephemeral: true);
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
