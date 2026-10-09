using System.ComponentModel.DataAnnotations;

namespace SecureOKCS.ViewModels
{
    public class EditUserViewModel
    {
        public int UserId { get; set; }

        [Required(ErrorMessage =
            "Full name is required.")]
        [StringLength(100)]
        [Display(Name = "Full Name")]
        public string FullName { get; set; }
            = string.Empty;

        [Required(ErrorMessage =
            "Email address is required.")]
        [EmailAddress(ErrorMessage =
            "Enter a valid email address.")]
        [StringLength(100)]
        [Display(Name = "Email Address")]
        public string Email { get; set; }
            = string.Empty;

        [StringLength(20)]
        [Display(Name = "Phone Number")]
        public string? PhoneNumber { get; set; }

        [Range(
            1,
            int.MaxValue,
            ErrorMessage = "Select a valid role.")]
        [Display(Name = "Account Role")]
        public int RoleId { get; set; }

        [Display(Name = "Active Account")]
        public bool IsActive { get; set; }

        [DataType(DataType.Password)]
        [MinLength(
            8,
            ErrorMessage =
                "The new password must contain at least 8 characters.")]
        [Display(Name = "New Password")]
        public string? NewPassword { get; set; }

        [DataType(DataType.Password)]
        [Compare(
            nameof(NewPassword),
            ErrorMessage =
                "The passwords do not match.")]
        [Display(Name = "Confirm New Password")]
        public string? ConfirmPassword { get; set; }
    }
}