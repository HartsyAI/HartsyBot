using Discord.Interactions;
using Discord;
using static SupabaseClient;
using Discord.WebSocket;

namespace Hartsy.Core
{
    public class AdminCommands : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly SupabaseClient _supabaseClient;

        public AdminCommands(SupabaseClient supabaseClient)
        {
            _supabaseClient = supabaseClient;
        }

        [SlashCommand("add-template", "Add a new template")]
        public async Task AddTemplateCommand()
        {
            // Check if the user has the "HARTSY Staff" role
            var user = Context.User as SocketGuildUser;
            var hasHartsyStaffRole = user.Roles.Any(role => role.Name.Equals("HARTSY Staff", StringComparison.OrdinalIgnoreCase));

            if (!hasHartsyStaffRole)
            {
                await RespondAsync("Only admins can perform this command. Report this with information on how you are even able to see this command!", ephemeral: true);
                return;
            }
            try
            { 
                // Pre-fill the modal with placeholder text
                var addTemplateModal = new AddTemplateModal
                {
                    Name = "Enter the name of the template",
                    Description = "Enter description here",
                    Positive = "(\"__TEXT_REPLACE__\":1.5) (text logo:1.3), "
                };

                // Respond with the modal
                await RespondWithModalAsync("add_template_modal", addTemplateModal);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in AddTemplateCommand: {ex.Message}");
                await RespondAsync("An error occurred while processing your request.", ephemeral: true);
            }
        }

        [ModalInteraction("add_template_modal")]
        public async Task OnTemplateModalSubmit(AddTemplateModal addTemplateModal)
        {
            // Extract the values from the modal
            string name = addTemplateModal.Name;
            string description = addTemplateModal.Description;
            string positive = addTemplateModal.Positive;

            Template newTemplate = new Template
            {
                Prompt = "not used",
                Name = name,
                Description = description,
                Positive = positive,
                Negative = "malformed letters, repeating letters, double letters",
                Checkpoint = "StarlightXL.safetensors",
                Seed = null,
                OrderRank = 1,
                ImageUrl = null,
                CreatedAt = DateTime.UtcNow.ToString(),
                Active = true,
                UserId = null,
            };

            // Save the new template to the database
            await _supabaseClient.AddTemplate(newTemplate);

            await RespondAsync($"Template '{name}' added successfully.", ephemeral: true);
        }

        public class AddTemplateModal : IModal
        {
            public string Title => "Add New Template";

            [InputLabel("Name")]
            [ModalTextInput("name", placeholder: "Template name")]
            public string Name { get; set; }

            [InputLabel("Description")]
            [ModalTextInput("description", TextInputStyle.Paragraph, placeholder: "Template description")]
            public string Description { get; set; }

            [InputLabel("Positive Prompt")]
            [ModalTextInput("positive", TextInputStyle.Paragraph, placeholder: "Positive prompt")]
            public string Positive { get; set; }

            // Constructors, if needed
            public AddTemplateModal() { }
            public AddTemplateModal(string name, string description, string positive)
            {
                Name = name;
                Description = description;
                Positive = positive;
            }
        }

    }
}

