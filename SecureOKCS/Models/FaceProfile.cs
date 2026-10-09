using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SecureOKCS.Models
{
    [Table("face_profiles")]
    public class FaceProfile
    {
        [Key]
        [Column("face_profile_id")]
        public int FaceProfileId { get; set; }

        [Column("user_id")]
        public int UserId { get; set; }

        [Required]
        [StringLength(255)]
        [Column("template_reference")]
        public string TemplateReference { get; set; }
            = string.Empty;

        [StringLength(64)]
        [Column("template_hash")]
        public string? TemplateHash { get; set; }

        [Column("liveness_required")]
        public bool LivenessRequired { get; set; }
            = true;

        [Column("is_active")]
        public bool IsActive { get; set; }
            = true;

        [Column("enrolled_at")]
        public DateTime EnrolledAt { get; set; }

        [Column("last_verified_at")]
        public DateTime? LastVerifiedAt { get; set; }

        [ForeignKey(nameof(UserId))]
        public User? User { get; set; }
    }
}