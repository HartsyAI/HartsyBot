using Discord;
using Discord.WebSocket;
using Hartsy.Core.SupaBase;
using Hartsy.Core.SupaBase.Models;

namespace Hartsy.Core
{
    public class Showcase()
    {
        /// <summary>Showcases an image in the showcase channel. This method is used to display
        /// images in the showcase channel. It creates a new message in the showcase channel with the image attached.</summary>
        /// <param name="guild">The guild where the showcase channel is located.</param>
        /// <param name="imagePath">The path to the image file to showcase.</param>
        /// <param name="user">The user who submitted the image.</param>
        /// <returns>A Task that represents the asynchronous operation of showcasing an image.</returns>
        public static async Task ShowcaseImageAsync(IGuild guild, string imagePath, IUser user)
        {
            IReadOnlyCollection<IGuildChannel> channels = await guild.GetChannelsAsync();
            if (channels.FirstOrDefault(x => x.Name == "showcase") is not ITextChannel showcaseChannel)
            {
                Console.WriteLine("Showcase channel not found.");
                return;
            }
            MessageComponent components = new ComponentBuilder()
                .WithButton("Up Vote", customId: "vote:up", style: ButtonStyle.Success, emote: new Emoji("\uD83D\uDC4D")) // 👍
                .WithButton("Report", customId: "report:admin", style: ButtonStyle.Secondary, emote: new Emoji("\u26A0")) // ⚠
                .WithButton(" ", customId: $"delete:{user.Id}", style: ButtonStyle.Danger, emote: new Emoji("\uD83D\uDDD1")) // 🗑
                .Build();
            try
            {
                // Load the image file as an attachment
                using FileStream fileStream = new(imagePath, FileMode.Open);
                string filename = Path.GetFileName(imagePath);
                filename = filename.Replace(":", "_");
                Embed embed = new EmbedBuilder()
                    .WithTitle("Showcase Image")
                    .WithDescription($"Submitted by {user.Username}")
                    .WithThumbnailUrl(user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl())
                    .WithImageUrl($"attachment://{filename}")
                    .AddField("Upvotes", "None", true)
                    .WithFooter("Total Votes: 0")
                    .Build();
                FileAttachment fileAttachment = new(fileStream, filename);
                IUserMessage message = await showcaseChannel.SendFileAsync(attachment: fileAttachment, text: null, embed: embed, components: components);
                await showcaseChannel.CreateThreadAsync($"Discuss Showcase by {user.Username}", autoArchiveDuration: ThreadArchiveDuration.OneDay, message: message);
            }
            catch (Exception ex)
            {
                   Console.WriteLine($"Error showcasing image: {ex.Message}");
            }
        }

        private static readonly char[] separator = [','];

        /// <summary> Updates the vote count for a showcased image. This method handles user votes on images
        /// displayed in the showcase channel. It ensures users can vote for images,
        /// and that they can only vote once per image. It modifies the embed associated with the image
        /// to reflect the current vote counts and updates the message to show the latest votes.</summary>
        /// <param name="channel">The message channel where the voting message resides.</param>
        /// <param name="messageId">The ID of the message being voted on.</param>
        /// <param name="user">The user who is voting.</param>
        /// <returns>A Task that represents the asynchronous operation of updating the vote on a message.</returns>
        public static async Task UpdateVoteAsync(IMessageChannel channel, ulong messageId, IUser user)
        {
            if (await channel.GetMessageAsync(messageId) is not IUserMessage message) return;
            IEmbed? embed = message.Embeds.FirstOrDefault();
            if (embed == null) return;
            // Download the existing image to a temp file
            string? imageUrl = embed.Image?.Url;
            if (string.IsNullOrEmpty(imageUrl)) return;
            using HttpClient httpClient = new();
            Stream imageStream = await httpClient.GetStreamAsync(imageUrl);
            string tempFilePath = Path.GetTempFileName();
            // Remove query parameters to get the correct file extension
            string fileExtension = Path.GetExtension(new Uri(imageUrl).LocalPath).ToLowerInvariant();
            tempFilePath += fileExtension;
            using (FileStream fileStream = new(tempFilePath, FileMode.Create, FileAccess.Write))
            {
                await imageStream.CopyToAsync(fileStream);
            }
            EmbedBuilder builder = embed.ToEmbedBuilder();
            EmbedFieldBuilder? upvotesField = builder.Fields.FirstOrDefault(f => f.Name == "Upvotes");
            List<string> upvotes = upvotesField != null ? upvotesField.Value.ToString()!.Split(separator, 
                StringSplitOptions.RemoveEmptyEntries).Select(v => v.Trim()).ToList() : [];
            upvotes.Remove("None");
            upvotes.RemoveAll(vote => vote == user.Username);
            upvotes.Add(user.Username);
            builder.Fields[0].WithIsInline(true).WithValue(string.Join(", ", upvotes));
            builder.WithFooter($"Total Votes: {upvotes.Count(upvote => upvote != "None")}");
            List<IAttachment> attachments = [.. message.Attachments];
            // Re-upload the image
            string filename = Path.GetFileName(new Uri(imageUrl).LocalPath); // Get the original filename with extension
            string sanitizedFilename = filename.Replace(":", "_");
            using (FileStream newFileStream = new(tempFilePath, FileMode.Open, FileAccess.Read))
            {
                FileAttachment fileAttachment = new(newFileStream, sanitizedFilename);
                builder.WithImageUrl($"attachment://{sanitizedFilename}");
                await message.ModifyAsync(msg =>
                {
                    msg.Embed = builder.Build();
                    msg.Attachments = new Optional<IEnumerable<FileAttachment>>([fileAttachment]);
                });
            }
            File.Delete(tempFilePath);

            // If upvotes reach 5, send to "top-hartists" channel, give the user GPUT, and announce the artist
            if (upvotes.Count(upvote => upvote != "None") == 1)
            {
                // TODO: I should do this a better less hacky way

                string description = embed.Description ?? string.Empty;
                string submittedByPrefix = "Submitted by ";
                int startIndex = description.IndexOf(submittedByPrefix) + submittedByPrefix.Length;
                int endIndex = description.IndexOf('\n', startIndex);
                string username = endIndex == -1 ? description.Substring(startIndex) : description.Substring(startIndex, endIndex - startIndex);

                Console.WriteLine($"User: {username}"); // Debugging
                                                        // Resolve the username to a Discord user
                SocketGuild? guild = message.Channel is SocketGuildChannel channelGuild ? channelGuild.Guild : null;
                SocketGuildUser? originalUser = guild!.Users.FirstOrDefault(u => u.Username == username);
                if (originalUser == null)
                {
                    Console.WriteLine("Original user not found.");
                    return;
                }
                else
                {
                    Console.WriteLine($"Original user: {originalUser.Username}");
                }
                SupabaseClient supabaseClient = new();
                Users? dbUser = await supabaseClient.GetUserByDiscordId(originalUser.Id.ToString());
                if (dbUser != null)
                {
                    Console.WriteLine($"Credit: {dbUser.Credit}"); // Debugging
                    dbUser.Credit = (dbUser.Credit ?? 0) + 10;
                    Console.WriteLine($"New credit: {dbUser.Credit}"); // Debugging
                    await supabaseClient.UpdateUserCredit(dbUser.ProviderId!, dbUser.Credit.Value);
                    Console.WriteLine($"User credit updated: {dbUser.Credit.Value}");
                }
                else
                {
                    Console.WriteLine("User not found in database.");
                }
                await SendToTopHartists(guild, message);
            }
        }

        /// <summary>Sends the message to the top-hartists channel when an image reaches 5 upvotes.</summary>
        /// <param name="guild"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        private static async Task SendToTopHartists(IGuild guild, IUserMessage message)
        {
            IReadOnlyCollection<IGuildChannel> channels = await guild.GetChannelsAsync();
            if (channels.FirstOrDefault(x => x.Name == "top-hartists") is not ITextChannel topHartistsChannel)
            {
                Console.WriteLine("Top-Hartists channel not found.");
                return;
            }
            IEmbed? iEmbed = message.Embeds.FirstOrDefault();
            if (iEmbed != null)
            {
                Embed embedBuilder = new EmbedBuilder()
                    .WithTitle("🌟 A new top artist has been selected! 🌟")
                    .WithDescription(iEmbed.Description)
                    .WithFooter(footer => footer.Text = iEmbed.Footer?.Text)
                    .WithImageUrl(iEmbed.Image?.Url)
                    .WithThumbnailUrl(iEmbed.Thumbnail?.Url)
                    .Build();
                await topHartistsChannel.SendMessageAsync(embed: embedBuilder);
            }
        }
    }
}
