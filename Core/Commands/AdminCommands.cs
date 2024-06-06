using Discord.Interactions;
using Discord;
using Discord.WebSocket;
using Hartsy.Core.SupaBase;
using Hartsy.Core.InteractionComponents;

namespace Hartsy.Core.Commands
{
    public class AdminCommands(SupabaseClient supabaseClient, HttpClient httpClient) : Commands(supabaseClient, httpClient)
    {

        /// <summary>Initiates adding a new template, accessible only by users with the "HARTSY Staff" role. Displays a modal for entering template details.</summary>
        [SlashCommand("add-template", "Add a new template")]
        [RequireRole("HARTSY Staff")]
        public async Task AddTemplateCommand([Summary("cover_image", "The cover image for the template")] IAttachment attachment)
        {
            if (Context.User is not SocketGuildUser user)
            {
                await RespondAsync("User not found.", ephemeral: true);
                return;
            }
            if (attachment == null)
            {
                await RespondAsync("Please attach a cover image for the template.", ephemeral: true);
                return;
            }
            string filename = attachment.Filename.ToLower();
            if (!(filename.EndsWith(".png") || filename.EndsWith(".jpg") || filename.EndsWith(".jpeg")))
            {
                await RespondAsync("Please upload a valid image file (png, jpg, jpeg).", ephemeral: true);
                return;
            }
            try
            {
                Stream imageStream = await _httpClient.GetStreamAsync(attachment.Url);
                string? imageUrl = await UploadImage(user.Id.ToString(), filename, imageStream);
                if (imageUrl == null)
                {
                    await RespondAsync("Failed to upload image.", ephemeral: true);
                    return;
                }
                Modals.AddTemplateModal addTemplateModal = new()
                {
                    Name = "Enter the name of the template",
                    Description = "Enter description here",
                    Positive = "(\"__TEXT_REPLACE__\":1.5) (text logo:1.3), "
                };
                await RespondWithModalAsync("add_template_modal", addTemplateModal);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in AddTemplateCommand: {ex.Message}");
                await RespondAsync("An error occurred while processing your request.", ephemeral: true);
            }
        }

        /// <summary>Initiates the setup or updating of server rules, accessible only by users with the "HARTSY Staff" role. Displays a modal to enter or update the server rules.</summary>
        [SlashCommand("setup_rules", "Set up rules for the server.")]
        [RequireRole("HARTSY Staff")]
        public async Task SetupRulesCommand()
        {
            try
            {
                ITextChannel? rulesChannel = Context.Guild.TextChannels.FirstOrDefault(x => x.Name == "rules");
                rulesChannel ??= await Context.Guild.CreateTextChannelAsync("rules");
                // Initialize default text
                string defaultDescription = "Default description text",
                    defaultServerRules = "Default server rules text",
                    defaultCodeOfConduct = "Default code of conduct text",
                    defaultOurStory = "Default our story text",
                    defaultButtonFunction = "Default button function description text";
                // Extract text from the last message if available
                IEnumerable<IMessage> messages = await rulesChannel.GetMessagesAsync(1).FlattenAsync();
                IMessage? lastMessage = messages.FirstOrDefault();
                if (lastMessage != null && lastMessage.Embeds.Count != 0)
                {
                    IEmbed embed = lastMessage.Embeds.First();
                    defaultDescription = embed.Description ?? defaultDescription;
                    defaultServerRules = embed.Fields.Length > 0 ? embed.Fields[0].Value : defaultServerRules;
                    defaultCodeOfConduct = embed.Fields.Length > 1 ? embed.Fields[1].Value : defaultCodeOfConduct;
                    defaultOurStory = embed.Fields.Length > 2 ? embed.Fields[2].Value : defaultOurStory;
                    defaultButtonFunction = embed.Fields.Length > 3 ? embed.Fields[3].Value : defaultButtonFunction;
                }
                // Prepare the modal with default text
                Modals.RulesModal rulesModal = new(defaultDescription, defaultServerRules, defaultCodeOfConduct, defaultOurStory, defaultButtonFunction);
                // Respond with the modal
                await RespondWithModalAsync("setup_rules_modal", rulesModal);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
                throw;
            }
        }
    }
}
