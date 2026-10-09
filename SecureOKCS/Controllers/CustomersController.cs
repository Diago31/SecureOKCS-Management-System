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
    public class CustomersController : Controller
    {
        private readonly ApplicationDbContext
            _context;

        private readonly CustomerEncryptionService
            _encryption;

        private readonly AuditLogService
            _auditLog;

        public CustomersController(
            ApplicationDbContext context,
            CustomerEncryptionService encryption,
            AuditLogService auditLog)
        {
            _context = context;
            _encryption = encryption;
            _auditLog = auditLog;
        }

        public async Task<IActionResult> Index()
        {
            var customers =
                await _context.Customers
                    .OrderByDescending(customer =>
                        customer.CreatedAt)
                    .ToListAsync();

            bool isAdmin =
                User.IsInRole("Admin");

            var result = customers
                .Select(customer =>
                    ConvertToDisplay(
                        customer,
                        isAdmin))
                .ToList();

            return View(result);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            CustomerCreateViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var customer = new Customer
            {
                FullNameEncrypted =
                    _encryption.Encrypt(
                        model.FullName),

                EmailEncrypted =
                    _encryption.Encrypt(
                        model.Email),

                PhoneEncrypted =
                    _encryption.Encrypt(
                        model.PhoneNumber),

                AddressEncrypted =
                    string.IsNullOrWhiteSpace(
                        model.Address)
                        ? null
                        : _encryption.Encrypt(
                            model.Address),

                PhoneLastFour =
                    _encryption.LastFour(
                        model.PhoneNumber),

                TrackingCode =
                    "OKCS-" +
                    Guid.NewGuid()
                        .ToString("N")[..10]
                        .ToUpperInvariant(),

                CreatedAt =
                    DateTime.Now,

                UpdatedAt =
                    DateTime.Now
            };

            _context.Customers.Add(
                customer);

            await _context.SaveChangesAsync();

            await _auditLog.LogAsync(
                "CREATE",
                "customers",
                customer.CustomerId.ToString(),
                "New customer record created " +
                "with encrypted personal data.");

            TempData["Success"] =
                "Customer berjaya ditambah.";

            return RedirectToAction(
                nameof(Index));
        }

        public async Task<IActionResult> Details(
            int id)
        {
            var customer =
                await _context.Customers
                    .FirstOrDefaultAsync(customer =>
                        customer.CustomerId == id);

            if (customer == null)
                return NotFound();

            bool isAdmin =
                User.IsInRole("Admin");

            await _auditLog.LogAsync(
                "VIEW",
                "customers",
                customer.CustomerId.ToString(),
                "Customer details viewed.");

            return View(
                ConvertToDisplay(
                    customer,
                    isAdmin));
        }

        private CustomerDisplayViewModel
            ConvertToDisplay(
                Customer customer,
                bool isAdmin)
        {
            return new CustomerDisplayViewModel
            {
                CustomerId =
                    customer.CustomerId,

                FullName = _encryption.Decrypt(customer.FullNameEncrypted),

                Email =
                    isAdmin
                        ? _encryption.Decrypt(
                            customer
                                .EmailEncrypted)
                        : _encryption.MaskEmail(
                            customer
                                .EmailEncrypted),

                PhoneNumber =
                    isAdmin
                        ? _encryption.Decrypt(
                            customer
                                .PhoneEncrypted)
                        : "******" +
                          customer.PhoneLastFour,

                Address =
                    isAdmin &&
                    customer.AddressEncrypted != null
                        ? _encryption.Decrypt(
                            customer
                                .AddressEncrypted)
                        : "Hidden",

                TrackingCode =
                    customer.TrackingCode,

                CreatedAt =
                    customer.CreatedAt
            };
        }
    }
}