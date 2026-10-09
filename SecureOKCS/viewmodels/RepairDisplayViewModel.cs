namespace SecureOKCS.ViewModels
{
    public class RepairDisplayViewModel
    {
        public int RepairId { get; set; }

        public string TicketNumber { get; set; }
            = string.Empty;

        public string CustomerName { get; set; }
            = string.Empty;

        public string DeviceType { get; set; }
            = string.Empty;

        public string BrandModel { get; set; }
            = string.Empty;

        public string? SerialNumber { get; set; }

        public string ProblemDescription { get; set; }
            = string.Empty;

        public string? TechnicianNotes { get; set; }

        public string TechnicianName { get; set; }
            = "Not Assigned";

        public string RepairStatus { get; set; }
            = "Received";

        public decimal EstimatedCost { get; set; }

        public decimal FinalCost { get; set; }

        public DateTime ReceivedAt { get; set; }

        public DateTime? CompletedAt { get; set; }
    }
}