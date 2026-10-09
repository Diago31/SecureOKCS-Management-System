using System.Security.Claims;
using SecureOKCS.Data;
using SecureOKCS.Models;

namespace SecureOKCS.Services
{
    public class AuditLogService
    {
        private readonly ApplicationDbContext
            _context;

        private readonly IHttpContextAccessor
            _httpContextAccessor;

        public AuditLogService(
            ApplicationDbContext context,
            IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor =
                httpContextAccessor;
        }

        public async Task LogAsync(
            string actionType,
            string tableName,
            string? recordId = null,
            string? actionDetails = null)
        {
            var httpContext =
                _httpContextAccessor.HttpContext;

            int? userId = null;

            string? userIdValue =
                httpContext?.User.FindFirstValue(
                    ClaimTypes.NameIdentifier);

            if (int.TryParse(
                    userIdValue,
                    out int parsedUserId))
            {
                userId =
                    parsedUserId;
            }

            string? ipAddress =
                httpContext?
                    .Connection
                    .RemoteIpAddress?
                    .ToString();

            var auditLog =
                new AuditLog
                {
                    UserId =
                        userId,

                    ActionType =
                        LimitLength(
                            actionType,
                            80),

                    TableName =
                        LimitLength(
                            tableName,
                            80),

                    RecordId =
                        LimitNullableLength(
                            recordId,
                            50),

                    ActionDetails =
                        actionDetails,

                    IpAddress =
                        LimitNullableLength(
                            ipAddress,
                            45),

                    CreatedAt =
                        DateTime.Now
                };

            _context.AuditLogs.Add(
                auditLog);

            await _context.SaveChangesAsync();
        }

        private static string LimitLength(
            string value,
            int maximumLength)
        {
            string safeValue =
                value.Trim();

            return safeValue.Length >
                   maximumLength
                ? safeValue[..maximumLength]
                : safeValue;
        }

        private static string?
            LimitNullableLength(
                string? value,
                int maximumLength)
        {
            if (string.IsNullOrWhiteSpace(
                    value))
            {
                return null;
            }

            string safeValue =
                value.Trim();

            return safeValue.Length >
                   maximumLength
                ? safeValue[..maximumLength]
                : safeValue;
        }
    }
}