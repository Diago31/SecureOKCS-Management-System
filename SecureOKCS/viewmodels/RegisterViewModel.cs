using System.ComponentModel.DataAnnotations;

namespace SecureOKCS.ViewModels
{
    public class RegisterViewModel
    {
        [Required(ErrorMessage = "Name is required")]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "The email format is wrong")]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "Phone Number")]
        public string? PhoneNumber { get; set; }

        [Required(ErrorMessage = "Password is required")]
        [MinLength(8, ErrorMessage =
            "Password must be 8 characters")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please verify your password")]
        [DataType(DataType.Password)]
        [Compare("Password",
            ErrorMessage = "Password unmatched")]
        [Display(Name = "Verify Password")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}