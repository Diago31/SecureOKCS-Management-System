using System.ComponentModel.DataAnnotations;

namespace SecureOKCS.ViewModels
{
    public class CreateStaffViewModel
    {
        [Required(ErrorMessage = "Enter your name")]
        [Display(Name = "Fill in your full name")]
        public string FullName { get; set; }
            = string.Empty;

        [Required(ErrorMessage = "Email needed]")]
        [EmailAddress(ErrorMessage =
            "Incorrect email")]
        public string Email { get; set; }
            = string.Empty;

        [Display(Name = "Phone number")]
        public string? PhoneNumber { get; set; }

        [Required(ErrorMessage =
            "Password needed")]
        [MinLength(8, ErrorMessage =
            "Password must be at least 8 characters long")]
        [DataType(DataType.Password)]
        public string Password { get; set; }
            = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [Compare("Password",
            ErrorMessage = "Passwords do not macth")]
        [Display(Name = "Confirm Password")]
        public string ConfirmPassword { get; set; }
            = string.Empty;
    }
}