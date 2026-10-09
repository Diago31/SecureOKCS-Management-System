namespace SecureOKCS.ViewModels
{
    public class AuditLogDisplayViewModel
    {
        public long AuditId { get; set; }

        public string UserName { get; set; }
            = string.Empty;

        public string RoleName { get; set; }
            = string.Empty;

        public string ActionType { get; set; }
            = string.Empty;

        public string TableName { get; set; }
            = string.Empty;

        public string? RecordId { get; set; }

        public string? ActionDetails { get; set; }

        public string? IpAddress { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}