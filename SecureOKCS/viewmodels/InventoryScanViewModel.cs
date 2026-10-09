using System.ComponentModel.DataAnnotations;

namespace SecureOKCS.ViewModels
{
    public class InventoryScanViewModel
    {
        [Required(ErrorMessage =
            "Please scan or enter the barcode")]
        [StringLength(100)]
        [Display(Name = "Barcode / Item Code")]
        public string ScannedCode { get; set; }
            = string.Empty;
    }
}