using Discord.Interactions;
using Discord.WebSocket;
using Discord;
using Hartsy.Core;

namespace HartsyBot.Core
{
    public class Commands : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly SupabaseClient _supabaseClient;
        private readonly RunpodAPI _runpodAPI;

        public Commands()
        {
            _supabaseClient = new SupabaseClient();
            _runpodAPI = new RunpodAPI();
        }

        [SlashCommand("runpod_test", "test generation from runpod")]
        public async Task RunpodTestCommand()
        {
            try
            {
                var prompt = "A colorful, vibrant, and lively cityscape with a bustling street and towering skyscrapers";
                var imageId = await _runpodAPI.CreateImageAsync(prompt);
                await RespondAsync($"Image ID: {imageId}", ephemeral: true);
            }
            catch (Exception ex)
            {
                await RespondAsync($"An error occurred: {ex.Message}", ephemeral: true);
            }
        }

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
                    .WithColor(Color.Blue)
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

                    embed.WithColor(Color.Blue);
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
            [Choice("Rainbow Flow", "Rainbow"),
                Choice("Phantom Grove", "Phantom"),
                Choice("Shroomantic", "Shroomantic"),
                Choice("Quantum Canvas", "Quantum"),
                Choice("Appster", "Appster"),
                Choice("Brandtastic", "Brandtastic"),
                Choice("Pixel Playground", "Pixel"),
                Choice("Speaking Sushi ", "Sushi"),
                Choice("video game alternate", "alternate"),
                Choice("Unholy Textament", "UnholyTextament"),
                Choice("Words of Wildstyle", "Wildstyle"),
                Choice("Dreamsmith", "Dreamsmith"),
                Choice("Cosmic Comics", "Comics"),
                Choice("Diamond Design", "Diamond")
            ] string template,
            [Summary("additional_details", "Describe other aspects to add to the prompt.")] string description = null)
        {
            await RespondAsync("Generating image, please wait...", ephemeral: true);
            // Get the channel and convert it to a SocketTextChannel
            var channel = Context.Channel as SocketTextChannel;
            var user = Context.User as SocketGuildUser;
            await GenerateImages.GenerateFromTemplate(text, template, channel, user, description);
        }
        public static class GenerateImages
        {
            public static async Task GenerateFromTemplate(string text, string template, SocketTextChannel channel, SocketGuildUser user, string description = null)
            {
                string templated = "";
                Dictionary<string, string> settings = []; // TODO: Add in correct settings for each template
                string TemplateInfo = "";

                switch (template)
                {
                    case "Rainbow":

                        templated = $"(\"{text}\":1.6) (text logo:1.3), rainbow pixel art lettering, " +
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
                        TemplateInfo = "Combining the natural world with a neon palette, this template is designed to create striking images " +
                                        $"that stand out with a blend of organic shapes and bright, artificial colors.";
                        break;
                    case "Phantom":
                        templated = $"(\"{text}\":1.5) (text logo:1.3), green dripping ooze letters, surrounded by spikey interwoven designs, " +
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
                        TemplateInfo = "Capturing the essence of eerie folktales and legends skeletal figures are given a ghastly hue from an unfarmiliar " +
                                        $"moon, great for story-driven or narrative content. Guaranteed to leave your audience in goosebumps.";
                        break;

                    case "Shroomantic":
                        templated = $"\"({text}\":1.5) (text logo:1.3), salient bold neon rainbow lettering, " +
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
                        TemplateInfo = "The template offers a trippy, colorful woodland experience, a pathway to capturing immersive imagination.";
                        break;

                    case "Quantum":
                        templated = $"(\"{text}\":1.5) (text logo:1.3), shiny bold metallic lettering, complex photorealistic computer chip, " +
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
                        TemplateInfo = "A backdrop designed for tech enthusiasts to inscribe their own message, merging the art of communication with the precision of technology.";
                        break;

                    case "Appster":
                        templated = $"(\"{text}\":1.6) (text logo:1.3), colorful, app logo, artistic, minimalist, professional logo, simple, {description}";
                        settings = new Dictionary<string, string>
                                    {
                                        { "lora", "Harrlogos" },
                                        { "weight", "1" },
                                        { "scheduler", "Euler" },
                                        { "sampler", "Karras" },
                                        { "steps", "24" },
                                        { "CFG", "3.4" }
                                    };
                        TemplateInfo = "Creativity through subtlety, this template creates content that not only screams to be clicked or tapped, but also remembered.";
                        break;

                    case "Brandtastic":
                        templated = $"(\"{text}\":1.5) (text logo:1.3), popular company text logo, professional, corporate logo design, " +
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
                        TemplateInfo = "Minimalist, elegant, clean. This template will put a (type)face to your name.";
                        break;

                    case "Pixel":
                        templated = $"(\"{text}\":1.6) (text logo:1.3), colorful, pixel art, video game title screen, voxel based, " +
                                    $"(16 - bit pixel art), weapon, clouds, {description}";
                        settings = new Dictionary<string, string>
                                    {
                                        { "lora", "Harrlogos" },
                                        { "weight", "1" },
                                        { "scheduler", "Euler" },
                                        { "sampler", "Karras" },
                                        { "steps", "24" },
                                        { "CFG", "3.4" }
                                    };
                        TemplateInfo = "Where pixel art meets pictuesque, this template offers all the nostalgia of classic video game graphics, " +
                                        $"while also giving creative control liike never before. ";
                        break;

                    case "Sushi":
                        templated = $"(\"{text}\":1.5) (text logo:1.4), Sushi, japanese, decorated with wasabi and salmon roe, " +
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
                        TemplateInfo = "A creative template for composing text within a delicious sushi arrangement, decorated by traditional Japanese garnishes.";
                        break;

                    case "alternate":
                        templated = $"(\"{text}\":1.5) text logo, pixel art, video game title screen, elemental weapon, " +
                                          $"ornate armor, adventure game setting, clouds, {description}";
                        settings = new Dictionary<string, string>
                                    {
                                        { "lora", "Harrlogos" },
                                        { "weight", "1" },
                                        { "scheduler", "Karras" },
                                        { "steps", "22" },
                                        { "CFG", "4.5" }
                                    };
                        TemplateInfo = "video game alternate";
                        break;

                    case "UnholyTextament":
                        templated = $"(\"{text}\":1.5) (text logo:1.3), salient bold spikey drippy lettering, terrifying depths of hell, " +
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
                        TemplateInfo = "Ideal for conveying a sense of the macabre, this template includes elements of gothic horror and heavy metal motifs.";
                        break;

                    case "Wildstyle":
                        templated = $"(\"{text}\":1.5) (text logo:1.3), wildstyle 90 graffiti, thick lines, dripping oil based colors, " +
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
                        TemplateInfo = "This template celebrates wildstyle, a legendary form of Graffiti revered for it's " +
                                        $"complexity and intricate design, by putting the spray can in YOUR hand.";
                        break;

                    case "Dreamsmith":
                        templated = $"(\"{text}\":1.5) (text logo:1.3), dreamscape landscape, strange future, secret nature, chroma, " +
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
                        TemplateInfo = "Use this template to bring to life enchanting worlds that are both tranquil and vibrant, invoking a sense of dreamy escapism.";
                        break;

                    case "Comics":
                        templated = $"(\"{text}\":1.5) (text logo:1.3), comic book cover illustration, thick comic book title lettering " +
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
                        TemplateInfo = "A template that brings comic book flair to the creation of outer space scenes, featuring detailed planets and glowing astral bodies.";
                        break;

                    case "Diamond":
                        templated = $"(\"{text}\":1.6) (text logo:1.3), bright, sparkling diamond encrusted letters, very expensive flashy, gold and silver jewelry items, " +
                                    $"glistening from the large diamonds {description}";
                        settings = new Dictionary<string, string>
                        {
                                        { "lora", "Harrlogos" },
                                        { "scheduler", "dpmpp se gpu" },
                                        { "sampler", "Karras" },
                                        { "steps", "35" },
                                        { "CFG", "3.7" }
                                    };
                        TemplateInfo = "Awaken your inner jeweler with Diamond Design! Encrust your text into high end gold and silver jewelry with beautiful sparking diamonds.";
                        break;

                    default:
                        // If no template matches
                        Console.WriteLine("Unknown template.");

                        await channel.SendMessageAsync("Unknown template. Please try again and choose a template.");
                        break;
                }

                // Construct the prompt from the parameters
                string prompt = templated;
                var username = user.Username;
                string projectRoot = Directory.GetParent(Environment.CurrentDirectory).Parent.Parent.FullName;
                string waitImageFilePath = Path.Combine(projectRoot, "images", "wait.gif");


                // Create a placeholder embed
                var embed = new EmbedBuilder()
                    .WithAuthor(user)
                    .WithTitle("Thank you for generating your image with Hartsy.AI")
                    .WithDescription($"Generating an image described by **{username}**\n\n" +
                     $"**Template Used:** {template}\n\n`{TemplateInfo}`\n\n")
                    .WithColor(Color.DarkerGrey)
                    .WithFooter("CFG:4.5 | Steps:35 | Height:768 | Width:1024")
                    .WithCurrentTimestamp()
                    .WithImageUrl($"attachment://wait.gif")
                    .Build();

                // Send the placeholder message
                //var previewMsg = await channel.SendMessageAsync(embed: embed);
                var previewMsg = await channel.SendFileAsync(waitImageFilePath, "wait.gif", embed: embed);

                // Generate the image
                var base64Images = await StableSwarmAPI.GenerateImage(prompt);
                if (base64Images.Count > 0)
                {
                    var apiInstance = new StableSwarmAPI();
                    string filePath = await apiInstance.ConvertAndSaveImage(base64Images[0], username, previewMsg.Id, "jpg");
                    // Filename used in the attachment
                    string filename = Path.GetFileName(filePath);

                    // Set the description to "None" if it's empty
                    description = string.IsNullOrEmpty(description) ? "None" : description;


                    if (!string.IsNullOrEmpty(filePath))
                    {
                        // Modify the message by grabbing the embed and generate a embedbuilder
                        var updatedEmbed = previewMsg.Embeds.First().ToEmbedBuilder();
                        updatedEmbed.WithDescription($"Generated an image for **{username}**\n\n**Text:** {text}\n\n**Extra Description:** {description}" +
                            $"\n\n**Template Used:** {template}\n\n`{TemplateInfo}`");
                        updatedEmbed.WithColor(Color.Green);
                        updatedEmbed.WithImageUrl($"attachment://{filename}");
                        var fileAttachment = new FileAttachment(filePath);

                        var components = new ComponentBuilder()
                            .WithButton("Regenerate", "regenerate", ButtonStyle.Success)
                            .WithButton("Add to Showcase", "showcase:add", ButtonStyle.Primary)
                            .WithButton("Report", "report:admin", ButtonStyle.Secondary, emote: new Emoji("\u26A0")) // ⚠
                            .WithButton(" ", "delete", ButtonStyle.Danger, emote: new Emoji("\uD83D\uDDD1"))// 🗑
                            .Build();

                        // Update the original message with the new embed and attachment
                        await previewMsg.ModifyAsync(m =>
                        {
                            m.Embed = updatedEmbed.Build();
                            m.Attachments = new[] { fileAttachment };
                            m.Components = components;
                        });
                    }
                    else
                    {
                        await channel.SendMessageAsync("Failed to generate image.");
                    }
                }
                else
                {
                    await channel.SendMessageAsync("No images were generated.");
                }
            }
        }

    }
}