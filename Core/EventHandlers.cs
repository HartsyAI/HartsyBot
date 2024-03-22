using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Supabase;
using Supabase.Interfaces;
using System;

namespace HartsyBot.Core
{
    public class EventHandlers
    {
        private readonly DiscordSocketClient _client;
        private readonly InteractionService _interactions;
        private readonly SupabaseClient _supabaseClient;

        public EventHandlers(DiscordSocketClient client, InteractionService interactions, SupabaseClient supabaseClient)
        {
            _client = client;
            _interactions = interactions;
            _supabaseClient = supabaseClient;
        }

        public void RegisterHandlers()
        {
            _client.UserJoined += OnUserJoinedAsync;
        }

        private async Task OnUserJoinedAsync(SocketGuildUser user)
        {
            string[] channelNames = { "welcome", "rules", "generate" };
            Dictionary<string, SocketTextChannel> channels = new Dictionary<string, SocketTextChannel>();

            foreach (var name in channelNames)
            {
                var channel = user.Guild.TextChannels.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (channel != null)
                {
                    channels[name] = channel;
                }
            }
            channels.TryGetValue("welcome", out var welcomeChannel);
            channels.TryGetValue("rules", out var rulesChannel);
            channels.TryGetValue("generate", out var generateChannel);

            // Check if the user has linked their Discord account
            bool isLinked = await _supabaseClient.IsDiscordLinked(user.Id.ToString());

            if (welcomeChannel != null && rulesChannel != null && generateChannel != null)
            {
                // Path to the image
                var imagePath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "images", "welcome.png");
                // Open the image file
                using var stream = new FileStream(imagePath, FileMode.Open);

                string welcomeMessage = $"{user.Mention}, welcome to the **Hartsy.AI** Discord Server! " +
                    $"Check out the <#{rulesChannel.Id}> channel. " +
                    $"Use our custom Discord bot to generate images in the <#{generateChannel.Id}> channel.";

                var embed = new EmbedBuilder()
                    .WithTitle("Welcome to Hartsy.AI")
                    .WithDescription(welcomeMessage)
                    .WithColor(Discord.Color.Blue)
                    .WithTimestamp(DateTimeOffset.Now)
                    .Build();

                var button = new ComponentBuilder()
                    .WithButton("Link Account", style: ButtonStyle.Link, url: "https://hartsy.ai")
                    .Build();

                await welcomeChannel.SendFileAsync(stream, "welcome.png", embed: embed, components: button);
                if (!isLinked)
                {
                    // Send a direct message to the user
                    var notLinkedEmbed = new EmbedBuilder()
                        .WithTitle("Link Your Hartsy.AI Account")
                        .WithDescription($"{user.Mention}, you have not linked your Discord account with your Hartsy.AI account. Make a FREE account " +
                                         "and log into Hartsy.AI using your Discord credentials.")
                        .WithColor(Discord.Color.Blue)
                        .WithTimestamp(DateTimeOffset.Now)
                        .Build();
                    try
                    {
                        await user.SendMessageAsync(embed: notLinkedEmbed, components: button);
                    }
                    catch
                    {
                        // If DM fails, send the message in the welcome channel
                        await welcomeChannel.SendMessageAsync(embed: notLinkedEmbed, components: button);
                    }
                    return;
                }
                await AssignRoleBasedOnSubscription(user);
            }
        }

        private async Task AssignRoleBasedOnSubscription(SocketGuildUser user)
        {
            var userStatus = await _supabaseClient.GetSubStatus(user.Id.ToString());
            if (userStatus != null && userStatus.ContainsKey("PlanName"))
            {
                string subStatus = userStatus["PlanName"].ToString() ?? "Free";
                var subRole = user.Guild.Roles.FirstOrDefault(role => role.Name.Equals(subStatus, StringComparison.OrdinalIgnoreCase));
                await user.AddRoleAsync(subRole);
            }
        }
    }
}
