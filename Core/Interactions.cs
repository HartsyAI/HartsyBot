using System.Text.RegularExpressions;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Hartsy.Core;
using Supabase.Interfaces;

namespace HartsyBot.Core
{
    public class InteractionHandlers : InteractionModuleBase<SocketInteractionContext>
    {

        private readonly DiscordSocketClient _client;
        private readonly Showcase _showcase;
        private readonly Commands _commands;
        private readonly SupabaseClient _supabaseClient;

        public InteractionHandlers(DiscordSocketClient client, Showcase showcase, Commands commands, SupabaseClient supabaseClient)
        {
            _client = client;
            _showcase = showcase;
            _commands = commands;
            _supabaseClient = supabaseClient;
        }

        private static readonly Dictionary<(ulong, string), DateTime> _lastInteracted = [];
        private static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(30); // 30 seconds cooldown

        private static bool IsOnCooldown(SocketUser user, string command)
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

            if (rolesToAdd.Count != 0)
            {
                await user.AddRolesAsync(rolesToAdd);
            }

            if (rolesToRemove.Count != 0)
            {
                await user.RemoveRolesAsync(rolesToRemove);
            }

            string response = "";
            if (rolesToAdd.Count != 0)
            {
                response += $"You have been given the {string.Join(", ", rolesToAdd.Select(r => r.Name))} role(s)!\n";
            }
            if (rolesToRemove.Count != 0)
            {
                response += $"The {string.Join(", ", rolesToRemove.Select(r => r.Name))} role(s) have been removed from you!";
            }

            await RespondAsync(response, ephemeral: true);

            // TODO: Add a check if the user has linked their discord account with their Hartsy.AI account and if they are a subscriber
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

        [ComponentInteraction("regenerate:*")]
        public async Task RegenerateButtonHandler(string customId)
        {
            if (Context.User.Id.ToString() != customId)
            {
                Console.WriteLine("Another user tried to click a button");
                await RespondAsync("Error: You cannot regenerate another users image.", ephemeral: true);
                return;
            }

            if (IsOnCooldown(Context.User, "regenerate"))
            {
                await RespondAsync("You are on cooldown. Please wait before trying again.", ephemeral: true);
                return;
            }

            if (Context.Interaction == null)
            {
                Console.WriteLine("Context.Interaction is null");
                await RespondAsync("Error: Interaction context is missing.", ephemeral: true);
                return;
            }

            var interaction = Context.Interaction as SocketMessageComponent;
            if (interaction == null)
            {
                Console.WriteLine("Interaction casting to SocketMessageComponent failed");
                await RespondAsync("Error: Interaction casting issue.", ephemeral: true);
                return;
            }

            var message = interaction.Message;
            if (message == null)
            {
                Console.WriteLine("Interaction.Message is null");
                await RespondAsync("Error: Message context is missing.", ephemeral: true);
                return;
            }

            if (!message.Embeds.Any())
            {
                Console.WriteLine("Message embeds are empty");
                await RespondAsync("Error: No embeds found in the message.", ephemeral: true);
                return;
            }

            var embed = message.Embeds.First();
            string embedDescription = embed.Description ?? "";
            Console.WriteLine($"Embed Description: {embedDescription}");

            // Regular expression checks
            var textPattern = @"\*\*Text:\*\*\s*(.+?)\n\n";
            var descriptionPattern = @"\*\*Extra Description:\*\*\s*(.+?)\n\n";
            var templatePattern = @"\n\n\*\*Template Used:\*\*\s*(.+?)\n\n";

            var textMatch = Regex.Match(embedDescription, textPattern);
            var descriptionMatch = Regex.Match(embedDescription, descriptionPattern);
            var templateMatch = Regex.Match(embedDescription, templatePattern);

            string text = textMatch.Groups[1].Value.Trim();
            string description = descriptionMatch.Groups[1].Value.Trim();
            string template = templateMatch.Groups[1].Value.Trim();

            var channel = Context.Channel as SocketTextChannel;
            if (channel == null)
            {
                Console.WriteLine("Channel casting to SocketTextChannel failed");
                await RespondAsync("Error: Channel casting issue.", ephemeral: true);
                return;
            }

            var user = Context.User as SocketGuildUser;
            if (user == null)
            {
                Console.WriteLine("User casting to SocketGuildUser failed");
                await RespondAsync("Error: User casting issue.", ephemeral: true);
                return;
            }

            var userInfo = await _supabaseClient.GetUserByDiscordId(user.Id.ToString());
            if (userInfo == null)
            {
                Console.WriteLine("userInfo is null - User not found in database.");
                await _commands.HandleSubscriptionFailure(user);
                return;
            }

            var subStatus = userInfo.PlanName;
            if (subStatus == null || userInfo.Credit <= 0)
            {
                Console.WriteLine($"Subscription status or credit issue. Status: {subStatus}, Credits: {userInfo.Credit}");
                await _commands.HandleSubscriptionFailure(user);
                return;
            }
            int credits = userInfo.Credit ?? 0;
            bool creditUpdated = await _supabaseClient.UpdateUserCredit(user.Id.ToString(), credits - 1);

            await RespondAsync($"You have {credits} GPUT. You will have {credits - 1} GPUT after this image is generated.", ephemeral: true);
            await _commands.GenerateImageWithCredits(user, text, template, description, userInfo.Credit.Value);
        }


        [ComponentInteraction("delete:*")]
        public async Task DeleteButtonHandler(string customId)
        {
            if (IsOnCooldown(Context.User, "delete"))
            {
                await RespondAsync("You are on cooldown. Please wait before trying again.", ephemeral: true);
                return;
            }
            if (Context.User.Id.ToString() != customId)
            {
                Console.WriteLine("Another user tried to click a button");
                await RespondAsync("Error: You cannot delete another users image.", ephemeral: true);
                return;
            }

            await DeferAsync();

            // Delete the original message
            await (Context.Interaction as SocketMessageComponent)?.Message.DeleteAsync();

            // Respond with a followup message
            await FollowupAsync("Message deleted successfully", ephemeral: true);
        }

        [ComponentInteraction("showcase:*")]
        public async Task ShowcaseButtonHandler(string customId)
        {
            if (IsOnCooldown(Context.User, "showcase"))
            {
                await RespondAsync("You are on cooldown. Please wait before trying again.", ephemeral: true);
                return;
            }

            if (Context.User.Id.ToString() != customId)
            {
                Console.WriteLine("Another user tried to click a button");
                await RespondAsync("Error: You cannot Showcase another users image.", ephemeral: true);
                return;
            }

            Console.WriteLine("Handling showcase interaction.");
            await DeferAsync(); // Defer the response

            var client = Context.Client as DiscordSocketClient;
            if (client == null)
            {
                Console.WriteLine("Discord client not available.");
                await FollowupAsync("Error: Discord client not available.", ephemeral: true);
                return;
            }
            var originalMessage = (Context.Interaction as SocketMessageComponent)?.Message as IUserMessage;

            if (originalMessage == null)
            {
                Console.WriteLine("Original message not found.");
                await FollowupAsync("Original message not found.", ephemeral: true);
                return;
            }

            var embed = originalMessage.Embeds.FirstOrDefault();
            if (embed == null || string.IsNullOrEmpty(embed.Image?.Url))
            {
                Console.WriteLine("No image found in the original message.");
                await FollowupAsync("No image found in the original message.", ephemeral: true);
                return;
            }

            Console.WriteLine("Calling ShowcaseImageAsync.");
            await _showcase.ShowcaseImageAsync(Context.Guild, embed.Image.Value.Url, Context.User);
            await FollowupAsync("Image added to the showcase!", ephemeral: true);
        }

        [ComponentInteraction("vote:*")]
        public async Task VoteButtonHandler(string customId)
        {
            if (IsOnCooldown(Context.User, "vote"))
            {
                await RespondAsync("You are on cooldown. Please wait before trying again.", ephemeral: true);
                return;
            }
            var channel = Context.Channel;
            var messageId = (Context.Interaction as SocketMessageComponent).Message.Id;

            switch (customId)
            {
                case "up":
                    await _showcase.UpdateVoteAsync(channel, messageId, Context.User, "upvote");
                    await RespondAsync("You upvoted this image!", ephemeral: true);
                    break;
                case "down":
                    await _showcase.UpdateVoteAsync(channel, messageId, Context.User, "downvote");
                    await RespondAsync("You downvoted this image!", ephemeral: true);
                    break;
                default:
                    await RespondAsync("Invalid vote.", ephemeral: true);
                    break;
            }
        }

        [ComponentInteraction("report:*")]
        public async Task ReportButtonHandler()
        {
            if (IsOnCooldown(Context.User, "report"))
            {
                await RespondAsync("You are on cooldown. Please wait before trying again.", ephemeral: true);
                return;
            }
            // TODO: disable button so it cannot be reported twice
            await DeferAsync();
            // disable the button

            // Send a follow-up message to the user
            await FollowupAsync("Reported to admins.", ephemeral: true);

            // TODO: Implement the actual reporting logic
        }

        [ComponentInteraction("link_account")]
        public async Task LinkAccountButtonHandler()
        {
            if (IsOnCooldown(Context.User, "link_account"))
            {
                await RespondAsync("You are on cooldown. Please wait before trying again.", ephemeral: true);
                return;
            }
        }
    }
}
