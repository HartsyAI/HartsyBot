using System.Text.RegularExpressions;
using Discord;
using Discord.WebSocket;

namespace Hartsy.Core.InteractionComponents
{
    public class ComponentHelpers
    {
        private static readonly Dictionary<(ulong, string), DateTime> _lastInteracted = [];
        private static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(3); // 3 seconds cooldown

        /// <summary>Checks if a user is on cooldown for a specific command.</summary>
        /// <param name="user">The user to check for cooldown.</param>
        /// <param name="command">The command to check for cooldown.</param>
        /// <returns>True if the user is on cooldown; otherwise, false.</returns>
        public static bool IsOnCooldown(SocketUser user, string command)
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

        /// <summary>Parses the embed to extract text, description, and template information.</summary>
        /// <param name="embed">The embed to parse.</param>
        /// <returns>A tuple containing the text, description, and template extracted from the embed.</returns>
        public static (string text, string description, string template) ParseEmbed(IEmbed embed)
        {
            string embedDescription = embed.Description ?? "";
            string textPattern = @"\*\*Text:\*\*\s*(.+?)\n\n";
            string descriptionPattern = @"\*\*Extra Description:\*\*\s*(.+?)\n\n";
            string templatePattern = @"\n\n\*\*Template Used:\*\*\s*(.+?)\n\n";
            Match textMatch = Regex.Match(embedDescription, textPattern);
            Match descriptionMatch = Regex.Match(embedDescription, descriptionPattern);
            Match templateMatch = Regex.Match(embedDescription, templatePattern);
            string text = textMatch.Groups[1].Value.Trim();
            string description = descriptionMatch.Groups[1].Value.Trim();
            string template = templateMatch.Groups[1].Value.Trim();
            return (text, description, template);
        }
    }
}
