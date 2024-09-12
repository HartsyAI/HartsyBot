using Postgrest.Attributes;
using Postgrest.Models;

namespace Hartsy.Core.SupaBase.Models
{
    /// <summary>Represents a user in the application, mapping to the 'users' table in Supabase.</summary>
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
        [Column("stripe_customer_id")]
        public string? StripeId { get; set; }
        [Column("gallery_size")]
        public int? Gallery { get; set; }
        [Column("plan_name")]
        public string? PlanName { get; set; }
    }
}
