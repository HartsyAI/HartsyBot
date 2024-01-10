using Discord.Interactions;
using Discord.WebSocket;
using System.Threading.Tasks;
using Discord;

namespace HartsyBot
{
    public class Commands : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("setup_rules", "Set up rules for the server.")]
        public async Task SetupRulesCommand()
        {
            var modal = new ModalBuilder()
                .WithTitle("Server Rules")
                .WithCustomId("setup_rules_modal")
                .AddTextInput("Title", "title_input", placeholder: "Enter the title", maxLength: 100)
                .AddTextInput("Description", "description_input", placeholder: "Enter the description", maxLength: 1000)
                .AddTextInput("Footer", "footer_input", placeholder: "Enter the footer text", maxLength: 100)
                .AddTextInput("Author", "author_input", placeholder: "Enter the author's name", maxLength: 100)
                .Build();

            await RespondWithModalAsync(modal);
        }

        [SlashCommand("ping", "Pings the bot.")]
        public async Task PingCommand()
        {
            await RespondAsync("Pong!");
        }

    }
}