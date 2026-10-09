using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SecureOKCS.Models
{
    [Table("repair_tickets")]
    public class RepairTicket
    {
        [Key]
        [Column("repair_id")]
        public int RepairId { get; set; }

        [Required]
        [Column("ticket_number")]
        [StringLength(30)]
        public string TicketNumber { get; set; }
            = string.Empty;

        [Column("customer_id")]
        public int CustomerId { get; set; }

        [Column("technician_id")]
        public int? TechnicianId { get; set; }

        [Required]
        [Column("device_type")]
        [StringLength(80)]
        public string DeviceType { get; set; }
            = string.Empty;

        [Required]
        [Column("brand_model")]
        [StringLength(120)]
        public string BrandModel { get; set; }
            = string.Empty;

        [Column("serial_number")]
        [StringLength(120)]
        public string? SerialNumber { get; set; }

        [Required]
        [Column("problem_description")]
        public string ProblemDescription { get; set; }
            = string.Empty;

        [Column("technician_notes")]
        public string? TechnicianNotes { get; set; }

        [Column("repair_status")]
        [StringLength(50)]
        public string RepairStatus { get; set; }
            = "Received";

        [Column("estimated_cost", TypeName = "decimal(10,2)")]
        public decimal EstimatedCost { get; set; }

        [Column("final_cost", TypeName = "decimal(10,2)")]
        public decimal FinalCost { get; set; }

        [Column("received_at")]
        public DateTime ReceivedAt { get; set; }

        [Column("completed_at")]
        public DateTime? CompletedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }

        [ForeignKey(nameof(CustomerId))]
        public Customer? Customer { get; set; }

        [ForeignKey(nameof(TechnicianId))]
        public User? Technician { get; set; }
    }
}