using Discord.Interactions;
using Discord.WebSocket;
using Discord;
using Hartsy.Core.SupaBase;
using Hartsy.Core.SupaBase.Models;

namespace Hartsy.Core.Commands
{
    /// <summary>Represents an abstract base class for command modules. It inherits from Discord's InteractionModuleBase 
    /// to facilitate the creation of Discord interaction commands.</summary>
    public abstract class Commands(SupabaseClient supabaseClient, HttpClient httpClient) : InteractionModuleBase<SocketInteractionContext>
    {
        protected readonly SupabaseClient _supabaseClient = supabaseClient;
        protected readonly HttpClient _httpClient = httpClient;

        /// <summary>Retrieves a user's data from Supabase based on their Discord ID. This method is useful for fetching
        /// additional user information stored in Supabase, such as credits or profile details, that are not available through Discord alone.</summary>
        /// <param name="user">The SocketGuildUser whose data is to be fetched from Supabase.</param>
        /// <returns>A Task containing a Users object if the user is found, otherwise null.</returns>
        protected async Task<Users?> GetUserAsync(SocketGuildUser user)
        {
            return await _supabaseClient.GetUserByDiscordId(user.Id.ToString());
        }

        /// <summary>Uploads an image to Supabase storage. This method takes an image stream, saves it temporarily on the local system,
        /// uploads it to Supabase, and returns the URL of the uploaded image. It ensures the temporary file is deleted after the upload.</summary>
        /// <param name="userId">The ID of the user uploading the image.</param>
        /// <param name="templateName">The name of the template associated with the image.</param>
        /// <param name="imageStream">The stream of the image to be uploaded.</param>
        /// <returns>A Task containing the URL of the uploaded image if successful, otherwise null.</returns>
        protected async Task<string?> UploadImage(string userId, string templateName, Stream imageStream)
        {
            using MemoryStream memoryStream = new();
            await imageStream.CopyToAsync(memoryStream);
            string tempFilePath = Path.GetTempFileName();
            await File.WriteAllBytesAsync(tempFilePath, memoryStream.ToArray());
            string? imageUrl = await _supabaseClient.UploadImage(userId, templateName);
            File.Delete(tempFilePath);
            return imageUrl;
        }

        /// <summary>Constructs a customizable Discord embed message. This method allows you to specify the title, description, color,
        /// footer text, thumbnail URL, and additional fields. It is useful for creating rich, formatted messages to display in Discord channels.</summary>
        /// <param name="title">The title of the embed.</param>
        /// <param name="description">The description text of the embed.</param>
        /// <param name="color">The color of the embed border.</param>
        /// <param name="footer">The footer text of the embed.</param>
        /// <param name="thumbnailUrl">The URL of the thumbnail image for the embed.</param>
        /// <param name="fields">Optional dictionary of fields to add to the embed, with field names as keys and field values as values.</param>
        /// <returns>An EmbedBuilder object configured with the specified parameters.</returns>
        protected static EmbedBuilder BuildEmbed(string title, string description, Color color, string footer, string thumbnailUrl, Dictionary<string, string>? fields = null)
        {
            EmbedBuilder embedBuilder = new EmbedBuilder()
                .WithTitle(title)
                .WithDescription(description)
                .WithThumbnailUrl(thumbnailUrl)
                .WithColor(color)
                .WithTimestamp(DateTimeOffset.Now)
                .WithFooter(footer);
            if (fields != null)
            {
                foreach (KeyValuePair<string, string> field in fields)
                {
                    embedBuilder.AddField(field.Key, field.Value);
                }
            }
            return embedBuilder;
        }

        /// <summary>Creates a standard error embed message. This method is used to generate a consistent error message format
        /// for users, instructing them to contact an admin and provide details about the encountered issue.</summary>
        /// <param name="thumb">The URL of the thumbnail image to be included in the error embed.</param>
        /// <returns>An EmbedBuilder object configured to display an error message with the specified thumbnail.</returns>
        protected static EmbedBuilder ErrorEmbed(string thumb)
        {
            return BuildEmbed("Error", "An error occurred: Please contact an admin and describe in detail what you were doing when this happened. " +
                "Also include any other relevent information",
                Color.Red, "Link your account at Hartsy.AI", thumb);
        }
    }
}