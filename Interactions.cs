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
            var memberRole = Context.Guild.Roles.FirstOrDefault(r => r.Name == "Member");
            var announcementRole = Context.Guild.Roles.FirstOrDefault(r => r.Name == "Announcement");
            var user = (SocketGuildUser)Context.User;

            if (IsOnCooldown(Context.User, "read_rules"))
            {
                await RespondAsync("You are on cooldown. Please wait before trying again.", ephemeral: true);
                return;
            }

            var rolesToAdd = new List<IRole>();
            var rolesToRemove = new List<IRole>();

            if (memberRole != null)
            {
                if (!user.Roles.Contains(memberRole))
                    rolesToAdd.Add(memberRole);
                else
                    rolesToRemove.Add(memberRole);
            }

            if (announcementRole != null)
            {
                if (!user.Roles.Contains(announcementRole))
                    rolesToAdd.Add(announcementRole);
                else
                    rolesToRemove.Add(announcementRole);
            }

            if (rolesToAdd.Any())
            {
                await user.AddRolesAsync(rolesToAdd);
            }

            if (rolesToRemove.Any())
            {
                await user.RemoveRolesAsync(rolesToRemove);
            }

            string response = "";
            if (rolesToAdd.Any())
            {
                response += $"You have been given the {string.Join(", ", rolesToAdd.Select(r => r.Name))} role(s)!\n";
            }
            if (rolesToRemove.Any())
            {
                response += $"The {string.Join(", ", rolesToRemove.Select(r => r.Name))} role(s) have been removed from you!";
            }

            await RespondAsync(response, ephemeral: true);
        }


        [ComponentInteraction("notify_me")]
        public async Task NotifyMeButtonHandler()
        {
            var role = Context.Guild.Roles.FirstOrDefault(r => r.Name == "Announcement");
            var user = (SocketGuildUser)Context.User;
            if (IsOnCooldown(Context.User, "notify_me"))
            {
                await RespondAsync("You are on cooldown. Please wait before trying again.", ephemeral: true);
                return;
            }
            if (role != null && user.Roles.Contains(role))
            {
                await user.RemoveRoleAsync(role);
                await RespondAsync("The 'Announcement' role has been removed from you!", ephemeral: true);
            }
            else
            {
                await user.AddRoleAsync(role);
                await RespondAsync("You have been given the 'Announcement' role!", ephemeral: true);
            }
        }
    }
}
