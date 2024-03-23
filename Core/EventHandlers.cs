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
            string[] channelNames = { "welcome", "rules", "generate", "info" };
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
            channels.TryGetValue("info", out var infoChannel);

            // Check if the user has linked their Discord account
            bool isLinked = await _supabaseClient.IsDiscordLinked(user.Id.ToString());

            if (welcomeChannel != null && rulesChannel != null && generateChannel != null)
            {
                // Path to the image
                var imagePath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "images", "welcome.png");
                // Open the image file
                using var stream = new FileStream(imagePath, FileMode.Open);

                var welcomeEmbed = new EmbedBuilder()
                    .WithTitle("Welcome to Hartsy.AI!")
                    .WithDescription($"{user.Mention}, we're thrilled to have you join the **Hartsy.AI** Discord Server!")
                    .AddField("Getting Started", $"Please check out the <#{rulesChannel.Id}> for our community guidelines and the " +
                    $"<#{infoChannel.Id}> for information on how to get the most out of our server.")
                    .AddField("Using the Bot", $"You can use our custom bot in the <#{generateChannel.Id}> channel to generate images. " +
                    $"Each image generation will consume one GPUT from your account.")
                    .AddField("About GPUTs", "GPUTs (GPU Time) are used as tokens for generating images. " +
                    "If you need more, you can purchase additional GPUTs on our website. You can make a 1 time purchase or choose a subscription")
                    .WithFooter("Enjoy your stay and unleash your creativity with Hartsy.AI!")
                    .WithColor(Discord.Color.Blue)
                    .WithThumbnailUrl(user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl())
                    .WithCurrentTimestamp()
                    .Build();


                var button = new ComponentBuilder()
                    .WithButton("Link Discord Account", style: ButtonStyle.Link, url: "https://hartsy.ai")
                    .Build();

                await welcomeChannel.SendFileAsync(stream, "welcome.png", embed: welcomeEmbed, components: button);
                if (!isLinked)
                {
                    // Send a direct message to the user
                    var notLinkedEmbed = new EmbedBuilder()
                        .WithTitle("Link Your Hartsy.AI Account")
                        .WithDescription($"{user.Mention}, you have not linked your Discord account with your Hartsy.AI account. Make a FREE account " +
                                         "and log into Hartsy.AI using your Discord credentials. If you have already done that and are still having issues" +
                                         " contact an admin. This may be a bug.")
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
