using Discord.Interactions;
using Discord.WebSocket;
using System.Threading.Tasks;
using Discord;
using System.Linq; 

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
                string descriptionDefaultText = "Default description text",
                    server_rulesDefault = "Default field 1 text",
                    field2DefaultText = "Default field 2 text",
                    field3DefaultText = "Default field 3 text",
                    field4DefaultText = "Default field 4 text";

                // Extract text from the last message if available
                var messages = await rulesChannel.GetMessagesAsync(1).FlattenAsync();
                var lastMessage = messages.FirstOrDefault();
                if (lastMessage != null && lastMessage.Embeds.Any())
                {
                    var embed = lastMessage.Embeds.First();
                    descriptionDefaultText = embed.Description ?? descriptionDefaultText;
                    server_rulesDefault = embed.Fields.Length > 0 ? embed.Fields[0].Value : server_rulesDefault;
                    field2DefaultText = embed.Fields.Length > 1 ? embed.Fields[1].Value : field2DefaultText;
                    field3DefaultText = embed.Fields.Length > 2 ? embed.Fields[2].Value : field3DefaultText;
                    field4DefaultText = embed.Fields.Length > 3 ? embed.Fields[3].Value : field4DefaultText;
                }
                Console.WriteLine($"descriptionDefaultText: {descriptionDefaultText}");

                // Prepare the modal with default text
                var rulesModal = new RulesModal(descriptionDefaultText, server_rulesDefault, field2DefaultText, field3DefaultText, field4DefaultText);
                Console.WriteLine($"rulesModal.Description: {rulesModal.Description}");
                Console.WriteLine($"rulesModal.server_rules: {rulesModal.Server_rules}");
                Console.WriteLine($"rulesModal.Field2: {rulesModal.Field2}");
                Console.WriteLine($"rulesModal.Field3: {rulesModal.Field3}");
                Console.WriteLine($"rulesModal.Field4: {rulesModal.Field4}");

                //// Respond with the modal
                await RespondWithModalAsync<RulesModal>("setup_rules_modal");
            }
            catch (Exception ex)
            {
                // Handle any exceptions that occur within the try block here
                // You can log the exception or take appropriate actions
                Console.WriteLine($"An error occurred: {ex.Message}");
                // Optionally, you can rethrow the exception if you want it to propagate further
                throw;
            }
        }

        [SlashCommand("test_welcome", "Tests the Welcome message")]
        public async Task TestCommand()
        { //add embed to welcome message with image as attachment
            var imagePath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "images", "welcome.png");
            var stream = new FileStream(imagePath, FileMode.Open);

            var embed = new EmbedBuilder()
                .WithTitle("Welcome to the Hartsy.AI Discord Server!")
                .WithDescription("This is a test of the welcome message")
                .WithColor(Color.Blue)
                .WithCurrentTimestamp()
                .WithImageUrl("attachment://welcome.png")
                .WithFooter("Click the buttons to add roles")
                .Build();
            // send the embed to the channel the command was run in.
            
            await Context.Channel.SendFileAsync(stream, "welcome.png", text: null, embed: embed);
            await RespondAsync("Welcome message sent!", ephemeral: true);
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
                var field2 = modal.Field2;
                var field3 = modal.Field3;
                var field4 = modal.Field4;

                var imagePath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "images", "server_rules.png");
                var stream = new FileStream(imagePath, FileMode.Open);

                // Construct the embed with fields from the modal
                var embed = new EmbedBuilder()
                    .WithTitle("Welcome to the Hartsy.AI Discord Server!")
                    .WithDescription(modal.Description)
                    .AddField("Server Rules", modal.Server_rules)
                    .AddField("Code of Conduct", modal.Field2, true)
                    .AddField("Our Story", modal.Field3, true)
                    .AddField("What Dat Button Do?", modal.Field4, true)
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
            [ModalTextInput("description_input", TextInputStyle.Paragraph, maxLength: 300)]
            public string Description { get; set; }

            // Additional fields
            [InputLabel("Server Rules")]
            [ModalTextInput("server_rules", TextInputStyle.Paragraph, maxLength: 800)]
            public string Server_rules { get; set; }

            [InputLabel("Code of Conduct")]
            [ModalTextInput("field2_input", TextInputStyle.Paragraph, maxLength: 400)]
            public string Field2 { get; set; }

            [InputLabel("Our Story")]
            [ModalTextInput("field3_input", TextInputStyle.Paragraph, maxLength: 400)]
            public string Field3 { get; set; }

            [InputLabel("What dat Button Do?")]
            [ModalTextInput("field4_input", TextInputStyle.Paragraph, maxLength: 200)]
            public string Field4 { get; set; }

            // Parameterless constructor
            public RulesModal()
            {
                // Initialize with default values or leave blank
            }

            // Custom constructor with parameters
            public RulesModal(string description, string server_rules, string field2, string field3, string field4)
            {
                Description = description;
                Server_rules = server_rules;
                Field2 = field2;
                Field3 = field3;
                Field4 = field4;
            }
        }
    }
}