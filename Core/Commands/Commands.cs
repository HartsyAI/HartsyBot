using Discord.Interactions;
using Discord.WebSocket;
using Discord;
using Hartsy.Core.SupaBase;
using Hartsy.Core.SupaBase.Models;

namespace Hartsy.Core.Commands
{
    public abstract class Commands(SupabaseClient supabaseClient, HttpClient httpClient) : InteractionModuleBase<SocketInteractionContext>
    {
        protected readonly SupabaseClient _supabaseClient = supabaseClient;
        protected readonly HttpClient _httpClient = httpClient;

        protected async Task<Users?> GetUserAsync(SocketGuildUser user)
        {
            return await _supabaseClient.GetUserByDiscordId(user.Id.ToString());
        }

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

        protected static EmbedBuilder ErrorEmbed(string thumb)
        {
            return BuildEmbed("Error", "An error occurred: Please contact an admin and describe in detail what you were doing when this happened. " +
                "Also include any other relevent information",
                Color.Red, "Link your account at Hartsy.AI", thumb);
        }
    }
}