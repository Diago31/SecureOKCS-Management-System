namespace SecureOKCS.ViewModels
{
    public class CustomerDisplayViewModel
    {
        public int CustomerId { get; set; }

        public string FullName { get; set; }
            = string.Empty;

        public string Email { get; set; }
            = string.Empty;

        public string PhoneNumber { get; set; }
            = string.Empty;

        public string? Address { get; set; }

        public string TrackingCode { get; set; }
            = string.Empty;

        public DateTime CreatedAt { get; set; }
    }
}