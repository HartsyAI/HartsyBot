using Discord.Interactions;
using Discord.WebSocket;
using System.Threading.Tasks;
using Discord;
using System.Linq;
using System.Reflection.Metadata;
using System.Net.Mail;
using Microsoft.VisualBasic;
using System.Threading.Channels;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using System.ComponentModel;

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

        [SlashCommand("logo_generator", "Generate an image from a prompt")]
        public async Task ImageGenerationCommand(
            [Summary("text", "The text you want to appear in the image.")] string text,
            [Summary("template", "Choose a template for the image.")]
            [Choice("Rainbow Flow", "Template1"),
                Choice("Phantom Grove", "Template2"),
                Choice("Shroomantic", "Template3"),
                Choice("Quantum Canvas", "Template4"),
                Choice("Appster", "Template5"),
                Choice("Brandtastic", "Template6"),
                Choice("video game", "Template7"),
                Choice("Speaking Sushi ", "Template8"),
                Choice("video game alternate", "Template9"),
                Choice("Unholy Textament", "Template10"),
                Choice("Words of Wildstyle", "Template11"),
                Choice("Dreamsmith", "Template12"),
                Choice("Cosmic Comics", "Template13"),
            ] string template,
            [Summary("description", "Describe other aspects of the image.")] string description)
        {
            string templated = "";
            Dictionary<string, string> settings = new Dictionary<string, string>(); // TODO: Add in correct settings for each template

            switch (template)
            {
                case "Template1":
                    
                    templated = $"({text}:1.6) (text logo:1.3), rainbow pixel art lettering, " +
                                    $"voxel based, vibrant colored paint splattered, dripping paint in " +
                                    $"intertwining intricate patterns to form geometrical shapes, " +
                                    $"technicolor clouds fill the sky, {description}";
                    settings = new Dictionary<string, string>
                                {
                                    { "lora", "Harrlogos" },
                                    { "weight", "1" },
                                    { "scheduler", "Euler" },
                                    { "sampler", "Karras" },
                                    { "steps", "24" },
                                    { "CFG", "3.4" }
                                };
                    break;
                case "Template2":
                    templated = $"({text}:1.5) (text logo:1.3), green dripping ooze letters, surrounded by spikey interwoven designs, " +
                                      $"a foggy forest in the dark of night, skeletons rise through the forest with glowing neon green eyes, " +
                                      $"while a moon illuminates rays through the fog, the background is deep purple and eerie, {description}";
                    settings = new Dictionary<string, string>
                                {
                                    { "lora", "Harrlogos" },
                                    { "weight", "1" },
                                    { "scheduler", "Euler" },
                                    { "sampler", "Karras" },
                                    { "steps", "24" },
                                    { "CFG", "3.4" }
                                };
                    break;

                case "Template3":
                    templated = $"({text}:1.5) (text logo:1.3), salient bold neon rainbow lettering, " +
                                      $"Enthralling fantasy forest, brimming with oversized, luminescent mushrooms, " +
                                      $"gigantic ancient tree with a face, whispering secrets, {description}";
                    settings = new Dictionary<string, string>
                                {
                                    { "lora", "Harrlogos" },
                                    { "weight", "1" },
                                    { "scheduler", "Euler" },
                                    { "sampler", "Karras" },
                                    { "steps", "24" },
                                    { "CFG", "3.4" }
                                };
                    break;

                case "Template4":
                    templated = $"({text}:1.5) (text logo:1.3), shiny bold metallic lettering, complex photorealistic computer chip, " +
                                      $"covered with intertwined, luminescent rgb wires, {description}";
                    settings = new Dictionary<string, string>
                                {
                                    { "lora", "Harrlogos" },
                                    { "weight", "1" },
                                    { "scheduler", "Euler" },
                                    { "sampler", "Karras" },
                                    { "steps", "24" },
                                    { "CFG", "3.4" }
                                };
                    break;

                case "Template5":
                    templated = $"({text}:1.6) (text logo:1.3), colorful, app logo, artistic, minimalist, professional logo, simple, {description}";
                    settings = new Dictionary<string, string>
                                {
                                    { "lora", "Harrlogos" },
                                    { "weight", "1" },
                                    { "scheduler", "Euler" },
                                    { "sampler", "Karras" },
                                    { "steps", "24" },
                                    { "CFG", "3.4" }
                                };
                    break;

                case "Template6":
                    templated = $"({text}:1.5) (text logo:1.3), popular company text logo, professional, corporate logo design, " +
                                      $"simple colors, minimalist, {description}";
                    settings = new Dictionary<string, string>
                                {
                                    { "lora", "Harrlogos" },
                                    { "weight", "1" },
                                    { "scheduler", "Euler" },
                                    { "sampler", "Karras" },
                                    { "steps", "24" },
                                    { "CFG", "3.4" }
                                };
                    break;

                case "Template7":
                    templated = $"({text}:1.4) (text logo:1.3), pixel art 16-bit style, video game title screen, " +
                                      $"elemental weapons, ornate armor, assorted video game item sprites, clouds, {description}";
                    settings = new Dictionary<string, string>
                                {
                                    { "lora", "Harrlogos" },
                                    { "weight", "1" },
                                    { "scheduler", "Euler" },
                                    { "sampler", "Karras" },
                                    { "steps", "24" },
                                    { "CFG", "3.4" }
                                };
                    break;

                case "Template8":
                    templated = $"({text}:1.5) text logo, Sushi, japanese, decorated with wasabi and salmon roe, " +
                                      $"{description}";
                    settings = new Dictionary<string, string>
                                {
                                    { "lora", "Harrlogos" },
                                    { "weight", "1" },
                                    { "scheduler", "Euler" },
                                    { "sampler", "Karras" },
                                    { "steps", "24" },
                                    { "CFG", "3.4" }
                                };
                    break;

                case "Template9":
                    templated = $"({text}:1.5) text logo, pixel art, video game title screen, elemental weapon, " +
                                      $"ornate armor, adventure game setting, clouds, {description}, Settings: FUSTERCLUCK checkpoint, " +
                                      $"harrlogos weight 1 22 steps, 4.5 CFG";
                    settings = new Dictionary<string, string>
                                {
                                    { "lora", "Harrlogos" },
                                    { "weight", "1" },
                                    { "scheduler", "Karras" },
                                    { "steps", "22" },
                                    { "CFG", "4.5" }
                                };
                    break;

                case "Template10":
                    templated = $"({text}:1.5) (text logo:1.3), salient bold spikey drippy lettering, terrifying depths of hell, " +
                                      $"brimming with oversized, luminescent pentagrams, inverted crosses glowing in the darkness, " +
                                      $"amidst a river of blood, nightmarish, {description}";
                    settings = new Dictionary<string, string>
                                {
                                    { "lora", "Harrlogos" },
                                    { "scheduler", "dpmpp se gpu" },
                                    { "sampler", "Karras" },
                                    { "steps", "35" },
                                    { "CFG", "3.7" }
                                };
                    break;

                case "Template11":
                    templated = $"({text}:1.5) (text logo:1.3), wildstyle 90 graffiti, thick lines, dripping oil based colors, " +
                                      $"black brick wall, urban city sidewalk, dim streetlight, at night, {description}";
                    settings = new Dictionary<string, string>
                                {
                                    { "lora", "Harrlogos" },
                                    { "weight", "1" },
                                    { "scheduler", "Euler" },
                                    { "sampler", "Karras" },
                                    { "steps", "24" },
                                    { "CFG", "3.4" }
                                };
                    break;

                case "Template12":
                    templated = $"({text}:1.5) (text logo:1.3), dreamscape landscape, strange future, secret nature, chroma, " +
                                      $"Surreal, ethereal, dreamy, mysterious, fantasy, highly detailed, {description}";
                    settings = new Dictionary<string, string>
                                {
                                    { "lora", "Harrlogos" },
                                    { "weight", "1" },
                                    { "scheduler", "Euler" },
                                    { "sampler", "Karras" },
                                    { "steps", "24" },
                                    { "CFG", "3.4" }
                                };
                    break;

                case "Template13":
                    templated = $"({text}:1.5) (text logo:1.3), comic book cover illustration, thick comic book title lettering " +
                                      $"in an outer space scene, planet, moons orbiting, holographic meteors leave technicolor trails " +
                                      $"in the vast black emptiness of the universe, {description}";
                    settings = new Dictionary<string, string>
                                {
                                    { "lora", "Harrlogos" },
                                    { "weight", "1" },
                                    { "scheduler", "Euler" },
                                    { "sampler", "Karras" },
                                    { "steps", "24" },
                                    { "CFG", "3.4" }
                                };
                    break;

                default:
                    // If no template matches
                    Console.WriteLine("Unknown template.");
                    await RespondAsync("Unknown template. Please try again and choose a template.", ephemeral: true);
                    break;
            }

            // Construct the prompt from the parameters
            string prompt = templated;

            // Defer the interaction to buy time for image generation
            await DeferAsync();

            // Create a placeholder embed
            var embed = new EmbedBuilder()
                .WithAuthor(Context.User)
                .WithTitle("Your Hartsy.AI Generated Image")
                .WithDescription($"Generating an image described by **{Context.User.Username}**\n" +
                 $"**Prompt:** `{prompt}`\n\n" +
                 "+-------Please wait while your image is being generated-------+\n\n")
                .WithColor(Color.DarkerGrey)
                .WithFooter("CFG:4.5|Steps:35|Height:768|Width:1024")
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
                    updatedEmbed.WithColor(Color.Green);
                    updatedEmbed.WithImageUrl($"attachment://{filename}");
                    var fileAttachment = new FileAttachment(filePath);

                    // Update the original response with the new embed and attachment
                    await newMessage.ModifyAsync(m =>
                    {
                        m.Embed = updatedEmbed.Build();
                        m.Attachments = new[] { fileAttachment };
                    });
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