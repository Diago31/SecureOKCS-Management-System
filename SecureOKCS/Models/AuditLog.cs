using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SecureOKCS.Models
{
    [Table("audit_logs")]
    public class AuditLog
    {
        [Key]
        [Column("audit_id")]
        public long AuditId { get; set; }

        [Column("user_id")]
        public int? UserId { get; set; }

        [Required]
        [StringLength(80)]
        [Column("action_type")]
        public string ActionType { get; set; }
            = string.Empty;

        [Required]
        [StringLength(80)]
        [Column("table_name")]
        public string TableName { get; set; }
            = string.Empty;

        [StringLength(50)]
        [Column("record_id")]
        public string? RecordId { get; set; }

        [Column("action_details")]
        public string? ActionDetails { get; set; }

        [StringLength(45)]
        [Column("ip_address")]
        public string? IpAddress { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [ForeignKey(nameof(UserId))]
        public User? User { get; set; }
    }
}