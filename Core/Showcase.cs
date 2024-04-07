using Discord;
using Discord.WebSocket;
using System.Net.Mail;

namespace Hartsy.Core
{
    public class Showcase
    {
        private readonly DiscordSocketClient _client;

        public Showcase(DiscordSocketClient client)
        {
            _client = client;
        }

        /// <summary>Showcases an image in the showcase channel. This method is used to display
        /// images in the showcase channel. It creates a new message in the showcase channel with the image attached.</summary>
        /// <param name="guild">The guild where the showcase channel is located.</param>
        /// <param name="imagePath">The path to the image file to showcase.</param>
        /// <param name="user">The user who submitted the image.</param>
        /// <returns>A Task that represents the asynchronous operation of showcasing an image.</returns>
        public async Task ShowcaseImageAsync(IGuild guild, string imagePath, IUser user)
        {
            var channels = await guild.GetChannelsAsync();
            var showcaseChannel = channels.FirstOrDefault(x => x.Name == "showcase") as ITextChannel;
            if (showcaseChannel == null)
            {
                Console.WriteLine("Showcase channel not found.");
                return;
            }

            var components = new ComponentBuilder()
                .WithButton("Up Vote", customId: "vote:up", style: ButtonStyle.Success, emote: new Emoji("\uD83D\uDC4D")) // 👍
                .WithButton("Report", customId: "report:admin", style: ButtonStyle.Secondary, emote: new Emoji("\u26A0")) // ⚠
                .WithButton(" ", customId: $"delete:{user.Id}", style: ButtonStyle.Danger, emote: new Emoji("\uD83D\uDDD1")) // 🗑
                .Build();
            // Load the image file as an attachment
            using var fileStream = new FileStream(imagePath, FileMode.Open);
            var filename = Path.GetFileName(imagePath);
            Console.WriteLine($"Showcasing image: {filename}");
            var embed = new EmbedBuilder()
                .WithTitle("Showcase Image")
                .WithDescription($"Submitted by {user.Username}")
                .WithThumbnailUrl(user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl())
                .WithImageUrl($"attachment://{filename}")
                .AddField("Upvotes", "None", true)
                .WithFooter("Total Votes: 0")
                .Build();

            var fileAttachment = new FileAttachment(fileStream, filename);
            var message = await showcaseChannel.SendFileAsync(attachment: fileAttachment, text: null, embed: embed, components: components);
            await showcaseChannel.CreateThreadAsync($"Discuss Showcase by {user.Username}", autoArchiveDuration: ThreadArchiveDuration.OneDay, message: message);
        }

        /// <summary> Updates the vote count for a showcased image. This method handles user votes on images
        /// displayed in the showcase channel. It ensures users can vote for images,
        /// and that they can only vote once per image. It modifies the embed associated with the image
        /// to reflect the current vote counts and updates the message to show the latest votes.</summary>
        /// <param name="channel">The message channel where the voting message resides.</param>
        /// <param name="messageId">The ID of the message being voted on.</param>
        /// <param name="user">The user who is voting.</param>
        /// <returns>A Task that represents the asynchronous operation of updating the vote on a message.</returns>
        public async Task UpdateVoteAsync(IMessageChannel channel, ulong messageId, IUser user)
        {
            var message = await channel.GetMessageAsync(messageId) as IUserMessage;
            if (message == null) return;

            var embed = message.Embeds.FirstOrDefault();
            if (embed == null) return;

            var builder = embed.ToEmbedBuilder();
            var upvotesField = builder.Fields.FirstOrDefault(f => f.Name == "Upvotes");

            var upvotes = upvotesField != null ? upvotesField.Value.ToString().Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(v => v.Trim()).ToList() : new List<string>();

            upvotes.Remove("None");
            upvotes.RemoveAll(vote => vote == user.Username);
            upvotes.Add(user.Username);

            builder.Fields[0].WithIsInline(true).WithValue(string.Join(", ", upvotes));
            builder.WithFooter($"Total Votes: {upvotes.Count(upvote => upvote != "None")}");

            await message.ModifyAsync(msg => msg.Embed = builder.Build());

            // If upvotes reach 5, send to "top-hartists" channel
            if (upvotes.Count(upvote => upvote != "None") >= 5)
            {
                var channelGuild = message.Channel as SocketGuildChannel;
                var guild = channelGuild.Guild;
                await SendToTopHartists(guild, message);
            }
        }

        /// <summary>Sends the message to the top-hartists channel when an image reaches 5 upvotes.</summary>
        /// <param name="guild"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        private async Task SendToTopHartists(IGuild guild, IUserMessage message)
        {
            var channels = await guild.GetChannelsAsync();
            var topHartistsChannel = channels.FirstOrDefault(x => x.Name == "top-hartists") as ITextChannel;
            if (topHartistsChannel == null)
            {
                Console.WriteLine("Top-Hartists channel not found.");
                return;
            }

            var iEmbed = message.Embeds.FirstOrDefault();
            if (iEmbed != null)
            {
                var embedBuilder = new EmbedBuilder()
                    .WithTitle(iEmbed.Title)
                    .WithDescription(iEmbed.Description)
                    .WithFooter(footer => footer.Text = iEmbed.Footer?.Text)
                    .WithImageUrl(iEmbed.Image?.Url)
                    .WithThumbnailUrl(iEmbed.Thumbnail?.Url)
                    .Build();

                await topHartistsChannel.SendMessageAsync("🌟 A new top artist has been selected! 🌟", embed: embedBuilder);
            }

        }
    }
}
