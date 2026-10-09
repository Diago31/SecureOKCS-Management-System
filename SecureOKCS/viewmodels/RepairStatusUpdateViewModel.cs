using System.ComponentModel.DataAnnotations;

namespace SecureOKCS.ViewModels
{
    public class RepairStatusUpdateViewModel
    {
        public int RepairId { get; set; }

        public string TicketNumber { get; set; }
            = string.Empty;

        public string CurrentStatus { get; set; }
            = string.Empty;

        [Required(ErrorMessage =
            "A new status is required")]
        [Display(Name = "New Status")]
        public string NewStatus { get; set; }
            = string.Empty;

        [Display(Name = "Status Notes")]
        [StringLength(255)]
        public string? StatusNotes { get; set; }

        [Display(Name = "Technician Notes")]
        public string? TechnicianNotes { get; set; }

        [Display(Name = "Final Cost")]
        [Range(0, 999999,
            ErrorMessage = "Invalid cost")]
        public decimal FinalCost { get; set; }
    }
}