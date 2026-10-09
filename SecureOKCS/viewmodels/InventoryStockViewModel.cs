using System.ComponentModel.DataAnnotations;

namespace SecureOKCS.ViewModels
{
    public class InventoryStockViewModel
    {
        [Required]
        public int InventoryId { get; set; }

        [Display(Name = "Item Code")]
        public string ItemCode { get; set; }
            = string.Empty;

        [Display(Name = "Item Name")]
        public string ItemName { get; set; }
            = string.Empty;

        [Display(Name = "Current Quantity")]
        public int CurrentQuantity { get; set; }

        [Display(Name = "Barcode / Scanned Code")]
        public string? ScannedCode { get; set; }

        [Required(ErrorMessage =
            "Transaction type required")]
        [Display(Name = "Transaction Type")]
        public string TransactionType { get; set; }
            = string.Empty;

        [Required(ErrorMessage =
            "Quantity required")]
        [Range(
            1,
            1000000,
            ErrorMessage =
                "Quantity must be at least 1")]
        [Display(Name = "Quantity")]
        public int Quantity { get; set; }

        [StringLength(255)]
        [Display(Name = "Transaction Notes")]
        public string? TransactionNotes { get; set; }
    }
}