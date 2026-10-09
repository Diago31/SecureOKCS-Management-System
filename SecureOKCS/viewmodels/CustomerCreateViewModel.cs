using System.ComponentModel.DataAnnotations;

namespace SecureOKCS.ViewModels
{
    public class CustomerCreateViewModel
    {
        [Required(ErrorMessage = "Name is required")]
        [Display(Name = "Full Name")]
        public string FullName { get; set; }
            = string.Empty;

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage =
            "The email format is wrong")]
        public string Email { get; set; }
            = string.Empty;

        [Required(ErrorMessage =
            "Phone number is required")]
        [Display(Name = "Phone Number")]
        public string PhoneNumber { get; set; }
            = string.Empty;

        [Display(Name = "Address")]
        public string? Address { get; set; }
    }
}