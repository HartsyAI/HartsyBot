using Postgrest.Attributes;
using Postgrest.Models;

namespace Hartsy.Core.SupaBase.Models
{
    /// <summary>Represents an image record, mapping to the 'images' table in Supabase.</summary>
    [Table("images")]
    public class Images : BaseModel
    {
        [PrimaryKey("id", false)]
        public long Id { get; set; }
        [Column("user_id")]
        public Guid UserId { get; set; }
        [Column("generation_id")]
        public long GenerationId { get; set; }
        [Column("image_url")]
        public string? ImageUrl { get; set; }
        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
        [Column("likes_count")]
        public long LikesCount { get; set; }
        //[Column("template_id")]
        //public long TemplateId { get; set; }
        [Column("is_public")]
        public bool IsPublic { get; set; }
    }
}
