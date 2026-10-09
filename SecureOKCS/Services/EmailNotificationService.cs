using System.Net;
using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SecureOKCS.Data;
using SecureOKCS.Models;

namespace SecureOKCS.Services
{
    public class EmailNotificationService
    {
        private readonly ApplicationDbContext
            _context;

        private readonly CustomerEncryptionService
            _encryption;

        private readonly EmailSettings
            _settings;

        public EmailNotificationService(
            ApplicationDbContext context,
            CustomerEncryptionService encryption,
            IOptions<EmailSettings> settings)
        {
            _context = context;
            _encryption = encryption;
            _settings = settings.Value;
        }

        public async Task SendRepairStatusEmailAsync(
            int repairId,
            string newStatus)
        {
            var repair =
                await _context.RepairTickets
                    .AsNoTracking()
                    .Include(repair =>
                        repair.Customer)
                    .FirstOrDefaultAsync(repair =>
                        repair.RepairId ==
                        repairId);

            if (repair?.Customer == null)
                return;

            string maskedEmail =
                "Hidden";

            try
            {
                maskedEmail =
                    _encryption.MaskEmail(
                        repair.Customer
                            .EmailEncrypted);
            }
            catch
            {
                maskedEmail =
                    "Hidden";
            }

            string subject =
                "Repair Status Update - " +
                repair.TicketNumber;

            string messageBody =
                "Your repair status has been updated."
                + Environment.NewLine
                + Environment.NewLine
                + "Ticket Number: "
                + repair.TicketNumber
                + Environment.NewLine
                + "Device: "
                + repair.DeviceType
                + Environment.NewLine
                + "Brand / Model: "
                + repair.BrandModel
                + Environment.NewLine
                + "New Status: "
                + newStatus
                + Environment.NewLine
                + "Updated At: "
                + DateTime.Now.ToString(
                    "dd MMM yyyy hh:mm tt")
                + Environment.NewLine
                + Environment.NewLine
                + "You may check the latest status "
                + "using the Track Repair page."
                + Environment.NewLine
                + Environment.NewLine
                + "Thank you,"
                + Environment.NewLine
                + "Secure OKCS";

            var notification =
                new EmailNotification
                {
                    RepairId =
                        repair.RepairId,

                    CustomerId =
                        repair.Customer.CustomerId,

                    RecipientEmailMasked =
                        maskedEmail,

                    EmailSubject =
                        subject,

                    MessageBody =
                        messageBody,

                    NotificationStatus =
                        _settings.Enabled
                            ? "Pending"
                            : "Skipped",

                    ErrorMessage =
                        _settings.Enabled
                            ? null
                            : "Email sending is " +
                              "currently disabled.",

                    CreatedAt =
                        DateTime.Now
                };

            _context.EmailNotifications.Add(
                notification);

            await _context.SaveChangesAsync();

            // EMAIL DISABLED SEMASA DEVELOPMENT
            if (!_settings.Enabled)
                return;

            try
            {
                ValidateSettings();

                string recipientEmail =
                    _encryption.Decrypt(
                        repair.Customer
                            .EmailEncrypted);

                using var mailMessage =
                    new MailMessage
                    {
                        From =
                            new MailAddress(
                                _settings.SenderEmail,
                                _settings.SenderName),

                        Subject =
                            subject,

                        Body =
                            messageBody,

                        IsBodyHtml =
                            false
                    };

                mailMessage.To.Add(
                    new MailAddress(
                        recipientEmail));

                using var smtpClient =
                    new SmtpClient(
                        _settings.Host,
                        _settings.Port)
                    {
                        EnableSsl =
                            _settings.EnableSsl,

                        UseDefaultCredentials =
                            false,

                        Credentials =
                            new NetworkCredential(
                                _settings.Username,
                                _settings.Password),

                        DeliveryMethod =
                            SmtpDeliveryMethod.Network
                    };

                await smtpClient.SendMailAsync(
                    mailMessage);

                notification.NotificationStatus =
                    "Sent";

                notification.SentAt =
                    DateTime.Now;

                notification.ErrorMessage =
                    null;
            }
            catch (Exception exception)
            {
                notification.NotificationStatus =
                    "Failed";

                notification.SentAt =
                    null;

                string errorMessage =
                    string.IsNullOrWhiteSpace(
                        exception.Message)
                        ? "Unknown email error."
                        : exception.Message;

                notification.ErrorMessage =
                    errorMessage.Length > 1000
                        ? errorMessage[..1000]
                        : errorMessage;
            }

            await _context.SaveChangesAsync();
        }

        private void ValidateSettings()
        {
            if (string.IsNullOrWhiteSpace(
                    _settings.Host) ||
                _settings.Port <= 0 ||
                string.IsNullOrWhiteSpace(
                    _settings.SenderEmail) ||
                string.IsNullOrWhiteSpace(
                    _settings.Username) ||
                string.IsNullOrWhiteSpace(
                    _settings.Password))
            {
                throw new InvalidOperationException(
                    "SMTP settings are incomplete.");
            }
        }
    }
}