using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SecureOKCS.Models
{
    [Table("repair_status_history")]
    public class RepairStatusHistory
    {
        [Key]
        [Column("history_id")]
        public int HistoryId { get; set; }

        [Column("repair_id")]
        public int RepairId { get; set; }

        [Column("changed_by")]
        public int ChangedBy { get; set; }

        [Column("previous_status")]
        [StringLength(50)]
        public string? PreviousStatus { get; set; }

        [Required]
        [Column("new_status")]
        [StringLength(50)]
        public string NewStatus { get; set; }
            = string.Empty;

        [Column("status_notes")]
        [StringLength(255)]
        public string? StatusNotes { get; set; }

        [Column("changed_at")]
        public DateTime ChangedAt { get; set; }

        [ForeignKey(nameof(RepairId))]
        public RepairTicket? RepairTicket { get; set; }

        [ForeignKey(nameof(ChangedBy))]
        public User? ChangedByUser { get; set; }
    }
}