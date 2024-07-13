using Discord;
using Discord.WebSocket;
using Hartsy.Core.SupaBase;
using Hartsy.Core.SupaBase.Models;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using Hartsy.Core.ImageUtil;

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
                // TODO: Check for GIF and handle it differently
                // Load the image, add a watermark, and send it to the showcase channel
                Image<Rgba32> image = await SixLabors.ImageSharp.Image.LoadAsync<Rgba32>(imagePath);
                Image<Rgba32> watermarkedImage = await ImageGrid.AddWatermarkBottomRight(image);
                SupabaseClient supaBase = new();
                Dictionary<string, object>? subStatus = await supaBase.GetSubStatus(user.Id.ToString());
                if (subStatus != null && subStatus["PlanName"].ToString() == "Free")
                {
                    watermarkedImage.Mutate(x => x.Resize(watermarkedImage.Width / 4, watermarkedImage.Height / 4));
                }
                string tempFilePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".jpeg");
                await watermarkedImage.SaveAsJpegAsync(tempFilePath);
                using (FileStream fileStream = new(tempFilePath, FileMode.Open))
                {
                    string filename = Path.GetFileName(tempFilePath);
                    Console.WriteLine($"Showcasing image: {filename}"); // Debugging
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
                    await showcaseChannel.CreateThreadAsync($"Chat about {user.Username}'s Showcased image!", autoArchiveDuration: ThreadArchiveDuration.OneDay, message: message);
                }
                File.Delete(tempFilePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error showcasing image: {ex.Message}");
            }
        }

        private static readonly char[] separatorArray = [','];
        private static readonly char[] separatorArray0 = [','];

        /// <summary>Updates the vote count for a showcased image. This method handles user votes on images displayed in the showcase channel,
        /// ensuring users can vote only once per image. It modifies the embed to reflect the current vote counts and, if the upvotes
        /// reach a specific threshold, sends the image to the "top-hartists" channel, updates the user's credits, 
        /// and sends a congratulatory message.</summary>
        /// <param name="channel">The IMessageChannel where the voting message resides.</param>
        /// <param name="messageId">The ID of the message being voted on.</param>
        /// <param name="user">The IUser who is voting.</param>
        /// <returns>A Task that represents the asynchronous operation of updating the vote on a message.</returns>
        public static async Task UpdateVoteAsync(IMessageChannel channel, ulong messageId, IUser user)
        {
            if (await channel.GetMessageAsync(messageId) is not IUserMessage message) return;
            IEmbed? embed = message.Embeds.FirstOrDefault();
            if (embed == null) return;
            // Download and save image
            string? tempFilePath = await DownloadAndSaveImage(embed.Image?.Url);
            if (tempFilePath == null) return;
            // Update embed with the new image
            await UpdateEmbedWithImage(message, embed, tempFilePath, user);
            File.Delete(tempFilePath);
            // If upvotes reach 5, send to "top-hartists" channel, give the user GPUT, and announce the artist
            if (IsUpvotesThresholdReached(embed, 1))
            {
                SocketGuild? guild = (message.Channel as SocketGuildChannel)?.Guild;
                if (guild == null) return;
                SocketGuildUser? originalUser = await GetOriginalUserFromEmbed(embed, guild);
                if (originalUser == null)
                {
                    Console.WriteLine("Original user not found.");
                    return;
                }
                await SendToTopHartists(guild, message);
                if (await UpdateUserCredits(originalUser))
                {
                    EmbedBuilder congratulatoryEmbed = new EmbedBuilder()
                        .WithTitle("🎉 Congratulations! 🎉")
                        .WithDescription("Your image has reached enough upvotes to be added to the Top Hartists " +
                        "channel. As a reward, you have received **10 Free GPUTs**!")
                        .WithThumbnailUrl(guild.IconUrl)
                        .WithColor(Discord.Color.Gold)
                        .WithFooter(footer => footer.WithText("Keep creating and showcasing your art to earn " +
                        "more rewards!").WithIconUrl(message.Author.GetAvatarUrl()))
                        .AddField("Earn More GPUTs", "The more art you add to showcase, the more chances you have to get free generations. " +
                        "Keep up the great work!");
                    await originalUser.SendMessageAsync(embed: congratulatoryEmbed.Build());
                }
            }
        }

        /// <summary>Downloads an image from a given URL and saves it to a temporary file on the local system. 
        /// The method ensures the file has the correct extensionbased on the original URL. This is useful for 
        /// handling image attachments in Discord messages.</summary>
        /// <param name="imageUrl">The URL of the image to be downloaded.</param>
        /// <returns>A string representing the file path of the saved image, or null if the URL is invalid.</returns>
        private static async Task<string?> DownloadAndSaveImage(string? imageUrl)
        {
            if (string.IsNullOrEmpty(imageUrl)) return null;
            using HttpClient httpClient = new();
            Stream imageStream = await httpClient.GetStreamAsync(imageUrl);
            string tempFilePath = Path.GetTempFileName();
            string fileExtension = Path.GetExtension(new Uri(imageUrl).LocalPath).ToLowerInvariant();
            tempFilePath += fileExtension;
            using (FileStream fileStream = new(tempFilePath, FileMode.Create, FileAccess.Write))
            {
                await imageStream.CopyToAsync(fileStream);
            }
            return tempFilePath;
        }

        /// <summary>Updates the embed of a Discord message with a new image. This method handles modifying the embed's 
        /// fields to reflect the current vote counts,attaching the new image, and updating the message in the channel. 
        /// It ensures the image is correctly attached and displayed within the embed.</summary>
        /// <param name="message">The IUserMessage to be updated.</param>
        /// <param name="embed">The original IEmbed from the message.</param>
        /// <param name="tempFilePath">The file path of the new image to be attached.</param>
        /// <param name="user">The IUser who voted, used to update the vote count in the embed.</param>
        private static async Task UpdateEmbedWithImage(IUserMessage message, IEmbed embed, string tempFilePath, IUser user)
        {
            EmbedBuilder builder = embed.ToEmbedBuilder();
            EmbedFieldBuilder? upvotesField = builder.Fields.FirstOrDefault(f => f.Name == "Upvotes");
            List<string> upvotes = upvotesField != null ? upvotesField.Value.ToString()!.Split(separatorArray, 
                StringSplitOptions.RemoveEmptyEntries).Select(v => v.Trim()).ToList() : [];
            upvotes.Remove("None");
            upvotes.RemoveAll(vote => vote == user.Username);
            upvotes.Add(user.Username);
            builder.Fields[0].WithIsInline(true).WithValue(string.Join(", ", upvotes));
            builder.WithFooter($"Total Votes: {upvotes.Count(upvote => upvote != "None")}");
            string filename = Path.GetFileName(tempFilePath).Replace(":", "_");
            using FileStream newFileStream = new(tempFilePath, FileMode.Open, FileAccess.Read);
            FileAttachment fileAttachment = new(newFileStream, filename);
            builder.WithImageUrl($"attachment://{filename}");
            await message.ModifyAsync(msg =>
            {
                msg.Embed = builder.Build();
                msg.Attachments = new Optional<IEnumerable<FileAttachment>>([fileAttachment]);
            });
        }

        /// <summary>Updates the credits of a user in the database. This method retrieves the user's data from Supabase, 
        /// increments their credit count, updates the record in the database, and verifies the update. 
        /// It handles both retrieval and update of user credits.</summary>
        /// <param name="originalUser">The SocketGuildUser whose credits are to be updated.</param>
        /// <returns>A Task containing a boolean indicating whether the update was successful.</returns>
        private static async Task<bool> UpdateUserCredits(SocketGuildUser originalUser)
        {
            SupabaseClient supabaseClient = new();
            Users? dbUser = await supabaseClient.GetUserByDiscordId(originalUser.Id.ToString());
            if (dbUser != null)
            {
                dbUser.Credit = (dbUser.Credit ?? 0) + 10;
                bool isUpdated = await supabaseClient.UpdateUserCredit(dbUser.ProviderId!, dbUser.Credit.Value);
                return isUpdated;
            }
            Console.WriteLine("User not found in database.");
            return false;
        }

        /// <summary>Extracts and resolves the original user who submitted an image from the embed's description. 
        /// This method parses the username from the description text,
        /// searches the guild for a user matching that username, and returns the corresponding SocketGuildUser.</summary>
        /// <param name="embed">The IEmbed containing the description with the submitter's username.</param>
        /// <param name="guild">The SocketGuild where the user is to be searched.</param>
        /// <returns>A Task containing the SocketGuildUser if found, otherwise null.</returns>
        private static Task<SocketGuildUser?> GetOriginalUserFromEmbed(IEmbed embed, SocketGuild guild)
        {
            string description = embed.Description ?? string.Empty;
            string submittedByPrefix = "Submitted by ";
            int startIndex = description.IndexOf(submittedByPrefix) + submittedByPrefix.Length;
            int endIndex = description.IndexOf('\n', startIndex);
            string username = endIndex == -1 ? description[startIndex..] : description[startIndex..endIndex];
            SocketGuildUser? user = guild.Users.FirstOrDefault(u => u.Username == username);
            return Task.FromResult(user);
        }

        /// <summary>Checks if the upvotes on an embed have reached a specified threshold. 
        /// This method parses the upvotes from the embed's fields, removes any placeholder values, 
        /// and counts the remaining upvotes to determine if the threshold has been met.</summary>
        /// <param name="embed">The IEmbed containing the upvote information.</param>
        /// <param name="threshold">The number of upvotes required to meet the threshold.</param>
        /// <returns>True if the upvotes count meets or exceeds the threshold, otherwise false.</returns>
        private static bool IsUpvotesThresholdReached(IEmbed embed, int threshold)
        {
            EmbedFieldBuilder? upvotesField = embed.ToEmbedBuilder().Fields.FirstOrDefault(f => f.Name == "Upvotes");
            if (upvotesField == null) return false;
            List<string> upvotes = upvotesField.Value.ToString()!.Split(separatorArray0, StringSplitOptions.RemoveEmptyEntries).Select(v => v.Trim()).ToList();
            upvotes.Remove("None");
            return upvotes.Count >= threshold;
        }

        /// <summary>Sends a congratulatory message to the "top-hartists" channel when an image reaches the upvote threshold.
        /// This method searches for the "top-hartists" channel within the guild, constructs an embed message containing
        /// the details of the upvoted image, and sends this embed to the channel.</summary>
        /// <param name="guild">The IGuild object representing the Discord guild where the "top-hartists" channel is located.</param>
        /// <param name="message">The IUserMessage containing the original embed of the upvoted image.</param>
        /// <returns>A Task representing the asynchronous operation.</returns>
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
