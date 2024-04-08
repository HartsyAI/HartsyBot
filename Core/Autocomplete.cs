using Discord.Interactions;
using Discord;

namespace Hartsy.Core
{
    public class TemplateAutocompleteHandler(SupabaseClient supabaseClient) : AutocompleteHandler
    {
        private readonly SupabaseClient _supabaseClient = supabaseClient;

        /// <summary>Provides autocomplete suggestions for template names by querying the list of templates from the Supabase database.</summary>
        public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context, IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services)
        {
            try
            {
                var templates = await _supabaseClient.GetTemplatesAsync();
                if (templates != null)
                {
                    var results = templates.Keys
                        .Select(name => new AutocompleteResult(name, name))
                        .Take(25)
                        .ToList();

                    return AutocompletionResult.FromSuccess(results);
                }

                return AutocompletionResult.FromSuccess([]);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in autocomplete handler: {ex.Message}");
                return AutocompletionResult.FromSuccess([]);
            }
        }
    }
}
