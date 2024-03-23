using Discord;
using Discord.WebSocket;

namespace Hartsy.Core
{
    public class Showcase
    {
        private readonly DiscordSocketClient _client;

        public Showcase(DiscordSocketClient client)
        {
            _client = client;
        }
        public async Task ShowcaseImageAsync(IGuild guild, string imageUrl, IUser user)
        {
            Console.WriteLine($"ShowcaseImageAsync called with image URL: {imageUrl}"); // Log the start of the method

            var channels = await guild.GetChannelsAsync();
            var showcaseChannel = channels.FirstOrDefault(x => x.Name == "showcase") as ITextChannel;
            if (showcaseChannel == null)
            {
                Console.WriteLine("Showcase channel not found."); // Log that the showcase channel was not found
                return;
            }

            var components = new ComponentBuilder()
                .WithButton("Vote", customId: "vote:up", style: ButtonStyle.Success, emote: new Emoji("\uD83D\uDC4D")) // 👍
                .WithButton("Vote", customId: "vote:down", style: ButtonStyle.Danger, emote: new Emoji("\uD83D\uDC4E")) // 👎
                .WithButton("Report", customId: "report:admin", style: ButtonStyle.Secondary, emote: new Emoji("\u26A0")) // ⚠
                .WithButton(" ", customId: $"delete:{user.Id}", style: ButtonStyle.Secondary, emote: new Emoji("\uD83D\uDDD1")); // 🗑

            var embed = new EmbedBuilder()
                .WithTitle("Showcase Image")
                .WithDescription($"Submitted by {user.Username}")
                .WithImageUrl(imageUrl)
                .WithThumbnailUrl(user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl())
                .AddField("Upvotes", "None", true)
                .AddField("Downvotes", "None", true)
                .WithFooter("Total Votes: 0")
                .Build();

            Console.WriteLine("Sending message to showcase channel."); // Log that we're sending a message
            var message = await showcaseChannel.SendMessageAsync(embed: embed);
            await message.ModifyAsync(msg => msg.Components = components.Build());
            Console.WriteLine("Message sent and components modified.");
            await showcaseChannel.CreateThreadAsync($"Discuss Showcase by {user.Username}", autoArchiveDuration: ThreadArchiveDuration.OneDay, message: message);
        }

        /// <summary> Updates the vote count for a showcased image. This method handles user votes on images
        /// displayed in the showcase channel. It ensures users can vote for or against images,
        /// and that they can only vote once per image. It modifies the embed associated with the image
        /// to reflect the current vote counts and updates the message to show the latest votes.</summary>
        /// <param name="channel">The message channel where the voting message resides.</param>
        /// <param name="messageId">The ID of the message being voted on.</param>
        /// <param name="user">The user who is voting.</param>
        /// <param name="voteType">The type of vote being cast. Should be "upvote" or "downvote".</param>
        /// <returns>A Task that represents the asynchronous operation of updating the vote on a message.</returns>
        public async Task UpdateVoteAsync(IMessageChannel channel, ulong messageId, IUser user, string voteType)
        {
            var message = await channel.GetMessageAsync(messageId) as IUserMessage;
            if (message == null) return;

            var embed = message.Embeds.FirstOrDefault();
            if (embed == null) return;

            var builder = embed.ToEmbedBuilder();
            var upvotesField = builder.Fields.FirstOrDefault(f => f.Name == "Upvotes");
            var downvotesField = builder.Fields.FirstOrDefault(f => f.Name == "Downvotes");

            var upvotes = upvotesField != null ? upvotesField.Value.ToString().Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(v => v.Trim()).ToList() : new List<string>();
            var downvotes = downvotesField != null ? downvotesField.Value.ToString().Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(v => v.Trim()).ToList() : new List<string>();

            // Remove "None" if it exists
            upvotes.Remove("None");
            downvotes.Remove("None");

            // Remove previous votes (if any)
            upvotes.RemoveAll(vote => vote == user.Username);
            downvotes.RemoveAll(vote => vote == user.Username);

            // Add new vote
            if (voteType == "upvote") upvotes.Add(user.Username);
            if (voteType == "downvote") downvotes.Add(user.Username);

            // If lists are empty after removing/adding votes, add "None" back
            if (!upvotes.Any()) upvotes.Add("None");
            if (!downvotes.Any()) downvotes.Add("None");

            // Update embed fields
            builder.Fields[0].WithIsInline(true).WithValue(string.Join(", ", upvotes));
            builder.Fields[1].WithIsInline(true).WithValue(string.Join(", ", downvotes));
            builder.WithFooter($"Total Votes: {upvotes.Count(upvote => upvote != "None") - downvotes.Count(downvote => downvote != "None")}");

            await message.ModifyAsync(msg => msg.Embed = builder.Build());
        }
    }
}
