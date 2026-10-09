using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SecureOKCS.Models
{
    [Table("email_notifications")]
    public class EmailNotification
    {
        [Key]
        [Column("notification_id")]
        public int NotificationId { get; set; }

        [Column("repair_id")]
        public int RepairId { get; set; }

        [Column("customer_id")]
        public int CustomerId { get; set; }

        [Required]
        [Column("recipient_email_masked")]
        [StringLength(150)]
        public string RecipientEmailMasked { get; set; }
            = string.Empty;

        [Required]
        [Column("email_subject")]
        [StringLength(200)]
        public string EmailSubject { get; set; }
            = string.Empty;

        [Required]
        [Column("message_body")]
        [StringLength(2000)]
        public string MessageBody { get; set; }
            = string.Empty;

        [Required]
        [Column("notification_status")]
        [StringLength(30)]
        public string NotificationStatus { get; set; }
            = "Pending";

        [Column("error_message")]
        [StringLength(1000)]
        public string? ErrorMessage { get; set; }

        [Column("sent_at")]
        public DateTime? SentAt { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [ForeignKey(nameof(RepairId))]
        public RepairTicket? RepairTicket { get; set; }

        [ForeignKey(nameof(CustomerId))]
        public Customer? Customer { get; set; }
    }
}