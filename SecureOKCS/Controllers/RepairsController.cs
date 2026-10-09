using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SecureOKCS.Data;
using SecureOKCS.Models;
using SecureOKCS.Services;
using SecureOKCS.ViewModels;

namespace SecureOKCS.Controllers
{
    [Authorize(Roles = "Admin,Staff")]
    public class RepairsController : Controller
    {
        private readonly ApplicationDbContext
            _context;

        private readonly CustomerEncryptionService
            _encryption;

        private readonly EmailNotificationService
            _emailNotification;

        private readonly AuditLogService
            _auditLog;

        public RepairsController(
            ApplicationDbContext context,
            CustomerEncryptionService encryption,
            EmailNotificationService emailNotification,
            AuditLogService auditLog)
        {
            _context = context;
            _encryption = encryption;
            _emailNotification = emailNotification;
            _auditLog = auditLog;
        }

        // SENARAI REPAIR
        public async Task<IActionResult> Index()
        {
            var repairs =
                await _context.RepairTickets
                    .Include(repair =>
                        repair.Customer)
                    .Include(repair =>
                        repair.Technician)
                    .OrderByDescending(repair =>
                        repair.ReceivedAt)
                    .ToListAsync();

            bool isAdmin =
                User.IsInRole("Admin");

            var result = repairs
                .Select(repair =>
                    ConvertToDisplay(
                        repair,
                        isAdmin))
                .ToList();

            return View(result);
        }

        // PAPARKAN BORANG CREATE
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            await LoadDropdowns();

            return View();
        }

        // SIMPAN REPAIR TICKET
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            RepairCreateViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await LoadDropdowns(
                    model.CustomerId,
                    model.TechnicianId);

                return View(model);
            }

            bool customerExists =
                await _context.Customers
                    .AnyAsync(customer =>
                        customer.CustomerId ==
                        model.CustomerId);

            if (!customerExists)
            {
                ModelState.AddModelError(
                    nameof(model.CustomerId),
                    "Customer was not found.");

                await LoadDropdowns(
                    model.CustomerId,
                    model.TechnicianId);

                return View(model);
            }

            var repair = new RepairTicket
            {
                TicketNumber =
                    "RPR-" +
                    DateTime.Now.ToString(
                        "yyyy") +
                    "-" +
                    Guid.NewGuid()
                        .ToString("N")[..8]
                        .ToUpperInvariant(),

                CustomerId =
                    model.CustomerId,

                TechnicianId =
                    model.TechnicianId,

                DeviceType =
                    model.DeviceType.Trim(),

                BrandModel =
                    model.BrandModel.Trim(),

                SerialNumber =
                    model.SerialNumber?.Trim(),

                ProblemDescription =
                    model.ProblemDescription.Trim(),

                RepairStatus =
                    "Received",

                EstimatedCost =
                    model.EstimatedCost,

                FinalCost =
                    0,

                ReceivedAt =
                    DateTime.Now,

                UpdatedAt =
                    DateTime.Now
            };

            _context.RepairTickets.Add(
                repair);

            await _context.SaveChangesAsync();

            // AUDIT LOG CREATE REPAIR
            await _auditLog.LogAsync(
                "CREATE",
                "repair_tickets",
                repair.RepairId.ToString(),
                $"Repair ticket " +
                $"{repair.TicketNumber} created.");

            TempData["Success"] =
                "Repair ticket created successfully.";

            return RedirectToAction(
                nameof(Index));
        }

        // PAPARKAN DETAILS REPAIR
        public async Task<IActionResult> Details(
            int id)
        {
            var repair =
                await _context.RepairTickets
                    .Include(repair =>
                        repair.Customer)
                    .Include(repair =>
                        repair.Technician)
                    .FirstOrDefaultAsync(repair =>
                        repair.RepairId == id);

            if (repair == null)
                return NotFound();

            bool isAdmin =
                User.IsInRole("Admin");

            var model =
                ConvertToDisplay(
                    repair,
                    isAdmin);

            return View(model);
        }

        // PAPARKAN BORANG UPDATE STATUS
        [HttpGet]
        public async Task<IActionResult> UpdateStatus(
            int id)
        {
            var repair =
                await _context.RepairTickets
                    .FirstOrDefaultAsync(repair =>
                        repair.RepairId == id);

            if (repair == null)
                return NotFound();

            var model =
                new RepairStatusUpdateViewModel
                {
                    RepairId =
                        repair.RepairId,

                    TicketNumber =
                        repair.TicketNumber,

                    CurrentStatus =
                        repair.RepairStatus,

                    TechnicianNotes =
                        repair.TechnicianNotes,

                    FinalCost =
                        repair.FinalCost
                };

            return View(model);
        }

        // SIMPAN PERUBAHAN STATUS
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(
            RepairStatusUpdateViewModel model)
        {
            string[] allowedStatuses =
            {
                "Received",
                "Inspecting",
                "Waiting For Parts",
                "Repairing",
                "Quality Check",
                "Ready For Pickup",
                "Collected",
                "Cancelled"
            };

            if (!allowedStatuses.Contains(
                    model.NewStatus))
            {
                ModelState.AddModelError(
                    nameof(model.NewStatus),
                    "Invalid repair status.");
            }

            var repair =
                await _context.RepairTickets
                    .FirstOrDefaultAsync(repair =>
                        repair.RepairId ==
                        model.RepairId);

            if (repair == null)
                return NotFound();

            if (!ModelState.IsValid)
            {
                model.TicketNumber =
                    repair.TicketNumber;

                model.CurrentStatus =
                    repair.RepairStatus;

                return View(model);
            }

            string? userIdValue =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier);

            if (!int.TryParse(
                    userIdValue,
                    out int userId))
            {
                return Forbid();
            }

            string previousStatus =
                repair.RepairStatus;

            repair.RepairStatus =
                model.NewStatus;

            repair.TechnicianNotes =
                model.TechnicianNotes;

            repair.FinalCost =
                model.FinalCost;

            repair.UpdatedAt =
                DateTime.Now;

            if (model.NewStatus ==
                    "Ready For Pickup" ||
                model.NewStatus ==
                    "Collected")
            {
                repair.CompletedAt =
                    DateTime.Now;
            }

            var history =
                new RepairStatusHistory
                {
                    RepairId =
                        repair.RepairId,

                    ChangedBy =
                        userId,

                    PreviousStatus =
                        previousStatus,

                    NewStatus =
                        model.NewStatus,

                    StatusNotes =
                        model.StatusNotes,

                    ChangedAt =
                        DateTime.Now
                };

            _context.RepairStatusHistories.Add(
                history);

            await _context.SaveChangesAsync();

            // AUDIT LOG UPDATE STATUS
            await _auditLog.LogAsync(
                "UPDATE",
                "repair_tickets",
                repair.RepairId.ToString(),
                $"Repair status changed from " +
                $"{previousStatus} to " +
                $"{model.NewStatus}.");

            // SEND AUTOMATIC STATUS EMAIL TO CUSTOMER
            string? emailStatus = null;

            try
            {
                await _emailNotification
                    .SendRepairStatusEmailAsync(
                        repair.RepairId,
                        model.NewStatus);

                emailStatus =
                    await _context.EmailNotifications
                        .AsNoTracking()
                        .Where(notification =>
                            notification.RepairId ==
                            repair.RepairId)
                        .OrderByDescending(notification =>
                            notification.CreatedAt)
                        .Select(notification =>
                            notification.NotificationStatus)
                        .FirstOrDefaultAsync();
            }
            catch
            {
                emailStatus =
                    "Failed";
            }

            TempData["Success"] =
                "Repair status updated successfully.";

            if (string.Equals(
                    emailStatus,
                    "Sent",
                    StringComparison.OrdinalIgnoreCase))
            {
                TempData["EmailSuccess"] =
                    "A status update email was sent " +
                    "to the customer.";
            }
            else if (string.Equals(
                         emailStatus,
                         "Skipped",
                         StringComparison.OrdinalIgnoreCase))
            {
                TempData["Info"] =
                    "The repair status was updated, " +
                    "but email notifications are disabled.";
            }
            else
            {
                TempData["Warning"] =
                    "The repair status was updated, " +
                    "but the customer email could not be sent.";
            }

            return RedirectToAction(
                nameof(Details),
                new
                {
                    id = repair.RepairId
                });
        }

        // TUKAR ENTITY KEPADA VIEWMODEL
        private RepairDisplayViewModel
            ConvertToDisplay(
                RepairTicket repair,
                bool isAdmin)
        {
            string customerName =
                "Unknown";

            if (repair.Customer != null)
            {
                customerName = _encryption.Decrypt(
                    repair.Customer.FullNameEncrypted);
            }

            return new RepairDisplayViewModel
            {
                RepairId =
                    repair.RepairId,

                TicketNumber =
                    repair.TicketNumber,

                CustomerName =
                    customerName,

                DeviceType =
                    repair.DeviceType,

                BrandModel =
                    repair.BrandModel,

                SerialNumber =
                    repair.SerialNumber,

                ProblemDescription =
                    repair.ProblemDescription,

                TechnicianNotes =
                    repair.TechnicianNotes,

                TechnicianName =
                    repair.Technician?.FullName
                    ?? "Not Assigned",

                RepairStatus =
                    repair.RepairStatus,

                EstimatedCost =
                    repair.EstimatedCost,

                FinalCost =
                    repair.FinalCost,

                ReceivedAt =
                    repair.ReceivedAt,

                CompletedAt =
                    repair.CompletedAt
            };
        }

        // ISI PILIHAN CUSTOMER DAN TECHNICIAN
        private async Task LoadDropdowns(
            int? selectedCustomer = null,
            int? selectedTechnician = null)
        {
            var customers =
                await _context.Customers
                    .OrderBy(customer =>
                        customer.CustomerId)
                    .ToListAsync();

            bool isAdmin =
                User.IsInRole("Admin");

            var customerOptions =
                customers.Select(customer =>
                    new
                    {
                        Id = customer.CustomerId,
                        Name = _encryption.Decrypt(customer.FullNameEncrypted)
                    });

            var technicians =
                await _context.Users
                    .Include(user =>
                        user.Role)
                    .Where(user =>
                        user.IsActive &&
                        user.Role != null &&
                        user.Role.RoleName ==
                            "Staff")
                    .OrderBy(user =>
                        user.FullName)
                    .ToListAsync();

            ViewBag.Customers =
                new SelectList(
                    customerOptions,
                    "Id",
                    "Name",
                    selectedCustomer);

            ViewBag.Technicians =
                new SelectList(
                    technicians,
                    "UserId",
                    "FullName",
                    selectedTechnician);
        }
    }
}