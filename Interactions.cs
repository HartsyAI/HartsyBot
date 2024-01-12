using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace HartsyBot
{
    public class InteractionHandlers : InteractionModuleBase<SocketInteractionContext>
    {
        private static readonly Dictionary<(ulong, string), DateTime> _lastInteracted = new Dictionary<(ulong, string), DateTime>();
        private static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(30); // 30 seconds cooldown

        private bool IsOnCooldown(SocketUser user, string command)
        {
            var key = (user.Id, command);
            if (_lastInteracted.TryGetValue(key, out var lastInteraction))
            {
                if (DateTime.UtcNow - lastInteraction < Cooldown)
                {
                    return true;
                }
            }
            _lastInteracted[key] = DateTime.UtcNow;
            return false;
        }

        [ComponentInteraction("read_rules")]
        public async Task ReadRulesButtonHandler()
        {
            var role = Context.Guild.Roles.FirstOrDefault(r => r.Name == "Member");
            var user = (SocketGuildUser)Context.User;
            if (IsOnCooldown(Context.User, "read_rules"))
            {
                await RespondAsync("You are on cooldown. Please wait before trying again.", ephemeral: true);
                return;
            }
            if (role != null && !user.Roles.Contains(role))
            {
                await user.AddRoleAsync(role);
                await RespondAsync("You have been given the 'Member' role!", ephemeral: true);
                return;
            }
            else
            {
                await user.RemoveRoleAsync(role);
                await RespondAsync("The 'Member' role has been removed from you!", ephemeral: true);
                return;
            }
        }

        [ComponentInteraction("notify_me")]
        public async Task NotifyMeButtonHandler()
        {
            var role = Context.Guild.Roles.FirstOrDefault(r => r.Name == "Member");
            var user = (SocketGuildUser)Context.User;
            if (IsOnCooldown(Context.User, "notify_me"))
            {
                await RespondAsync("You are on cooldown. Please wait before trying again.", ephemeral: true);
                return;
            }
            if (role != null && !user.Roles.Contains(role))
            {
                await user.AddRoleAsync(role);
                await RespondAsync("You have been given the 'Announcements' role!", ephemeral: true);
            }
            else
            {
                await user.RemoveRoleAsync(role);
                await RespondAsync("The 'Announcements' role has been removed from you!", ephemeral: true);
            }
        }
    }
}
