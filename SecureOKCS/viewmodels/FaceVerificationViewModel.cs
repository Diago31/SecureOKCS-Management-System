using System.ComponentModel.DataAnnotations;

namespace SecureOKCS.ViewModels
{
    public class FaceVerificationViewModel
    {
        [Required(
            ErrorMessage =
                "Face descriptor are not accepted")]
        public string DescriptorJson
        {
            get;
            set;
        } = string.Empty;

        [Range(
            0.0,
            1.0,
            ErrorMessage =
                "Invalid Liveness score")]
        public double LivenessScore
        {
            get;
            set;
        }
    }
}