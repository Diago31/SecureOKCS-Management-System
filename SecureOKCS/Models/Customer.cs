using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SecureOKCS.Models
{
    [Table("customers")]
    public class Customer
    {
        [Key]
        [Column("customer_id")]
        public int CustomerId { get; set; }

        [Column("user_id")]
        public int? UserId { get; set; }

        [Required]
        [Column("full_name_encrypted")]
        public string FullNameEncrypted { get; set; }
            = string.Empty;

        [Required]
        [Column("email_encrypted")]
        public string EmailEncrypted { get; set; }
            = string.Empty;

        [Required]
        [Column("phone_encrypted")]
        public string PhoneEncrypted { get; set; }
            = string.Empty;

        [Column("address_encrypted")]
        public string? AddressEncrypted { get; set; }

        [Column("phone_last_four")]
        public string? PhoneLastFour { get; set; }

        [Required]
        [Column("tracking_code")]
        public string TrackingCode { get; set; }
            = string.Empty;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }

        [ForeignKey(nameof(UserId))]
        public User? User { get; set; }
    }
}