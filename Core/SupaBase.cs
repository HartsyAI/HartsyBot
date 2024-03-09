using Postgrest.Attributes;
using Postgrest.Models;
using Supabase;
using Supabase.Gotrue;
using System;
using System.Threading.Tasks;
using static Postgrest.Constants;

public class SupabaseClient
{
    private Supabase.Client supabase;

    public SupabaseClient()
    {
        InitializeSupabase().GetAwaiter().GetResult();
    }

    private async Task InitializeSupabase()
    {
        var url = Environment.GetEnvironmentVariable("SUPABASE_URL");
        var key = Environment.GetEnvironmentVariable("SUPABASE_KEY");
        if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(key))
        {
            throw new InvalidOperationException("Supabase URL or KEY is not set in the environment variables.");
        }

        var options = new SupabaseOptions
        {
            AutoConnectRealtime = true
        };

        supabase = new Supabase.Client(url, key, options);
        await supabase.InitializeAsync();
    }

    public async Task<List<Users>> GetAllUsers()
    {
        try
        {
            var response = await supabase.From<Users>().Get();
            // If no exception is thrown, assume the call was successful
            return response.Models ?? new List<Users>(); // Safely return the list, ensuring it's not null
        }
        catch (Exception ex)
        {
            // Log or handle exceptions here
            Console.WriteLine($"Exception when fetching users: {ex.Message}");
            return new List<Users>(); // Return an empty list in case of error
        }
    }

    public async Task<bool> IsDiscordLinked(string discordId)
    {
        try
        {
            // Check if any user has the provided Discord ID
            var result = await supabase
                .From<Users>()
                .Select("*") // Selects all fields; replace '*' with specific fields as needed
                .Filter("provider_id", Operator.Equals, discordId)
                .Get();

            // If the response contains any users, it means the user's Discord ID is linked
            bool isLinked = result.Models?.Count > 0;

            return isLinked; // Return the result of the check
        }
        catch (Exception ex)
        {
            // Log or handle exceptions here
            Console.WriteLine($"Exception when checking Discord link: {ex.Message}");
            return false; // Return false in case of error
        }
    }

    [Table("users")]
    public class Users : BaseModel
    {
        [PrimaryKey("id", false)]
        public string? Id { get; set; }

        [Column("full_name")]
        public string? Name { get; set; }

        [Column("avatar_url")]
        public string? Avatar_URL { get; set; }

        [Column("billing_address")]
        public string? Billing { get; set; }

        [Column("payment_method")]
        public string? Payment { get; set; }

        [Column("email")]
        public string? Email { get; set; }

        [Column("username")]
        public string? Username { get; set; }

        [Column("likes_count")]
        public int? Likes { get; set; }

        [Column("created_at")]
        public string? Created { get; set; }

        [Column("provider")]
        public string? Provider { get; set; }

        [Column("provider_id")]
        public string? ProviderId { get; set; }

        [Column("credit_limit")]   
        public int? Credit { get; set; }

        [Column("banner_url")]
        public string? Banner { get; set; }
    }
}
