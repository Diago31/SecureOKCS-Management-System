using System.ComponentModel.DataAnnotations;

namespace SecureOKCS.ViewModels
{
    public class RepairTrackingSearchViewModel
    {
        [Required(
            ErrorMessage =
                "Tracking code required")]
        [StringLength(
            30,
            ErrorMessage =
                "Invalid Tracking code")]
        [Display(
            Name =
                "Tracking Code")]
        public string TrackingCode { get; set; }
            = string.Empty;

        [Required(
            ErrorMessage =
                "Ticket number required")]
        [StringLength(
            40,
            ErrorMessage =
                "Invalid Ticket number")]
        [Display(
            Name =
                "Ticket Number")]
        public string TicketNumber { get; set; }
            = string.Empty;
    }

    public class RepairTrackingResultViewModel
    {
        public string TicketNumber { get; set; }
            = string.Empty;

        public string DeviceType { get; set; }
            = string.Empty;

        public string BrandModel { get; set; }
            = string.Empty;

        public string RepairStatus { get; set; }
            = string.Empty;

        public DateTime ReceivedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public DateTime? CompletedAt { get; set; }

        public List<TrackingHistoryItemViewModel>
            StatusHistory
        { get; set; }
                = new();
    }

    public class TrackingHistoryItemViewModel
    {
        public string Status { get; set; }
            = string.Empty;

        public string? Notes { get; set; }

        public DateTime ChangedAt { get; set; }
    }
}