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
        private static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(3); // 3 seconds cooldown

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
            await DeferAsync();
            if (Context.User.Id.ToString() != customId)
            {
                Console.WriteLine("Another user tried to click a button");
                await FollowupAsync("Error: You cannot regenerate another users image.", ephemeral: true);
                return;
            }

            if (IsOnCooldown(Context.User, "regenerate"))
            {
                await FollowupAsync ("You are on cooldown. Please wait before trying again.", ephemeral: true);
                return;
            }

            if (Context.Interaction == null)
            {
                Console.WriteLine("Context.Interaction is null");
                await FollowupAsync("Error: Interaction context is missing.", ephemeral: true);
                return;
            }

            var interaction = Context.Interaction as SocketMessageComponent;
            if (interaction == null)
            {
                Console.WriteLine("Interaction casting to SocketMessageComponent failed");
                await FollowupAsync("Error: Interaction casting issue.", ephemeral: true);
                return;
            }

            var message = interaction.Message;
            if (message == null)
            {
                Console.WriteLine("Interaction.Message is null");
                await FollowupAsync("Error: Message context is missing.", ephemeral: true);
                return;
            }

            if (!message.Embeds.Any())
            {
                Console.WriteLine("Message embeds are empty");
                await FollowupAsync("Error: No embeds found in the message.", ephemeral: true);
                return;
            }

            var embed = message.Embeds.First();
            string embedDescription = embed.Description ?? "";

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
                await FollowupAsync("Error: Channel casting issue.", ephemeral: true);
                return;
            }

            var user = Context.User as SocketGuildUser;
            if (user == null)
            {
                Console.WriteLine("User casting to SocketGuildUser failed");
                await FollowupAsync("Error: User casting issue.", ephemeral: true);
                return;
            }

            var userInfo = await _supabaseClient.GetUserByDiscordId(user.Id.ToString());
            if (userInfo == null)
            {
                Console.WriteLine("userInfo is null - User not found in database.");
                await _commands.HandleSubscriptionFailure(Context);
                return;
            }

            var subStatus = userInfo.PlanName;
            if (subStatus == null || userInfo.Credit <= 0)
            {
                Console.WriteLine($"Subscription status or credit issue. Status: {subStatus}, Credits: {userInfo.Credit}");
                await _commands.HandleSubscriptionFailure(Context);
                return;
            }
            int credits = userInfo.Credit ?? 0;
            bool creditUpdated = await _supabaseClient.UpdateUserCredit(user.Id.ToString(), credits - 1);

            await FollowupAsync($"You have {credits} GPUT. You will have {credits - 1} GPUT after this image is generated.", ephemeral: true);
            await _commands.GenerateImageWithCredits(Context, text, template, description);
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
        public async Task ReportButtonHandler(string userId)
        {
            var user = Context.User as SocketGuildUser;
            var guild = Context.Guild;

            if (IsOnCooldown(user, "report"))
            {
                await RespondAsync("You are on cooldown. Please wait before trying again.", ephemeral: true);
                return;
            }
            var message = (Context.Interaction as SocketMessageComponent)?.Message;
            var GetEmbed = message?.Embeds.FirstOrDefault();
            var staffChannel = guild.TextChannels.FirstOrDefault(c => c.Name == "staff-chat-🔒");


            if (message != null && staffChannel != null)
            {
                var embed = new EmbedBuilder()
                    .WithTitle("Reported Message")
                    .WithDescription($"A message has been reported by {user.Mention}. " +
                    $"\n\n<@{userId}> may have created an image that breaks the community rules. A mod needs to look at this ASAP!")
                    .AddField("Reported by", user.Mention, true)
                    .AddField("Message Link", $"[Jump to message]({message.GetJumpUrl()})", true)
                    .WithColor(Color.Red)
                    .WithTimestamp(DateTimeOffset.Now)
                    .Build();

                // Send a detailed report to the staff channel
                await staffChannel.SendMessageAsync(embed: embed);

                // Disable the button on the reported message
                var component = new ComponentBuilder()
                    .WithButton("Reported", "report", ButtonStyle.Danger, disabled: true)
                    .Build();
                await (message as IUserMessage)?.ModifyAsync(msg => msg.Components = component);

                var response = new EmbedBuilder()
                    .WithTitle("Message Reported")
                    .WithDescription($"{user.Mention}, Thank you for reporting this message. Our community's safety and integrity are of utmost importance to us.")
                    .AddField("Report Received", "Your report has been successfully submitted to our staff team.")
                    .AddField("Next Steps", "A staff member will review the reported content shortly. If they determine that it violates our community rules, " +
                    "appropriate actions will be taken to address the issue. Deletion of the post has been disabled while staff looks into the issue.")
                    .WithFooter("Thank you for helping to maintain a safe and respectful environment. If you have any further information please contact a mod.")
                    .WithColor(Discord.Color.Gold)
                    .WithCurrentTimestamp()
                    .Build();

                // Send the embed in the original channel
                await RespondAsync(embed: response, ephemeral: true);

            }
            else
            {
                await RespondAsync("Failed to report the message. Please try again or contact an admin.", ephemeral: true);
            }
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
