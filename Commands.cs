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
            var rulesChannel = Context.Guild.TextChannels.FirstOrDefault(x => x.Name == "rules");
            if (rulesChannel == null)
            {
                await RespondAsync("Rules channel not found.");
                return;
            }

            // Initialize placeholders
            string descriptionPlaceholder = "Enter the description",
                field1Placeholder = "Enter field 1",
                field2Placeholder = "Enter field 2",
                field3Placeholder = "Enter field 3",
                field4Placeholder = "Enter field 4";

            // Check for the last message to populate the placeholders
            var messages = await rulesChannel.GetMessagesAsync(1).FlattenAsync();
            var lastMessage = messages.FirstOrDefault();
            if (lastMessage != null && lastMessage.Embeds.Any())
            {
                var embed = lastMessage.Embeds.First();
                descriptionPlaceholder = embed.Description ?? descriptionPlaceholder;
                // Extract fields from the embed if they exist
                field1Placeholder = embed.Fields.Length > 0 ? embed.Fields[0].Value : field1Placeholder;
                field2Placeholder = embed.Fields.Length > 1 ? embed.Fields[1].Value : field2Placeholder;
                field3Placeholder = embed.Fields.Length > 2 ? embed.Fields[2].Value : field3Placeholder;
                field4Placeholder = embed.Fields.Length > 3 ? embed.Fields[3].Value : field4Placeholder;
            }

            // Prepare the modal with dynamic placeholders
            var rulesModal = new RulesModal(descriptionPlaceholder, field1Placeholder, field2Placeholder, field3Placeholder, field4Placeholder);

            // Build the modal using ModalBuilder
            var modalBuilder = new ModalBuilder()
                .WithTitle("Server Rules")
                .WithCustomId("setup_rules_modal")
                .AddTextInput("Description", "description_input", placeholder: rulesModal.Description, style: TextInputStyle.Paragraph, maxLength: 1000)
                .AddTextInput("Field 1", "field1_input", placeholder: rulesModal.Field1, maxLength: 1000)
                .AddTextInput("Field 2", "field2_input", placeholder: rulesModal.Field2, maxLength: 1000)
                .AddTextInput("Field 3", "field3_input", placeholder: rulesModal.Field3, maxLength: 1000)
                .AddTextInput("Field 4", "field4_input", placeholder: rulesModal.Field4, maxLength: 1000);

            // Respond with the modal
            await RespondWithModalAsync(modalBuilder.Build());
        }

            [SlashCommand("ping", "Pings the bot.")]
        public async Task PingCommand()
        {
            await RespondAsync("Pong!");
        }

        [ModalInteraction("setup_rules_modal")]
        public async Task OnRulesModalSubmit(RulesModal modal)
        {
            await DeferAsync(ephemeral: true);
            try
            {
                // Extract the data from the modal
                var description = modal.Description;
                var field1 = modal.Field1;
                var field2 = modal.Field2;
                var field3 = modal.Field3;
                var field4 = modal.Field4;

                var imagePath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "images", "server_rules.jpg");
                var stream = new FileStream(imagePath, FileMode.Open);

                // Construct the embed with fields from the modal
                var embed = new EmbedBuilder()
                    .WithTitle("Welcome to the Hartsy.AI Discord Server!")
                    .WithDescription(modal.Description)
                    .AddField("Field 1", modal.Field1, true)
                    .AddField("Field 2", modal.Field2, true)
                    .AddField("Field 3", modal.Field3, true)
                    .AddField("Field 4", modal.Field4, true)
                    .WithColor(Color.Blue)
                    .WithCurrentTimestamp()
                    .WithImageUrl("attachment://server_rules.jpg")
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
                    await rulesChannel.SendFileAsync(stream, "server_rules.jpg", text: null, embed: embed, components: buttonComponent);
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
            [ModalTextInput("description_input", TextInputStyle.Paragraph, maxLength: 1000)]
            public string Description { get; set; }

            // Additional fields
            [InputLabel("Field 1")]
            [ModalTextInput("field1_input", maxLength: 1000)]
            public string Field1 { get; set; }

            [InputLabel("Field 2")]
            [ModalTextInput("field2_input", maxLength: 1000)]
            public string Field2 { get; set; }

            [InputLabel("Field 3")]
            [ModalTextInput("field3_input", maxLength: 1000)]
            public string Field3 { get; set; }

            [InputLabel("Field 4")]
            [ModalTextInput("field4_input", maxLength: 1000)]
            public string Field4 { get; set; }

            // Parameterless constructor
            public RulesModal()
            {
                // Initialize with default values or leave blank
            }

            // Custom constructor with parameters
            public RulesModal(string descriptionPlaceholder, string field1Placeholder, 
                string field2Placeholder, string field3Placeholder, string field4Placeholder)
            {
                Description = descriptionPlaceholder;
                Field1 = field1Placeholder;
                Field2 = field2Placeholder;
                Field3 = field3Placeholder;
                Field4 = field4Placeholder;
            }
        }
    }
}