using System.ComponentModel.DataAnnotations;

namespace SecureOKCS.ViewModels
{
    public class InvoiceCreateViewModel
    {
        [Required]
        public int RepairId { get; set; }

        [Display(Name = "Ticket Number")]
        public string TicketNumber { get; set; }
            = string.Empty;

        [Display(Name = "Customer Name")]
        public string CustomerName { get; set; }
            = string.Empty;

        [Display(Name = "Device")]
        public string DeviceDescription { get; set; }
            = string.Empty;

        [Required(ErrorMessage =
            "Subtotal required")]
        [Range(
            typeof(decimal),
            "0.01",
            "99999999.99",
            ErrorMessage =
                "Subtotal must over RM0.00")]
        [Display(Name = "Subtotal")]
        public decimal Subtotal { get; set; }

        [Range(
            typeof(decimal),
            "0.00",
            "99999999.99",
            ErrorMessage =
                "Invalid Tax amount")]
        [Display(Name = "Tax Amount")]
        public decimal TaxAmount { get; set; }

        [Display(Name = "Total Amount")]
        public decimal TotalAmount =>
            Subtotal + TaxAmount;
    }
}