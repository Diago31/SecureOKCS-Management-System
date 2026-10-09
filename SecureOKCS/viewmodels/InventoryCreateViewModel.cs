using System.ComponentModel.DataAnnotations;

namespace SecureOKCS.ViewModels
{
    public class InventoryCreateViewModel
    {
        [Required(ErrorMessage =
            "Item code required")]
        [StringLength(50)]
        [Display(Name = "Item Code")]
        public string ItemCode { get; set; }
            = string.Empty;

        [Required(ErrorMessage =
            "Item name required")]
        [StringLength(150)]
        [Display(Name = "Item Name")]
        public string ItemName { get; set; }
            = string.Empty;

        [StringLength(100)]
        [Display(Name = "Category")]
        public string? Category { get; set; }

        [StringLength(500)]
        [Display(Name = "Description")]
        public string? Description { get; set; }

        [StringLength(100)]
        [Display(Name = "Barcode / Scanner Code")]
        public string? Barcode { get; set; }

        [Range(
            0,
            1000000,
            ErrorMessage =
                "Quantity cannot be negative")]
        [Display(Name = "Initial Quantity")]
        public int Quantity { get; set; }

        [Range(
            0,
            1000000,
            ErrorMessage =
                "Minimum stock cannot be negative")]
        [Display(Name = "Minimum Stock Level")]
        public int MinimumStock { get; set; } = 5;

        [Range(
            0,
            999999.99,
            ErrorMessage =
                "Invalid Unit")]
        [DataType(DataType.Currency)]
        [Display(Name = "Unit Price")]
        public decimal UnitPrice { get; set; }
    }
}