using System.ComponentModel.DataAnnotations;

namespace SecureOKCS.ViewModels
{
    public class FaceEnrollmentViewModel
    {
        [Required(ErrorMessage =
            "Face descriptors are not accpted")]
        public string DescriptorJson { get; set; }
            = string.Empty;

        [Range(
            0.0,
            1.0,
            ErrorMessage =
                "Invalid liveness score")]
        public double LivenessScore { get; set; }

        [Display(Name =
            "I consent to biometric processing")]
        public bool ConsentGiven { get; set; }
    }
}