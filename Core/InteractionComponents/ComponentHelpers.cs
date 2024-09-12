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
            string text = "";
            string description = "";
            string template = "";
            string templateDescription = "";
            foreach (EmbedField field in embed.Fields)
            {
                switch (field.Name)
                {
                    case "Text":
                        text = field.Value.TrimStart('`').TrimEnd('`');
                        break;
                    case "Description":
                        description = field.Value.TrimStart('`').TrimEnd('`');
                        break;
                    case "Template":
                        template = field.Value.TrimStart('`').TrimEnd('`');
                        break;
                    case "Template Description":
                        templateDescription = field.Value.TrimStart('`').TrimEnd('`');
                        break;
                }
            }
            // Debugging: Output the parsed values
            Console.WriteLine($"Parsed Text: {text}");
            Console.WriteLine($"Parsed Description: {description}");
            Console.WriteLine($"Parsed Template: {template}");
            Console.WriteLine($"Parsed Template Description: {templateDescription}");
            return (text, description, template);
        }
    }
}
