using Newtonsoft.Json.Linq;
using Postgrest.Attributes;
using Postgrest.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hartsy.Core.SupaBase.Models
{
    // Represents a template record, mapping to the 'templates' table in Supabase.
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
        public DateTimeOffset? CreatedAt { get; set; }
        [Column("order_rank")]
        public long? OrderRank { get; set; }
        [Column("active")]
        public bool? Active { get; set; }
        [Column("name")]
        public string Name { get; set; } = string.Empty;
        [Column("description")]
        public string? Description { get; set; }
        [Column("image_url")]
        public string? ImageUrl { get; set; }
        [Column("user_id")]
        public Guid? UserId { get; set; }
        [Column("cfg")]
        public float? Cfg { get; set; }
        [Column("steps")]
        public int? Steps { get; set; }
        [Column("sampler")]
        public string? Sampler { get; set; }
        [Column("scheduler")]
        public string? Scheduler { get; set; }
        [Column("loras")]
        public JArray? Loras { get; set; }
    }
}
