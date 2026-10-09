using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SecureOKCS.Models
{
    [Table("invoices")]
    public class Invoice
    {
        [Key]
        [Column("invoice_id")]
        public int InvoiceId { get; set; }

        [Required]
        [StringLength(30)]
        [Column("invoice_number")]
        public string InvoiceNumber { get; set; }
            = string.Empty;

        [Column("repair_id")]
        public int RepairId { get; set; }

        [Column("issued_by")]
        public int IssuedBy { get; set; }

        [Column("subtotal", TypeName = "decimal(10,2)")]
        public decimal Subtotal { get; set; }

        [Column("tax_amount", TypeName = "decimal(10,2)")]
        public decimal TaxAmount { get; set; }

        [Column("total_amount", TypeName = "decimal(10,2)")]
        public decimal TotalAmount { get; set; }

        [Required]
        [StringLength(30)]
        [Column("payment_status")]
        public string PaymentStatus { get; set; }
            = "Unpaid";

        [StringLength(50)]
        [Column("payment_method")]
        public string? PaymentMethod { get; set; }

        [Column("issued_at")]
        public DateTime IssuedAt { get; set; }

        [Column("paid_at")]
        public DateTime? PaidAt { get; set; }

        [ForeignKey(nameof(RepairId))]
        public RepairTicket? RepairTicket { get; set; }

        [ForeignKey(nameof(IssuedBy))]
        public User? IssuedByUser { get; set; }
    }
}