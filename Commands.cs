using Discord.Interactions;
using Discord.WebSocket;
using System.Threading.Tasks;
using Discord;
using System.Linq; 

namespace HartsyBot
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
        string titlePlaceholder = "Enter the title", 
            descriptionPlaceholder = "Enter the description",
            footerPlaceholder = "Enter the footer text",
            authorPlaceholder = "Enter the author's name";

        // Get the last message from the rules channel
        var messages = await rulesChannel.GetMessagesAsync(1).FlattenAsync();
        var lastMessage = messages.FirstOrDefault();
        if (lastMessage != null && lastMessage.Embeds.Any())
        {
            var embed = lastMessage.Embeds.First();
            titlePlaceholder = embed.Title ?? titlePlaceholder;
            descriptionPlaceholder = embed.Description ?? descriptionPlaceholder;
            footerPlaceholder = embed.Footer?.Text ?? footerPlaceholder;
            authorPlaceholder = embed.Author?.Name ?? authorPlaceholder;
        }

        // Prepare the modal with dynamic placeholders
        var rulesModal = new RulesModal(titlePlaceholder, descriptionPlaceholder, footerPlaceholder, authorPlaceholder);

        // Respond with the modal
        await RespondWithModalAsync(rulesModal);
    }

    [SlashCommand("ping", "Pings the bot.")]
    public async Task PingCommand()
    {
        await RespondAsync("Pong!");
    }

    //[ModalInteraction("modal_input_demo")]
    //public async Task ModalResponse(HelloModal modal)

    [ModalInteraction("setup_rules_modal")]
    public async Task OnRulesModalSubmit(RulesModal modal)
    {
        try
        {
            // Extract the data from the modal
            var title = modal.TitleInput;
            var description = modal.Description;
            var footer = modal.Footer;
            var author = modal.Author;

            // Construct the embed
            var embed = new EmbedBuilder()
                .WithTitle(title)
                .WithDescription(description)
                .WithFooter(footer)
                .WithAuthor(author)
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
                await rulesChannel.SendMessageAsync(embed: embed, components: buttonComponent);
            }
            else
            {
                await RespondAsync("Rules channel not found.", ephemeral: true);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in modal submission: {ex.Message}");
            await RespondAsync("An error occurred while processing your request.", ephemeral: true);
        }
    }

    public class RulesModal : IModal
    {
        public string Title => "Server Rules";

        [InputLabel("Title")]
        [ModalTextInput("title_input", maxLength: 100)]
        public string TitleInput { get; set; }

        [InputLabel("Description")]
        [ModalTextInput("description_input", TextInputStyle.Paragraph, maxLength: 1000)]
        public string Description { get; set; }

        [InputLabel("Footer")]
        [ModalTextInput("footer_input", maxLength: 100)]
        public string Footer { get; set; }

        [InputLabel("Author")]
        [ModalTextInput("author_input", maxLength: 100)]
        public string Author { get; set; }

        public RulesModal(string titlePlaceholder, string descriptionPlaceholder, string footerPlaceholder, string authorPlaceholder)
        {
            TitleInput = titlePlaceholder;
            Description = descriptionPlaceholder;
            Footer = footerPlaceholder;
            Author = authorPlaceholder;
        }
    }
}