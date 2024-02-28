using Postgrest.Attributes;
using Postgrest.Models;
using Supabase;
using System;
using System.Threading.Tasks;

public class SupabaseClient
{
    private Client supabase;

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

        var supabase = new Supabase.Client(url, key, options);
        await supabase.InitializeAsync();
    }

    [Table("users")]
    public class Users : BaseModel
    {
        [PrimaryKey("id", false)]
        public int Id { get; set; }

        [Column("full_name")]
        public string Name { get; set; }

        [Column("avatar_url")]
        public int CountryId { get; set; }

        [Column("billing_address")]
        public string Billing { get; set; }

        [Column("payment_method")]
        public string Payment { get; set; }

        [Column("email")]
        public string Email { get; set; }

        [Column("username")]
        public int Username { get; set; }

        [Column("likes_count")]
        public string Likes { get; set; }

        [Column("created_at")]
        public string Created { get; set; }

        [Column("provider")]
        public string Provider { get; set; }

        [Column("provider_id")]
        public string ProviderId { get; set; }

        [Column("credit_limit")]   
        public string Credit { get; set; }

        [Column("banner_url")]
        public string Banner { get; set; }
    }
}
