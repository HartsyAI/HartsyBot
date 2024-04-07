using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Hartsy.Core;
using Microsoft.VisualBasic;
using Supabase.Gotrue;
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
            var message = (Context.Interaction as SocketMessageComponent)?.Message;
            if (message == null || !message.Embeds.Any())
            {
                Console.WriteLine("Message or embeds are null/empty");
                await FollowupAsync("Error: Message or embeds are missing.", ephemeral: true);
                return;
            }

            var embed = message.Embeds.First();
            var (text, description, template) = ParseEmbed(embed);

            var channel = Context.Channel as SocketTextChannel;

            var user = Context.User as SocketGuildUser;

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

            var creditEmbed = new EmbedBuilder()
                    .WithTitle("Image Generation")
                    .WithDescription($"You have {credits} GPUT. You will have {credits - 1} GPUT after this image is generated.")
                    .AddField("Generate Command", "This command allows you to generate images based on the text and template you provide. " +
                    "Each generation will use one GPUT from your account.")
                    .WithColor(Discord.Color.Gold)
                    .WithCurrentTimestamp()
                    .Build();

            await FollowupAsync(embed: creditEmbed, ephemeral: true);
            await _commands.GenerateFromTemplate(text, template, channel, user, description);
        }

        private (string text, string description, string template) ParseEmbed(IEmbed embed)
        {
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

            return (text, description, template);
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
                await RespondAsync("Error: You cannot Showcase another user's image.", ephemeral: true);
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

            // Disable the "Showcase" button
            var componentBuilder = new ComponentBuilder();
            foreach (var actionRow in originalMessage.Components)
            {
                if (actionRow is ActionRowComponent actionRowComponent)
                {
                    foreach (var innerComponent in actionRowComponent.Components)
                    {
                        if (innerComponent is ButtonComponent buttonComponent)
                        {
                            bool isDisabled = buttonComponent.CustomId == $"showcase:{customId}";
                            componentBuilder.WithButton(buttonComponent.Label, buttonComponent.CustomId, buttonComponent.Style, buttonComponent.Emote, url: null, isDisabled);
                        }
                    }
                }
            }

            // Update the original message with the modified components
            await originalMessage.ModifyAsync(msg => msg.Components = componentBuilder.Build());

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
                    await _showcase.UpdateVoteAsync(channel, messageId, Context.User);
                    await RespondAsync("You upvoted this image!", ephemeral: true);
                    break;
                case "down":
                    await _showcase.UpdateVoteAsync(channel, messageId, Context.User);
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

        [ComponentInteraction("choose_image:*")]
        public async Task ChooseImageButtonHandler(string customId)
        {
            Console.WriteLine($"Custom ID: {customId}");
            if (IsOnCooldown(Context.User, "choose_image"))
            {
                await RespondAsync("You are on cooldown. Please wait before trying again.", ephemeral: true);
                return;
            }
            if (Context.User is SocketGuildUser user)
            {
                var userInfo = await _supabaseClient.GetUserByDiscordId(user.Id.ToString());
                if (userInfo == null)
                {
                    var components = new ComponentBuilder()
                    .WithButton("Link Account", style: ButtonStyle.Link, url: "https://hartsy.ai")
                    .Build();

                    var embed = new EmbedBuilder()
                        .WithTitle("Link Your Hartsy.AI Account")
                        .WithDescription($"{user.Mention}, you have not linked your Discord account with your Hartsy.AI account. Make a FREE account " +
                                                            "and log into Hartsy.AI using your Discord credentials. If you have already done that and are still having issues" +
                                                                                                " contact an admin. This may be a bug.")
                        .WithColor(Discord.Color.Blue)
                        .WithTimestamp(DateTimeOffset.Now)
                        .Build();

                    await user.SendMessageAsync(embed: embed, components: components);
                    return;
                }

                var subStatus = userInfo.PlanName;
                if (subStatus == null)
                {
                    await RespondAsync("Error: Subscription status not found.", ephemeral: true);
                    return;
                }

                try
                {
                    string[] splitCustomId = customId.Split(":");
                    ulong userId = ulong.Parse(splitCustomId[1]);
                    string type = splitCustomId[0].ToString();
                    var interaction = Context.Interaction as SocketMessageComponent;
                    string username = interaction.User.Username;
                    ulong messageId = interaction.Message.Id;
                    var selectMenu = new SelectMenuBuilder()
                            .WithPlaceholder("Select an image")
                            .AddOption("Image 1", "image_0")
                            .AddOption("Image 2", "image_1")
                            .AddOption("Image 3", "image_2")
                            .AddOption("Image 4", "image_3");

                    if (type == "i2i")
                    {
                        selectMenu.WithCustomId($"select_image:i2i:{userId}:{messageId}");
                        var selectBuilder = new ComponentBuilder()
                            .WithSelectMenu(selectMenu);
                        var itiEmbed = new EmbedBuilder()
                            .WithTitle("Select Image")
                            .WithDescription("Choose an image and we will generate 4 new images based off of that.")
                            .WithColor(Color.Purple)
                            .WithCurrentTimestamp();

                        await RespondAsync(embed: itiEmbed.Build(), components: selectBuilder.Build(), ephemeral: true);

                        return;
                    }
                    else if (type == "save")
                    {
                        selectMenu.WithCustomId($"select_image:add:{userId}:{messageId}");
                        var selectBuilder = new ComponentBuilder()
                            .WithSelectMenu(selectMenu);
                        var saveEmbed = new EmbedBuilder()
                            .WithTitle("Select Image")
                            .WithDescription("Select the image you wish to save to the gallery")
                            .WithColor(Color.Blue)
                            .WithCurrentTimestamp();

                        await RespondAsync(embed: saveEmbed.Build(), components: selectBuilder.Build(), ephemeral: true);

                        return;
                    }
                }
                catch
                {
                    await RespondAsync("Error: Failed to send a direct message to the user.", ephemeral: true);
                }
            }
        }

        [ComponentInteraction("select_image:*")]
        private async Task HandleImageSelect(string customId, string[] selections)
        {
            await DeferAsync();
            var selectedValue = selections.FirstOrDefault();

            if (!string.IsNullOrEmpty(selectedValue))
            {
                string[] parts = customId.Split(':');
                if (parts.Length >= 4) return;

                var actionType = parts[0]; // Should give "i2i" or "add"
                var userid = parts[1]; // Should give the userId part
                var messageId = parts[2]; // Should give the messageId part

                var interaction = Context.Interaction as SocketMessageComponent;
                var username = interaction.User.Username;
                string userId = interaction.User.Id.ToString();

                if (userId != userid)
                {
                    var errorEmbed = new EmbedBuilder()
                        .WithTitle("Selection Error")
                        .WithDescription("Error: You cannot select another user's image.")
                        .WithColor(Color.Red)
                        .WithCurrentTimestamp();

                    await FollowupAsync(embed: errorEmbed.Build(), ephemeral: true);
                    return;
                }

                var filePath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), $"../../../images/{username}/{messageId}/{messageId}:{selectedValue}.jpeg"));
                // add the base 64 of the image to send to generatefromtemplate
                var initimage = Convert.ToBase64String(File.ReadAllBytes(filePath));

                if (File.Exists(filePath))
                {
                    if (actionType == "i2i")
                    {
                        var message = await Context.Channel.GetMessageAsync(Convert.ToUInt64(messageId)) as IUserMessage;
                        var embed = message.Embeds.First();
                        var (text, description, template) = ParseEmbed(embed);
                        var channel = Context.Channel as SocketTextChannel;

                        var user = Context.User as SocketGuildUser;

                        var userInfo = await _supabaseClient.GetUserByDiscordId(user.Id.ToString());
                        var subStatus = userInfo.PlanName;
                        if (subStatus == null || userInfo.Credit <= 0)
                        {
                            Console.WriteLine($"Subscription status or credit issue. Status: {subStatus}, Credits: {userInfo.Credit}");
                            await _commands.HandleSubscriptionFailure(Context);
                            return;
                        }
                        int credits = userInfo.Credit ?? 0;
                        bool creditUpdated = await _supabaseClient.UpdateUserCredit(user.Id.ToString(), credits - 1);

                        var creditEmbed = new EmbedBuilder()
                                .WithTitle("Image Generation")
                                .WithDescription($"You have {credits} GPUT. You will have {credits - 1} GPUT after this image is generated.")
                                .AddField("Generate Command", "This command allows you to generate images based on the text and template you provide. " +
                                "Each generation will use one GPUT from your account.")
                                .WithColor(Discord.Color.Gold)
                                .WithCurrentTimestamp()
                                .Build();

                        await FollowupAsync(embed: creditEmbed, ephemeral: true);
                        await _commands.GenerateFromTemplate(text, template, channel, user, description, initimage);
                    }
                    else if (actionType == "add")
                    {
                        // TODO: Check if the user has room in the gallery to add the image
                        var supaUser = await _supabaseClient.GetUserByDiscordId(userId);
                        var supaUserId = supaUser.Id;
                        var url = await _supabaseClient.UploadImage(supaUserId, filePath);
                        if (url != null)
                        {
                            _supabaseClient.AddImage(supaUserId, url);
                            // Create an embed builder instance
                            var embed = new EmbedBuilder()
                                .WithTitle("Image Saved Successfully")
                                .WithDescription("Your image has been added to your gallery. You can go to [Hartsy.ai](https://hartsy.ai) to view and download.")
                                .WithColor(Color.Green)
                                .WithCurrentTimestamp();

                            await FollowupAsync(embed: embed.Build(), ephemeral: true);
                        }
                        else
                        {
                            await FollowupAsync("Error saving image.", ephemeral: true);
                        }
                    }
                }
                else
                {
                    await FollowupAsync("Image not found.", ephemeral: true);
                }
            }
            else
            {
                await FollowupAsync("No option selected.", ephemeral: true);
            }
        }
    }
}
