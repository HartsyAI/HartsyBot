using Postgrest.Attributes;
using Postgrest.Models;
using Supabase;
using Supabase.Gotrue;
using System;
using System.Threading.Tasks;
using static Postgrest.Constants;
using System.Text.Json.Serialization;
using System.Text.Json;

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

            return isLinked;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception when checking Discord link: {ex.Message}");
            return false; // Return false in case of error
        }
    }

    public async Task<Users?> GetUserByDiscordId(string discordId)
    {
        var response = await supabase
            .From<Users>()
            .Select("*") // You can specify only necessary fields instead of "*"
            .Filter("provider_id", Operator.Equals, discordId)
            .Single(); // Single as we expect one user

        return response;
    }

    public async Task<Subscriptions?> GetSubscriptionByUserId(string userId)
    {
        try
        {
            Console.WriteLine("\nAttempting to fetch subscription for user ID: " + userId + "\n");

            // Query to get subscription data for a specific user ID
            var response = await supabase.From<Subscriptions>().Get();
            //.Select("*")
            //.Filter("user_id", Operator.Equals, userId)
            //.Filter("price_id", Operator.Equals, price_id)
            //.Single();

            Console.WriteLine($"Subscriptions Table # of Columns: {response.Models.Count}");
            var templates = await supabase.From<Template>().Get();
            Console.WriteLine($"Templates Table # of Columns: {templates.Models.Count}");
            var prices = await supabase.From<Prices>().Get();
            Console.WriteLine($"Prices Table # of Columns: {prices.Models.Count}");

            if (response != null)
            {
                Console.WriteLine("Subscription found for user ID: " + userId);
                Console.WriteLine($"Sub  table response: {response}");
                return response.Model;
            }
            else
            {
                Console.WriteLine("No subscription found for user ID: " + userId);
                return null; // Return null if no subscription is found
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred while fetching subscription for user ID {userId}: {ex.Message}");
            return null; // Return null in case of error
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

    [Table("subscriptions")]
    public class Subscriptions : BaseModel
    {
        [PrimaryKey("id", false)]
        public string? Id { get; set; }

        //[Column("user_id")]
        //public string? UserId { get; set; }

        //[Column("status")]
        //public string? Status { get; set; }

        //[Column("metadata")]
        //public string? MetadataJson { get; set; } // Keeping as string but changing the name for clarity

        //// Not stored in DB, just a convenient way to access the parsed metadata
        //[JsonIgnore] // Make sure this isn't attempted to be mapped by your ORM
        //public Dictionary<string, object>? Metadata
        //{
        //    get
        //    {
        //        if (string.IsNullOrEmpty(MetadataJson)) return null;
        //        try
        //        {
        //            return JsonSerializer.Deserialize<Dictionary<string, object>>(MetadataJson);
        //        }
        //        catch (JsonException)
        //        {
        //            Console.WriteLine("Failed to parse JSON from metadata.");
        //            return null;
        //        }
        //    }
        //}

        //[Column("price_id")]
        //public string? PriceId { get; set; }

        //[Column("quantity")]
        //public int? Quantity { get; set; }

        //[Column("cancel_at_period_end")]
        //public bool? CancelAtPeriodEnd { get; set; }

        //[Column("created")]
        //public DateTime? Created { get; set; }

        //[Column("current_period_start")]
        //public DateTime? CurrentPeriodStart { get; set; }

        //[Column("current_period_end")]
        //public DateTime? CurrentPeriodEnd { get; set; }

        //[Column("ended_at")]
        //public DateTime? EndedAt { get; set; }

        //[Column("cancel_at")]
        //public DateTime? CancelAt { get; set; }

        //[Column("canceled_at")]
        //public DateTime? CanceledAt { get; set; }

        //[Column("trial_start")]
        //public DateTime? TrialStart { get; set; }

        //[Column("trial_end")]
        //public DateTime? TrialEnd { get; set; }

        //[Column("amount")]
        //public int? Amount { get; set; }
    }


    [Table("templates")]
    public class Template : BaseModel
    {
        [PrimaryKey("id", false)]
        public long? Id { get; set; }

        [Column("prompt")]
        public string? Prompt { get; set; }

        [Column("positive")]
        public string? Positive { get; set; }

        [Column("negative")]
        public string? Negative { get; set; }

        [Column("checkpoint")]
        public string? Checkpoint { get; set; }

        [Column("seed")]
        public long? Seed { get; set; }

        [Column("created_at")]
        public string? CreatedAt { get; set; }

        [Column("order_rank")]
        public long? OrderRank { get; set; }

        [Column("active")]
        public bool? Active { get; set; }

        [Column("name")]
        public string? Name { get; set; }

        [Column("description")]
        public string? Description { get; set; }

        [Column("image_url")]
        public string? ImageUrl { get; set; }

        [Column("user_id")]
        public string? UserId { get; set; }
    }

    [Table("prices")]
    public class Prices : BaseModel
    {
        [PrimaryKey("id", false)]
        public string? Id { get; set; }

        [Column("product_id")]
        public string? ProductId { get; set; }

        [Column("active")]
        public bool Active { get; set; }

        [Column("description")]
        public string? Description { get; set; }

        [Column("unit_amount")]
        public long UnitAmount { get; set; }

        [Column("currency")]
        public string? Currency { get; set; }

        [Column("type")]
        public string? Type { get; set; }

        [Column("interval")]
        public string? Interval { get; set; }

        //[Column("interval_count")]
        //public int IntervalCount { get; set; }

        [Column("trial_period_days")]
        public int? TrialPeriodDays { get; set; }

        //[Column("metadata")]
        //public string? Metadata { get; set; }

        [Column("is_default")]
        public bool IsDefault { get; set; }

        [Column("is_metered")]
        public bool IsMetered { get; set; }

        [Column("is_topup")]
        public bool IsTopup { get; set; }
    }

}
