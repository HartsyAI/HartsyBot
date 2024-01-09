using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using System.Threading.Tasks;

namespace HartsyBot
{
    public class Commands : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("setup_rules", "Set up rules for the server.")]
        public async Task SetupRulesCommand()
        {
            // Define the modal
            var modal = new ModalBuilder()
                .WithTitle("Server Rules")
                .WithCustomId("setup_rules_modal")
                .AddTextInput("Enter the rules", "rules_input", placeholder: "Type the rules here", maxLength: 1000)
                .Build();

            // Show the modal to the user
            await RespondAsync("Test response");
            //await RespondWithModalAsync(modal);
        }

        [ModalInteraction("setup_rules_modal")]
        public async Task OnRulesModalSubmit(SocketModal modal)
        {
            // Get the rules from the modal
            var rules = modal.Data.Components.FirstOrDefault(x => x.CustomId == "rules_input")?.Value;

            // Find the 'rules' channel
            var rulesChannel = Context.Guild.TextChannels.FirstOrDefault(x => x.Name == "rules");
            if (rulesChannel != null)
            {
                // Create an embed with the rules
                var embed = new EmbedBuilder()
                    .WithTitle("Server Rules")
                    .WithDescription(rules)
                    .WithColor(Color.Blue)
                    .Build();

                // Define the buttons
                var buttonComponent = new ComponentBuilder()
                    .WithButton("I Read the Rules", "read_rules", ButtonStyle.Success)
                    .WithButton("Notify Me", "notify_me", ButtonStyle.Primary)
                    .Build();

               // Send the embed with buttons to the 'rules' channel
               await rulesChannel.SendMessageAsync(embed: embed, components: buttonComponent);
            }
            else
            {
                await RespondAsync("Rules channel not found.", ephemeral: true);
            }
        }
        [SlashCommand("ping", "Pings the bot.")]
    }
}