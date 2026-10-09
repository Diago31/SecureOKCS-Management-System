using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SecureOKCS.Models
{
    [Table("face_login_attempts")]
    public class FaceLoginAttempt
    {
        [Key]
        [Column("face_login_attempt_id")]
        public int FaceLoginAttemptId
        {
            get;
            set;
        }

        [Column("user_id")]
        public int UserId
        {
            get;
            set;
        }

        [Required]
        [StringLength(45)]
        [Column("ip_address")]
        public string IpAddress
        {
            get;
            set;
        } = string.Empty;

        [Column("failed_attempts")]
        public int FailedAttempts
        {
            get;
            set;
        }

        [Column("blocked_until")]
        public DateTime? BlockedUntil
        {
            get;
            set;
        }

        [Column("last_failed_at")]
        public DateTime? LastFailedAt
        {
            get;
            set;
        }

        [Column("updated_at")]
        public DateTime UpdatedAt
        {
            get;
            set;
        }

        [ForeignKey(nameof(UserId))]
        public User? User
        {
            get;
            set;
        }
    }
}