using Postgrest.Attributes;
using Postgrest.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hartsy.Core.SupaBase.Models
{
    /// <summary>Represents a generation record, mapping to the 'generations' table in Supabase.</summary>
    [Table("generations")]
    public class Generations : BaseModel
    {
        [PrimaryKey("id", false)]
        public long Id { get; set; }
        [Column("user_id")]
        public string? UserId { get; set; }
        [Column("batch")]
        public short Batch { get; set; }
        //[Column("duration")]
        //public long Duration { get; set; }
        [Column("positive")]
        public string? Positive { get; set; }
        [Column("negative")]
        public string? Negative { get; set; }
        [Column("checkpoint")]
        public string? Checkpoint { get; set; }
        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
        [Column("comfy_endpoint_id")]
        public int? ComfyEndpointId { get; set; }
        [Column("comfy_prompt_id")]
        public string? ComfyPromptId { get; set; }
        [Column("width")]
        public long Width { get; set; }
        [Column("height")]
        public long Height { get; set; }
        //[Column("template_id")]
        //public long TemplateId { get; set; }
        [Column("status")]
        public string? Status { get; set; }
    }
}
