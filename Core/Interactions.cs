using System.Text.RegularExpressions;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Supabase.Gotrue;

namespace Hartsy.Core
{
    public class InteractionHandlers(Commands commands, SupabaseClient supabaseClient) : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly Commands _commands = commands;
        private readonly SupabaseClient _supabaseClient = supabaseClient;
        private static readonly Dictionary<(ulong, string), DateTime> _lastInteracted = [];
        private static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(3); // 3 seconds cooldown

        /// <summary>Checks if a user is on cooldown for a specific command.</summary>
        /// <param name="user">The user to check for cooldown.</param>
        /// <param name="command">The command to check for cooldown.</param>
        /// <returns>True if the user is on cooldown; otherwise, false.</returns>
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

        /// <summary>Handles the interaction when the 'read_rules' button is clicked, assigning or removing roles as necessary.</summary>
        /// <returns>A task representing the asynchronous operation.</returns>
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

        /// <summary>Handles the interaction when the 'notify_me' button is clicked, toggling the 'Announcement' role for the user.</summary>
        /// <returns>A task representing the asynchronous operation.</returns>
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

        /// <summary>Handles the interaction when the 'regenerate' button is clicked, regenerating the image based on the original parameters.</summary>
        /// <param name="customId">The custom ID associated with the button that triggered the interaction.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
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
            if (message == null || message.Embeds.Count == 0)
            {
                Console.WriteLine("Message or embeds are null/empty");
                await FollowupAsync("Error: Message or embeds are missing.", ephemeral: true);
                return;
            }

            var embed = message.Embeds.First();
            var (text, description, template) = ParseEmbed(embed);

            var channel = Context.Channel as SocketTextChannel;

            var user = Context.User as SocketGuildUser;

            var userInfo = await _supabaseClient.GetUserByDiscordId(user?.Id.ToString() ?? "");
            if (userInfo == null)
            {
                Console.WriteLine("userInfo is null - User not found in database.");
                await Commands.HandleSubscriptionFailure(Context);
                return;
            }

            var subStatus = userInfo.PlanName;
            if (subStatus == null || userInfo.Credit <= 0)
            {
                Console.WriteLine($"Subscription status or credit issue. Status: {subStatus}, Credits: {userInfo.Credit}");
                await Commands.HandleSubscriptionFailure(Context);
                return;
            }
            int credits = userInfo.Credit ?? 0;
            await _supabaseClient.UpdateUserCredit(user?.Id.ToString() ?? "", credits - 1);

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

        /// <summary>Parses the embed to extract text, description, and template information.</summary>
        /// <param name="embed">The embed to parse.</param>
        /// <returns>A tuple containing the text, description, and template extracted from the embed.</returns>
        private static (string text, string description, string template) ParseEmbed(IEmbed embed)
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

        /// <summary>Handles the interaction when the 'delete' button is clicked, removing the associated message.</summary>
        /// <param name="customId">The custom ID associated with the button that triggered the interaction.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
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
            SocketMessageComponent? interaction = Context.Interaction as SocketMessageComponent;

            // Delete the original message
            await (interaction?.Message.DeleteAsync()!);

            // Respond with a followup message
            await FollowupAsync("Message deleted successfully", ephemeral: true);
        }

        /// <summary>Handles the interaction when the 'showcase' button is clicked, preparing a selection menu for showcasing an image.</summary>
        /// <param name="customId">The custom ID associated with the button that triggered the interaction.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
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
                await RespondAsync("Error: You cannot showcase another user's image.", ephemeral: true);
                return;
            }
            await DeferAsync(); // Defer the response
            var originalMessage = (Context.Interaction as SocketMessageComponent)?.Message as IUserMessage;

            // Create a select menu for the user to choose an image to showcase
            var selectMenu = new SelectMenuBuilder()
                .WithCustomId($"select_image:showcase:{Context.User.Id}:{originalMessage?.Id}")
                .WithPlaceholder("Select an image to add to the showcase channel")
                .AddOption("Image 1", "image_0")
                .AddOption("Image 2", "image_1")
                .AddOption("Image 3", "image_2")
                .AddOption("Image 4", "image_3");

            var selectBuilder = new ComponentBuilder()
                .WithSelectMenu(selectMenu);

            var showcaseEmbed = new EmbedBuilder()
                .WithTitle("Select Image for Showcase")
                .WithDescription("Select an image to add to the #showcase channel. Other users will be able to vote on your image.")
                .WithColor(Color.Blue)
                .WithCurrentTimestamp();

            await FollowupAsync(embed: showcaseEmbed.Build(), components: selectBuilder.Build(), ephemeral: true);

            // TODO: Fix it so showcase button is disabled after image is showcased

            // Update the original message with the modified components
            //await originalMessage.ModifyAsync(msg => msg.Components = componentBuilder.Build());

            //Console.WriteLine("Calling ShowcaseImageAsync.");
            //await _showcase.ShowcaseImageAsync(Context.Guild, embed.Image.Value.Url, Context.User);
            //await FollowupAsync("Image added to the showcase!", ephemeral: true);
        }

        /// <summary>Handles the interaction when a vote button is clicked, updating the vote count for an image.</summary>
        /// <param name="customId">The custom ID associated with the button that triggered the interaction.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [ComponentInteraction("vote:*")]
        public async Task VoteButtonHandler(string customId)
        {
            if (IsOnCooldown(Context.User, "vote"))
            {
                await RespondAsync("You are on cooldown. Please wait before trying again.", ephemeral: true);
                return;
            }
            ISocketMessageChannel channel = Context.Channel;
            SocketMessageComponent? interaction = Context.Interaction as SocketMessageComponent;
            ulong messageId = interaction!.Message.Id;

            switch (customId)
            {
                case "up":
                    await Showcase.UpdateVoteAsync(channel, messageId, Context.User);
                    await RespondAsync("You upvoted this image!", ephemeral: true);
                    break;
                case "down":
                    await Showcase.UpdateVoteAsync(channel, messageId, Context.User);
                    await RespondAsync("You downvoted this image!", ephemeral: true);
                    break;
                default:
                    await RespondAsync("Invalid vote.", ephemeral: true);
                    break;
            }
        }

        /// <summary>Handles the interaction when the 'report' button is clicked, notifying staff of a reported image.</summary>
        /// <param name="userId">The user ID associated with the button that triggered the interaction.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [ComponentInteraction("report:*")]
        public async Task ReportButtonHandler(string userId)
        {
            SocketGuildUser? user = Context.User as SocketGuildUser;
            var guild = Context.Guild;

            if (IsOnCooldown(user!, "report"))
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
                    .WithDescription($"A message has been reported by {user!.Mention}. " +
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
                await (message as IUserMessage)?.ModifyAsync(msg => msg.Components = component)!;

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

        /// <summary>Handles the interaction when a user selects an image, providing options based on the action type.</summary>
        /// <param name="customId">The custom ID associated with the select menu that triggered the interaction.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
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
                        .WithColor(Color.Blue)
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
                    SocketMessageComponent? interaction = Context.Interaction as SocketMessageComponent;
                    string username = interaction!.User.Username;
                    ulong messageId = interaction!.Message.Id;
                    SelectMenuBuilder selectMenu = new SelectMenuBuilder()
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

        /// <summary>Handles the interaction when an image is selected for a specific action, such as showcasing or saving to gallery.</summary>
        /// <param name="customId">The custom ID associated with the select menu that triggered the interaction.</param>
        /// <param name="selections">The selections made by the user in the select menu.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [ComponentInteraction("select_image:*")]
#pragma warning disable IDE0051 // Remove unused private members
        private async Task HandleImageSelect(string customId, string[] selections)
#pragma warning restore IDE0051 // Remove unused private members
        {
            await DeferAsync();
            var selectedValue = selections.FirstOrDefault();

            if (!string.IsNullOrEmpty(selectedValue))
            {
                string[] parts = customId.Split(':');
                if (parts.Length >= 4) return;

                string actionType = parts[0]; // Should give "i2i" or "add"
                string userid = parts[1]; // Should give the userId part
                string messageId = parts[2]; // Should give the messageId part

                SocketMessageComponent? interaction = Context.Interaction as SocketMessageComponent;
                string username = interaction!.User.Username;
                string userId = interaction.User.Id.ToString();

                if (userId != userid)
                {
                    EmbedBuilder errorEmbed = new EmbedBuilder()
                        .WithTitle("Selection Error")
                        .WithDescription("Error: You cannot select another user's image.")
                        .WithColor(Color.Red)
                        .WithCurrentTimestamp();

                    await FollowupAsync(embed: errorEmbed.Build(), ephemeral: true);
                    return;
                }

                string filePath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), $"../../../images/{username}/{messageId}/{messageId}:{selectedValue}.jpeg"));
                // add the base 64 of the image to send to generatefromtemplate
                string initimage = Convert.ToBase64String(File.ReadAllBytes(filePath));

                if (File.Exists(filePath))
                {
                    if (actionType == "i2i")
                    {
                        IUserMessage? message = await Context.Channel.GetMessageAsync(Convert.ToUInt64(messageId)) as IUserMessage;
                        IEmbed embed = message!.Embeds.First();
                        var (text, description, template) = ParseEmbed(embed);
                        SocketTextChannel? channel = Context.Channel as SocketTextChannel;

                        SocketGuildUser? user = Context.User as SocketGuildUser;

                        SupabaseClient.Users? userInfo = await _supabaseClient.GetUserByDiscordId(user!.Id.ToString());
                        var subStatus = userInfo!.PlanName;
                        if (subStatus == null || userInfo.Credit <= 0)
                        {
                            Console.WriteLine($"Subscription status or credit issue. Status: {subStatus}, Credits: {userInfo.Credit}");
                            await Commands.HandleSubscriptionFailure(Context);
                            return;
                        }
                        int credits = userInfo.Credit ?? 0;
                        await _supabaseClient.UpdateUserCredit(user.Id.ToString(), credits - 1);

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
                        SupabaseClient.Users? supaUser = await _supabaseClient.GetUserByDiscordId(userId);
                        string? supaUserId = supaUser?.Id;
                        string url = await _supabaseClient.UploadImage(supaUserId!, filePath);
                        if (url != null)
                        {
                            await _supabaseClient.AddImage(supaUserId!, url);
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
                    else if (actionType == "showcase")
                    {
                        await Showcase.ShowcaseImageAsync(Context.Guild, filePath, Context.User);
                        await FollowupAsync("Image added to the showcase!", ephemeral: true);
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
