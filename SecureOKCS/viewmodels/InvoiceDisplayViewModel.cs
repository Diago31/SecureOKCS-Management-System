namespace SecureOKCS.ViewModels
{
    public class InvoiceDisplayViewModel
    {
        public int InvoiceId { get; set; }

        public string InvoiceNumber { get; set; }
            = string.Empty;

        public int RepairId { get; set; }

        public string TicketNumber { get; set; }
            = string.Empty;

        // REPAIR TRACKING INFORMATION
        public string TrackingCode { get; set; }
            = string.Empty;

        public string RepairStatus { get; set; }
            = string.Empty;

        // CUSTOMER INFORMATION
        public string CustomerName { get; set; }
            = string.Empty;

        public string CustomerEmail { get; set; }
            = string.Empty;

        public string CustomerPhone { get; set; }
            = string.Empty;

        public string? CustomerAddress { get; set; }

        // DEVICE INFORMATION
        public string DeviceType { get; set; }
            = string.Empty;

        public string BrandModel { get; set; }
            = string.Empty;

        public string ProblemDescription { get; set; }
            = string.Empty;

        // INVOICE AMOUNTS
        public decimal Subtotal { get; set; }

        public decimal TaxAmount { get; set; }

        public decimal TotalAmount { get; set; }

        // PAYMENT INFORMATION
        public string PaymentStatus { get; set; }
            = string.Empty;

        public string? PaymentMethod { get; set; }

        // ISSUED INFORMATION
        public string IssuedByName { get; set; }
            = string.Empty;

        public DateTime IssuedAt { get; set; }

        public DateTime? PaidAt { get; set; }
    }
}