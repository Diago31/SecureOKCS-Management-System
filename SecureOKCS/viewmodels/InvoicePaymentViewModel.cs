using System.ComponentModel.DataAnnotations;

namespace SecureOKCS.ViewModels
{
    public class InvoicePaymentViewModel
    {
        [Required]
        public int InvoiceId { get; set; }

        public string InvoiceNumber { get; set; }
            = string.Empty;

        public decimal TotalAmount { get; set; }

        [Required(ErrorMessage =
            "Choose payment method")]
        [Display(Name = "Payment Method")]
        public string PaymentMethod { get; set; }
            = string.Empty;
    }
}