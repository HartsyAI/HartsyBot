using Discord.Interactions;
using Discord.WebSocket;
using Discord;
using Hartsy.Core;
using System.Drawing;
using System.Drawing.Imaging;

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

        //[SlashCommand("runpod_test", "test generation from runpod")]
        //public async Task RunpodTestCommand()
        //{
        //    try
        //    {
        //        string userId = Context.User.Id.ToString();
        //        _supabaseClient.AddGenerationAsync(userId);
        //        await RespondAsync("Testing, please wait...", ephemeral: true);
        //    }
        //    catch (Exception ex)
        //    {
        //        await RespondAsync($"An error occurred: {ex.Message}", ephemeral: true);
        //    }
        //}

        [SlashCommand("help", "Learn how to use the bot")]
        public async Task HelpCommand()
        {
            try
            {
                var embed = new EmbedBuilder()
                    .WithTitle("Hartsy.AI Bot Help")
                    .WithThumbnailUrl(Context.Guild.IconUrl)
                    .WithDescription("Hartsy.AI is the premier Stable Diffusion platform for generating images with text directly in Discord. \n\nOur custom Discord bot enables users to generate images with text using our fine-tuned templates, choose your favorite images to send to #showcase for community voting, and potentially get featured weekly on the server. \n\nDiscover more and subscribe at: https://hartsy.ai/subs")
                    .AddField("Available Slash Commands", "Checked the pinned messages for a more detailed explanation of these commands.", false)
                    .AddField("/generate", "Generate an image based on the text you provide, select a template, and optionally add extra prompt information. Example: `/generate_logo text:\"Your Text\" template:\"Template Name\" additions:\"Extra Prompt\"`", false)
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
                if (targetUser != null && !user.Roles.Any(x => x.Name == "HARTSY Staff"))
                {
                    await RespondAsync("Only admins are allowed to specify a user. Just run the command without specifying a user and it will automatically show your info.", ephemeral: true);
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
                        .AddField("Subscription Level", subscriptionInfo != null ? subscriptionInfo.Status ?? "Active" : "No Subscription", true)
                        .AddField("Credit Limit", userInfo.Credit?.ToString() ?? "N/A", true)
                        .AddField("Likes", userInfo.Likes?.ToString() ?? "0", true);

                    if (subscriptionInfo != null)
                    {
                        embed.AddField("Subscription Status", subscriptionInfo.Status ?? "N/A", true);
                    }

                    embed.WithColor(Discord.Color.Blue);
                    await RespondAsync(embed: embed.Build(), ephemeral: true);
                }
                else
                {
                    await RespondAsync("User information not found in the database.", ephemeral: true);
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
            await RespondAsync("Generating image, please wait...", ephemeral: true);
            // Get the channel and convert it to a SocketTextChannel
            var channel = Context.Channel as SocketTextChannel;
            var user = Context.User as SocketGuildUser;
            await GenerateFromTemplate(text, template, channel, user, description);
        }

        public async Task GenerateFromTemplate(string text, string template, SocketTextChannel channel, SocketGuildUser user, string description = null)
        {
            string prompt = string.Empty;
            string TemplateInfo = string.Empty;
            // Fetch the templates from the database
            var templates = await _supabaseClient.GetTemplatesAsync();
            if (templates != null && templates.TryGetValue(template, out var templateDetails))
            {
                string positiveText = templateDetails.Positive.Replace("__TEXT_REPLACE__", text);
                prompt = $"{positiveText}, {description}";
                TemplateInfo = templateDetails.Description;
            }

            var username = user.Username;

            // Create a placeholder embed
            var embed = new EmbedBuilder()
                .WithAuthor(user)
                .WithTitle("Thank you for generating your image with Hartsy.AI")
                .WithDescription($"Generating an image described by **{username}**\n\n" +
                                 $"**Template Used:** {template}\n\n`{TemplateInfo}`\n\n")
                .WithColor(Discord.Color.DarkerGrey)
                .WithCurrentTimestamp()
                .Build();

            var previewMsg = await channel.SendMessageAsync(embed: embed);

            // Generate the image and update the embed with each received image
            await foreach (var (imageBase64, isFinal) in _stableSwarmAPI.GenerateImage(prompt))
            {
                Console.WriteLine($"Received image. Final: {isFinal}");
                var filePath = await _stableSwarmAPI.ConvertAndSaveImage(imageBase64, username, previewMsg.Id, "png", isFinal);

                if (!string.IsNullOrEmpty(filePath))
                {
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

                        var components = new ComponentBuilder()
                        .WithButton("Regenerate", "regenerate", ButtonStyle.Success)
                        .WithButton("Add to Showcase", "showcase:add", ButtonStyle.Primary)
                        .WithButton("Report", "report:admin", ButtonStyle.Secondary, emote: new Emoji("\u26A0")) // ⚠
                        .WithButton(" ", "delete", ButtonStyle.Danger, emote: new Emoji("\uD83D\uDDD1"))// 🗑
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
