using Discord.Interactions;
using Discord.WebSocket;
using System.Threading.Tasks;
using Discord;
using System.Linq;
using System.Reflection.Metadata;
using System.Net.Mail;
using Microsoft.VisualBasic;
using System.Threading.Channels;

namespace HartsyBot
{
    public class Commands : InteractionModuleBase<SocketInteractionContext>
    {
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
                if (lastMessage != null && lastMessage.Embeds.Any())
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
                    .WithColor(Color.Blue)
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
                        .WithButton("Notify Me", "notify_me", ButtonStyle.Primary)
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
        [SlashCommand("test_welcome", "Tests the Welcome message")]
        public async Task TestCommand()
        {
            // Path to the image
            var imagePath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "images", "welcome.png");

            // Open the image file
            using (var stream = new FileStream(imagePath, FileMode.Open))
            {
                // Message to be sent, mentioning the user who initiated the command
                string message = $"{Context.User.Mention}, welcome to the Hartsy.AI Discord Server!";

                // Send the image along with the message
                await Context.Channel.SendFileAsync(stream, "welcome.png", message);
            }
            // Respond to the command.
            await RespondAsync("Welcome message sent!", ephemeral: true);
        }

        [SlashCommand("test_image_gen", "Generate an image from a prompt")]
        public async Task TestImageGenerationCommand(string prompt)
        {
            // Defer the interaction to buy time for image generation
            await DeferAsync();

            // Create a placeholder embed
            var embed = new EmbedBuilder()
                .WithAuthor(Context.User)
                .WithTitle("Your logo")
                .WithDescription($"Generating an image for **{Context.User.Username}**\n**Prompt:**`{prompt}`")
                .WithColor(Color.Blue)
                .WithCurrentTimestamp()
                .Build();

            // Update the original response with the placeholder embed
            await Context.Interaction.ModifyOriginalResponseAsync(msg => msg.Embed = embed);

            // Generate the image
            var base64Images = await StableSwarmAPI.GenerateImage(prompt);
            if (base64Images.Count > 0)
            {
                var apiInstance = new StableSwarmAPI();
                string filePath = await apiInstance.ConvertAndSaveImage(base64Images[0], Context.User.Username, Context.Interaction.Id, "jpg");
                // Filename used in the attachment
                string filename = Path.GetFileName(filePath);

                if (!string.IsNullOrEmpty(filePath))
                {
                    // Modify the message by grabbing the embed and generate a embedbuilder
                    var newMessage = await Context.Interaction.GetOriginalResponseAsync();
                    var updatedEmbed = newMessage.Embeds.First().ToEmbedBuilder();
                    updatedEmbed.WithDescription($"Generated an image for **{Context.User.Username}**\n**Prompt:**`{prompt}`");
                    updatedEmbed.WithImageUrl($"attachment://{filename}");
                    var fileAttachment = new FileAttachment(filePath);


                    // Update the original response with the new embed and attachment
                    await newMessage.ModifyAsync(m =>
                    {
                        m.Embed = updatedEmbed.Build();
                        m.Attachments = new[] { fileAttachment };
                    });
                    // send a new messahe to the channel with the same embed and attached file
                    //await Context.Channel.SendFileAsync(filePath, embed: updatedEmbed.Build());
                }
                else
                {
                    await Context.Interaction.ModifyOriginalResponseAsync(msg => msg.Content = "Failed to generate image.");
                }
            }
            else
            {
                await Context.Interaction.ModifyOriginalResponseAsync(msg => msg.Content = "No images were generated.");
            }
        }

    }
}