using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace HartsyBot
{
    public class InteractionHandlers : InteractionModuleBase<SocketInteractionContext>
    {
        // Handler for a button interaction
        [ComponentInteraction("read_rules")]
        public async Task ReadRulesButtonHandler()
        {
            var role = Context.Guild.Roles.FirstOrDefault(r => r.Name == "Member");
            var user = (SocketGuildUser)Context.User;

            if (role != null && !user.Roles.Contains(role))
            {
                await user.AddRoleAsync(role);
                await RespondAsync("You have been given the 'Member' role!", ephemeral: true);
            }
        }

        [ComponentInteraction("notify_me")]
        public async Task NotifyMeButtonHandler()
        {
            var role = Context.Guild.Roles.FirstOrDefault(r => r.Name == "Announcements");
            var user = (SocketGuildUser)Context.User;

            if (role != null && !user.Roles.Contains(role))
            {
                await user.AddRoleAsync(role);
                await RespondAsync("You have been given the 'Announcements' role!", ephemeral: true);
            }
        }
    }
}