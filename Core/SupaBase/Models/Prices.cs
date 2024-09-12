using Postgrest.Attributes;
using Postgrest.Models;

namespace Hartsy.Core.SupaBase.Models
{
    /// <summary>Represents a price record, mapping to the 'prices' table in Supabase.</summary>
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
