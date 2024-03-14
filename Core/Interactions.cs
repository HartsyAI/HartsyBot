using System.Text.RegularExpressions;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Hartsy.Core;

namespace HartsyBot.Core
{
    public class InteractionHandlers : InteractionModuleBase<SocketInteractionContext>
    {

        private readonly DiscordSocketClient _client;
        private readonly Showcase _showcase;

        public InteractionHandlers(DiscordSocketClient client, Showcase showcase)
        {
            _client = client;
            _showcase = showcase;
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

        [ComponentInteraction("regenerate")]
        public async Task RegenerateButtonHandler()
        {
            if (IsOnCooldown(Context.User, "regenerate"))
            {
                await RespondAsync("You are on cooldown. Please wait before trying again.", ephemeral: true);
                return;
            }
            await RespondAsync("Regenerating...",ephemeral: true);
            // run the GenerateFromTemplate Task
            var interaction = Context.Interaction as SocketMessageComponent;
            var message = interaction.Message;
            // Get the first embed
            var embed = message.Embeds.First();

            // Embed description
            string embedDescription = embed.Description;

            // Regular expression patterns to extract text, description, and template
            var textPattern = @"\*\*Text:\*\*\s*(.+?)\n\n";
            var descriptionPattern = @"\*\*Extra Description:\*\*\s*(.+?)\n\n";
            var templatePattern = @"\n\n\*\*Template Used:\*\*\s*(.+?)\n\n";

            // Match and extract information using Regex
            var textMatch = Regex.Match(embedDescription, textPattern);
            var descriptionMatch = Regex.Match(embedDescription, descriptionPattern);
            var templateMatch = Regex.Match(embedDescription, templatePattern);

            // Extracted values
            string text = textMatch.Groups[1].Value.Trim();
            string description = descriptionMatch.Groups[1].Value.Trim();
            string template = templateMatch.Groups[1].Value.Trim();

            var channel = Context.Channel as SocketTextChannel;
            var user = (SocketGuildUser)Context.User;
            await Core.Commands.GenerateImages.GenerateFromTemplate(text, template, channel, user, description);
            // TODO: Break up the generate_logo command into smaller functions so that we can call them here

        }

        [ComponentInteraction("delete")]
        public async Task DeleteButtonHandler()
        {
            if (IsOnCooldown(Context.User, "delete"))
            {
                await RespondAsync("You are on cooldown. Please wait before trying again.", ephemeral: true);
                return;
            }

            // TODO: Make it so only the user who sent the message can delete it

            await DeferAsync(); // Defer the response

            // Delete the original message
            await (Context.Interaction as SocketMessageComponent)?.Message.DeleteAsync();

            // Respond with a followup message
            await FollowupAsync("Message deleted successfully", ephemeral: true);
        }

        [ComponentInteraction("showcase:*")]
        public async Task ShowcaseButtonHandler()
        {
            if (IsOnCooldown(Context.User, "showcase"))
            {
                await RespondAsync("You are on cooldown. Please wait before trying again.", ephemeral: true);
                return;
            }

            // TODO: Make it so only the user who sent the message can showcase it

            Console.WriteLine("Handling showcase interaction."); // Log the beginning of the interaction
            await DeferAsync(); // Defer the response

            var client = Context.Client as DiscordSocketClient;
            if (client == null)
            {
                Console.WriteLine("Discord client not available."); // Log the client availability issue
                await FollowupAsync("Error: Discord client not available.", ephemeral: true);
                return;
            }

            //var originalMessage = await Context.Channel.GetMessageAsync(numericMessageId) as IUserMessage;
            //var originalMessage = await Context.Channel.GetMessageAsync(Context.Interaction.Id) as IUserMessage;
            var originalMessage = (Context.Interaction as SocketMessageComponent)?.Message as IUserMessage;

            if (originalMessage == null)
            {
                Console.WriteLine("Original message not found."); // Log that the original message was not found
                await FollowupAsync("Original message not found.", ephemeral: true);
                return;
            }

            var embed = originalMessage.Embeds.FirstOrDefault();
            if (embed == null || string.IsNullOrEmpty(embed.Image?.Url))
            {
                Console.WriteLine("No image found in the original message."); // Log that no image was found
                await FollowupAsync("No image found in the original message.", ephemeral: true);
                return;
            }

            Console.WriteLine("Calling ShowcaseImageAsync."); // Log that we're calling the ShowcaseImageAsync method
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
    }
}
