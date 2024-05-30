using Postgrest.Attributes;
using Postgrest.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hartsy.Core.SupaBase.Models
{
    /// <summary>Represents a subscription record, mapping to the 'subscriptions' table in Supabase.</summary>
    [Table("subscriptions")]
    public class Subscriptions : BaseModel
    {
        [PrimaryKey("id", false)]
        public string? Id { get; set; }
        [Column("user_id")]
        public string? UserId { get; set; }
        [Column("status")]
        public string? Status { get; set; }
        [Column("metadata")]
        public string? MetadataJson { get; set; } // Keeping as string but changing the name for clarity
                                                  // Not stored in DB, just a convenient way to access the parsed metadata
        [JsonIgnore] // Make sure this isn't attempted to be mapped by your ORM
        public Dictionary<string, object>? Metadata
        {
            get
            {
                if (string.IsNullOrEmpty(MetadataJson)) return null;
                try
                {
                    return JsonSerializer.Deserialize<Dictionary<string, object>>(MetadataJson);
                }
                catch (JsonException)
                {
                    Console.WriteLine("Failed to parse JSON from metadata.");
                    return null;
                }
            }
        }

        [Column("price_id")]
        public string? PriceId { get; set; }
        [Column("quantity")]
        public int? Quantity { get; set; }
        [Column("cancel_at_period_end")]
        public bool? CancelAtPeriodEnd { get; set; }
        [Column("created")]
        public DateTime? Created { get; set; }
        [Column("current_period_start")]
        public DateTime? CurrentPeriodStart { get; set; }
        [Column("current_period_end")]
        public DateTime? CurrentPeriodEnd { get; set; }
        [Column("ended_at")]
        public DateTime? EndedAt { get; set; }
        [Column("cancel_at")]
        public DateTime? CancelAt { get; set; }
        [Column("canceled_at")]
        public DateTime? CanceledAt { get; set; }
        [Column("trial_start")]
        public DateTime? TrialStart { get; set; }
        [Column("trial_end")]
        public DateTime? TrialEnd { get; set; }
        [Column("amount")]
        public int? Amount { get; set; }
    }
}
