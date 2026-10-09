using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureOKCS.Data;
using SecureOKCS.Models;
using SecureOKCS.Services;
using SecureOKCS.ViewModels;

namespace SecureOKCS.Controllers
{
    [Authorize(Roles = "Admin,Staff")]
    public class InvoicesController : Controller
    {
        private readonly ApplicationDbContext
            _context;

        private readonly CustomerEncryptionService
            _encryption;

        private readonly AuditLogService
            _auditLog;

        public InvoicesController(
            ApplicationDbContext context,
            CustomerEncryptionService encryption,
            AuditLogService auditLog)
        {
            _context = context;
            _encryption = encryption;
            _auditLog = auditLog;
        }

        // SENARAI INVOICE
        public async Task<IActionResult> Index()
        {
            var invoices =
                await _context.Invoices
                    .Include(invoice =>
                        invoice.RepairTicket)
                    .ThenInclude(repair =>
                        repair!.Customer)
                    .Include(invoice =>
                        invoice.IssuedByUser)
                    .OrderByDescending(invoice =>
                        invoice.IssuedAt)
                    .AsNoTracking()
                    .ToListAsync();

            var result = invoices
                .Select(ConvertToDisplay)
                .ToList();

            return View(result);
        }

        // PAPARKAN BORANG GENERATE INVOICE
        [HttpGet]
        public async Task<IActionResult> Create(
            int repairId)
        {
            var repair =
                await _context.RepairTickets
                    .Include(repair =>
                        repair.Customer)
                    .FirstOrDefaultAsync(repair =>
                        repair.RepairId ==
                        repairId);

            if (repair == null)
                return NotFound();

            var existingInvoice =
                await _context.Invoices
                    .FirstOrDefaultAsync(invoice =>
                        invoice.RepairId ==
                        repairId);

            if (existingInvoice != null)
            {
                TempData["Info"] =
                    "Invoice for this " +
                    "already created.";

                return RedirectToAction(
                    nameof(Details),
                    new
                    {
                        id =
                            existingInvoice.InvoiceId
                    });
            }

            var model =
                new InvoiceCreateViewModel
                {
                    RepairId =
                        repair.RepairId,

                    TicketNumber =
                        repair.TicketNumber,

                    CustomerName =
                        GetCustomerName(repair),

                    DeviceDescription =
                        $"{repair.DeviceType} - " +
                        $"{repair.BrandModel}",

                    Subtotal =
                        repair.FinalCost > 0
                            ? repair.FinalCost
                            : repair.EstimatedCost,

                    TaxAmount =
                        0
                };

            return View(model);
        }

        // SIMPAN INVOICE
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            InvoiceCreateViewModel model)
        {
            var repair =
                await _context.RepairTickets
                    .Include(repair =>
                        repair.Customer)
                    .FirstOrDefaultAsync(repair =>
                        repair.RepairId ==
                        model.RepairId);

            if (repair == null)
                return NotFound();

            model.TicketNumber =
                repair.TicketNumber;

            model.CustomerName =
                GetCustomerName(repair);

            model.DeviceDescription =
                $"{repair.DeviceType} - " +
                $"{repair.BrandModel}";

            bool invoiceExists =
                await _context.Invoices
                    .AnyAsync(invoice =>
                        invoice.RepairId ==
                        model.RepairId);

            if (invoiceExists)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Invoice for this " +
                    "already created.");
            }

            decimal totalAmount =
                model.Subtotal +
                model.TaxAmount;

            if (totalAmount >
                99999999.99m)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Total invoice to much.");
            }

            if (!ModelState.IsValid)
                return View(model);

            string? userIdValue =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier);

            if (!int.TryParse(
                    userIdValue,
                    out int userId))
            {
                return Forbid();
            }

            decimal subtotal =
                decimal.Round(
                    model.Subtotal,
                    2);

            decimal taxAmount =
                decimal.Round(
                    model.TaxAmount,
                    2);

            decimal total =
                subtotal +
                taxAmount;

            var invoice = new Invoice
            {
                InvoiceNumber =
                    "INV-" +
                    DateTime.Now.ToString(
                        "yyyyMMdd") +
                    "-" +
                    Guid.NewGuid()
                        .ToString("N")[..6]
                        .ToUpperInvariant(),

                RepairId =
                    repair.RepairId,

                IssuedBy =
                    userId,

                Subtotal =
                    subtotal,

                TaxAmount =
                    taxAmount,

                TotalAmount =
                    total,

                PaymentStatus =
                    "Unpaid",

                PaymentMethod =
                    null,

                IssuedAt =
                    DateTime.Now,

                PaidAt =
                    null
            };

            repair.FinalCost =
                subtotal;

            repair.UpdatedAt =
                DateTime.Now;

            _context.Invoices.Add(
                invoice);

            await _context.SaveChangesAsync();

            // AUDIT LOG CREATE INVOICE
            await _auditLog.LogAsync(
                "CREATE",
                "invoices",
                invoice.InvoiceId.ToString(),
                $"Invoice {invoice.InvoiceNumber} " +
                $"generated for repair " +
                $"{repair.TicketNumber}.");

            TempData["Success"] =
                "Invoice successful.";

            return RedirectToAction(
                nameof(Details),
                new
                {
                    id = invoice.InvoiceId
                });
        }

        // PAPARKAN DETAILS INVOICE
        public async Task<IActionResult> Details(
            int id)
        {
            var invoice =
                await _context.Invoices
                    .Include(invoice =>
                        invoice.RepairTicket)
                    .ThenInclude(repair =>
                        repair!.Customer)
                    .Include(invoice =>
                        invoice.IssuedByUser)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(invoice =>
                        invoice.InvoiceId == id);

            if (invoice == null)
                return NotFound();

            // AUDIT LOG VIEW INVOICE
            await _auditLog.LogAsync(
                "VIEW",
                "invoices",
                invoice.InvoiceId.ToString(),
                $"Invoice {invoice.InvoiceNumber} " +
                "details viewed.");

            var model =
                ConvertToDisplay(invoice);

            return View(model);
        }

        // PAPARKAN BORANG PAYMENT
        [HttpGet]
        public async Task<IActionResult> MarkPaid(
            int id)
        {
            var invoice =
                await _context.Invoices
                    .AsNoTracking()
                    .FirstOrDefaultAsync(invoice =>
                        invoice.InvoiceId == id);

            if (invoice == null)
                return NotFound();

            if (invoice.PaymentStatus ==
                "Paid")
            {
                TempData["Info"] =
                    "Invoice already paid.";

                return RedirectToAction(
                    nameof(Details),
                    new
                    {
                        id =
                            invoice.InvoiceId
                    });
            }

            var model =
                new InvoicePaymentViewModel
                {
                    InvoiceId =
                        invoice.InvoiceId,

                    InvoiceNumber =
                        invoice.InvoiceNumber,

                    TotalAmount =
                        invoice.TotalAmount
                };

            return View(model);
        }

        // SIMPAN PAYMENT
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkPaid(
            InvoicePaymentViewModel model)
        {
            string[] allowedMethods =
            {
                "Cash",
                "Debit / Credit Card",
                "Online Banking",
                "E-Wallet"
            };

            var invoice =
                await _context.Invoices
                    .FirstOrDefaultAsync(invoice =>
                        invoice.InvoiceId ==
                        model.InvoiceId);

            if (invoice == null)
                return NotFound();

            model.InvoiceNumber =
                invoice.InvoiceNumber;

            model.TotalAmount =
                invoice.TotalAmount;

            if (invoice.PaymentStatus ==
                "Paid")
            {
                TempData["Info"] =
                    "Successful Payment.";

                return RedirectToAction(
                    nameof(Details),
                    new
                    {
                        id =
                            invoice.InvoiceId
                    });
            }

            if (!allowedMethods.Contains(
                    model.PaymentMethod))
            {
                ModelState.AddModelError(
                    nameof(model.PaymentMethod),
                    "Incorrect payment.");
            }

            if (!ModelState.IsValid)
                return View(model);

            invoice.PaymentStatus =
                "Paid";

            invoice.PaymentMethod =
                model.PaymentMethod;

            invoice.PaidAt =
                DateTime.Now;

            await _context.SaveChangesAsync();

            // AUDIT LOG PAYMENT
            await _auditLog.LogAsync(
                "PAYMENT",
                "invoices",
                invoice.InvoiceId.ToString(),
                $"Payment recorded for invoice " +
                $"{invoice.InvoiceNumber} using " +
                $"{invoice.PaymentMethod}.");

            TempData["Success"] =
                "Payment successfully recorded.";

            return RedirectToAction(
                nameof(Details),
                new
                {
                    id =
                        invoice.InvoiceId
                });
        }

        // DAPATKAN NAMA CUSTOMER
        private string GetCustomerName(
            RepairTicket repair)
        {
            if (repair.Customer == null)
                return "Unknown";

            return _encryption.Decrypt(
                repair.Customer
                    .FullNameEncrypted);
        }

        // STAFF HANYA MELIHAT SEBAHAGIAN E-MEL DAN NOMBOR TELEFON
        private static string MaskEmailForStaff(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return "-";

            int atIndex = email.IndexOf('@');
            if (atIndex <= 0)
                return "****";

            return email[0] + "***" + email[atIndex..];
        }

        private static string MaskPhoneForStaff(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
                return "-";

            int visibleCount = Math.Min(4, phone.Length);
            return "******" + phone[^visibleCount..];
        }

        // TUKAR INVOICE KEPADA VIEWMODEL
        private InvoiceDisplayViewModel
            ConvertToDisplay(
                Invoice invoice)
        {
            var repair =
                invoice.RepairTicket;

            var customer =
                repair?.Customer;

            string customerName =
                customer == null
                    ? "Unknown"
                    : _encryption.Decrypt(
                        customer.FullNameEncrypted);

            bool isAdmin = User.IsInRole("Admin");

            string customerEmail =
                customer == null
                    ? "-"
                    : isAdmin
                        ? _encryption.Decrypt(
                            customer.EmailEncrypted)
                        : MaskEmailForStaff(
                            _encryption.Decrypt(
                                customer.EmailEncrypted));

            string customerPhone =
                customer == null
                    ? "-"
                    : isAdmin
                        ? _encryption.Decrypt(
                            customer.PhoneEncrypted)
                        : MaskPhoneForStaff(
                            _encryption.Decrypt(
                                customer.PhoneEncrypted));

            string? customerAddress =
                customer == null ||
                string.IsNullOrWhiteSpace(customer.AddressEncrypted)
                    ? null
                    : isAdmin
                        ? _encryption.Decrypt(customer.AddressEncrypted)
                        : "********";

            return new InvoiceDisplayViewModel
            {
                InvoiceId =
                    invoice.InvoiceId,

                InvoiceNumber =
                    invoice.InvoiceNumber,

                RepairId =
                    invoice.RepairId,

                TicketNumber =
                    repair?.TicketNumber ?? "-",

                // REPAIR TRACKING INFORMATION
                TrackingCode =
                    customer?.TrackingCode ?? "-",

                RepairStatus =
                    repair?.RepairStatus ?? "-",

                CustomerName =
                    customerName,

                CustomerEmail =
                    customerEmail,

                CustomerPhone =
                    customerPhone,

                CustomerAddress =
                    customerAddress,

                DeviceType =
                    repair?.DeviceType ?? "-",

                BrandModel =
                    repair?.BrandModel ?? "-",

                ProblemDescription =
                    repair?.ProblemDescription ?? "-",

                Subtotal =
                    invoice.Subtotal,

                TaxAmount =
                    invoice.TaxAmount,

                TotalAmount =
                    invoice.TotalAmount,

                PaymentStatus =
                    invoice.PaymentStatus,

                PaymentMethod =
                    invoice.PaymentMethod,

                IssuedByName =
                    invoice.IssuedByUser?.FullName
                    ?? "Unknown",

                IssuedAt =
                    invoice.IssuedAt,

                PaidAt =
                    invoice.PaidAt
            };
        }
    }
}
