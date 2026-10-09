using System.ComponentModel.DataAnnotations;

namespace SecureOKCS.ViewModels
{
    public class RepairCreateViewModel
    {
        [Range(
            1,
            int.MaxValue,
            ErrorMessage = "Please select a customer.")]
        [Display(Name = "Customer")]
        public int CustomerId { get; set; }

        [Display(Name = "Assigned Technician")]
        public int? TechnicianId { get; set; }

        [Required(
            ErrorMessage = "Device type is required.")]
        [Display(Name = "Device Type")]
        public string DeviceType { get; set; }
            = string.Empty;

        [Required(
            ErrorMessage = "Brand and model are required.")]
        [Display(Name = "Brand and Model")]
        public string BrandModel { get; set; }
            = string.Empty;

        [Display(Name = "Serial Number")]
        public string? SerialNumber { get; set; }

        [Required(
            ErrorMessage = "Problem description is required.")]
        [Display(Name = "Problem Description")]
        public string ProblemDescription { get; set; }
            = string.Empty;

        [Display(Name = "Estimated Cost")]
        [Range(
            0,
            999999,
            ErrorMessage = "Please enter a valid estimated cost.")]
        public decimal EstimatedCost { get; set; }
    }
}