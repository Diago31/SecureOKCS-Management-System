using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SecureOKCS.Models
{
    [Table("inventory_transactions")]
    public class InventoryTransaction
    {
        [Key]
        [Column("transaction_id")]
        public int TransactionId { get; set; }

        [Column("inventory_id")]
        public int InventoryId { get; set; }

        [Column("user_id")]
        public int UserId { get; set; }

        [Required]
        [Column("transaction_type")]
        [StringLength(30)]
        public string TransactionType { get; set; }
            = string.Empty;

        [Column("quantity_changed")]
        public int QuantityChanged { get; set; }

        [Column("balance_after")]
        public int BalanceAfter { get; set; }

        [Column("scanned_code")]
        [StringLength(100)]
        public string? ScannedCode { get; set; }

        [Column("transaction_notes")]
        [StringLength(255)]
        public string? TransactionNotes { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [ForeignKey(nameof(InventoryId))]
        public InventoryItem? InventoryItem { get; set; }

        [ForeignKey(nameof(UserId))]
        public User? User { get; set; }
    }
}