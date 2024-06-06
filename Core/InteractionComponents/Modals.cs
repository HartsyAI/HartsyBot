using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Hartsy.Core.SupaBase;
using Hartsy.Core.SupaBase.Models;

namespace Hartsy.Core.InteractionComponents
{
    public class Modals(SupabaseClient supaBase) : InteractionModuleBase<SocketInteractionContext>
    {
        /// <summary>Handles the submission of the add template modal and saves the new template details to the database.</summary>
        /// <param name="addTemplateModal">The modal containing the template details.</param>
        [ModalInteraction("add_template_modal")]
        public async Task OnTemplateModalSubmit(AddTemplateModal addTemplateModal)
        {
            // Extract the values from the modal
            string name = addTemplateModal.Name ?? "Template Name";
            string description = addTemplateModal.Description ?? "Template description";
            string positive = addTemplateModal.Positive ?? "Positive prompt";
            Template newTemplate = new()
            {
                Prompt = "not used",
                Name = name,
                Description = description,
                Positive = positive,
                Negative = "malformed letters, repeating letters, double letters",
                Checkpoint = "StarlightXL.safetensors",
                Seed = null,
                OrderRank = 1,
                ImageUrl = "https://yfixpvzkurnytlvefeos.supabase.co/storage/v1/object/public/assets/TemplateGens/Placeholder/Placeholder.jpg?t=2024-04-05T03%3A29%3A44.676Z",
                CreatedAt = DateTime.UtcNow,
                Active = true,
                UserId = null,
                Loras = []
            };
            // Save the new template to the database
            await supaBase.AddTemplate(newTemplate);

            await RespondAsync($"Template '{name}' added successfully.", ephemeral: true);
        }

        /// <summary>Represents a modal for adding a new template, containing input fields for the template's name, description, and positive prompt.</summary>
        public class AddTemplateModal : IModal
        {
            public string Title => "Add New Template";

            [InputLabel("Name")]
            [ModalTextInput("name", placeholder: "Template name")]
            public string? Name { get; set; }

            [InputLabel("Description")]
            [ModalTextInput("description", TextInputStyle.Paragraph, placeholder: "Template description")]
            public string? Description { get; set; }

            [InputLabel("Positive Prompt")]
            [ModalTextInput("positive", TextInputStyle.Paragraph, placeholder: "Positive prompt")]
            public string? Positive { get; set; }

            // Constructors, if needed
            public AddTemplateModal() { }
            public AddTemplateModal(string name, string description, string positive)
            {
                Name = name;
                Description = description;
                Positive = positive;
            }
        }

        /// <summary>Handles the submission of the server rules setup modal and updates the rules message in the specified channel.</summary>
        /// <param name="modal">The modal containing the updated rules data.</param>
        [ModalInteraction("setup_rules_modal")]
        public async Task OnRulesModalSubmit(RulesModal modal)
        {
            await DeferAsync(ephemeral: true);
            try
            {
                // Extract the data from the modal
                string description = modal.Description ?? "";
                string server_rules = modal.Server_rules ?? "";
                string codeOfConduct = modal.CodeOfConduct ?? "";
                string ourStory = modal.OurStory ?? "";
                string ButtonFunction = modal.ButtonFunction ?? "";
                string imagePath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "images", "server_rules.png");
                FileStream stream = new(imagePath, FileMode.Open);
                // Construct the embed with fields from the modal
                Embed embed = new EmbedBuilder()
                    .WithTitle("Welcome to the Hartsy.AI Discord Server!")
                    .WithDescription(modal.Description)
                    .AddField("Server Rules", modal.Server_rules)
                    .AddField("Code of Conduct", modal.CodeOfConduct, true)
                    .AddField("Our Story", modal.OurStory, true)
                    .AddField("What Does This Button Do?", modal.ButtonFunction, true)
                    .WithColor(Color.Blue)
                    .WithCurrentTimestamp()
                    .WithImageUrl("attachment://server_rules.png")
                    .WithFooter("Click the buttons to add roles")
                    .Build();
                // Find the 'rules' channel
                SocketTextChannel? rulesChannel = Context.Guild.TextChannels.FirstOrDefault(x => x.Name == "rules");
                if (rulesChannel != null)
                {
                    // Check for the last message in the 'rules' channel
                    IEnumerable<IMessage> messages = await rulesChannel.GetMessagesAsync(1).FlattenAsync();
                    var lastMessage = messages.FirstOrDefault();
                    // If there's an existing message, delete it
                    if (lastMessage != null)
                    {
                        await lastMessage.DeleteAsync();
                    }
                    // Define the buttons
                    MessageComponent buttonComponent = new ComponentBuilder()
                        .WithButton("I Read the Rules", "read_rules", ButtonStyle.Success)
                        .WithButton("Don't Notify Me", "notify_me", ButtonStyle.Primary)
                        .Build();
                    // Send the new embed with buttons to the 'rules' channel
                    await rulesChannel.SendFileAsync(stream, "server_rules.png", text: null, embed: embed, components: buttonComponent);
                    await FollowupAsync("Rules have been updated!", ephemeral: true);
                }
                else
                {
                    await FollowupAsync("Rules channel not found.", ephemeral: true);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in modal submission: {ex.Message}");
                await FollowupAsync("An error occurred while processing your request.", ephemeral: true);
            }
        }

        /// <summary>Represents a modal for setting up or updating server rules, containing input fields for the server's description, 
        /// rules, code of conduct, our story, and button function explanation.</summary>
        public class RulesModal : IModal
        {
            public string Title => "Server Rules";

            [InputLabel("Description")]
            [ModalTextInput("description_input", TextInputStyle.Paragraph,
            placeholder: "Enter a brief description", maxLength: 300)]
            public string? Description { get; set; }

            [InputLabel("Server Rules")]
            [ModalTextInput("server_rules", TextInputStyle.Paragraph,
            placeholder: "List the server rules here", maxLength: 800)]
            public string? Server_rules { get; set; }

            [InputLabel("Code of Conduct")]
            [ModalTextInput("code_of_conduct_input", TextInputStyle.Paragraph,
            placeholder: "Describe the code of conduct", maxLength: 400)]
            public string? CodeOfConduct { get; set; }

            [InputLabel("Our Story")]
            [ModalTextInput("our_story_input", TextInputStyle.Paragraph,
            placeholder: "Share the story of your community", maxLength: 400)]
            public string? OurStory { get; set; }

            [InputLabel("What Does This Button Do?")]
            [ModalTextInput("button_function_description_input", TextInputStyle.Paragraph,
            placeholder: "Explain the function of this button", maxLength: 200)]
            public string? ButtonFunction { get; set; }

            // Constructors
            public RulesModal() { /* ... */ }
            public RulesModal(string description, string server_rules, string codeOfConduct, string ourStory, string buttonFunction)
            {
                // Initialize with provided values
                Description = description;
                Server_rules = server_rules;
                CodeOfConduct = codeOfConduct;
                OurStory = ourStory;
                ButtonFunction = buttonFunction;
            }
        }
    }
}
