using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureOKCS.Data;
using SecureOKCS.Models;
using SecureOKCS.ViewModels;

namespace SecureOKCS.Controllers
{
    [AllowAnonymous]
    [ResponseCache(
        NoStore = true,
        Location = ResponseCacheLocation.None)]
    public class TrackingController : Controller
    {
        private const string TrackingCodeSessionKey =
            "RepairTrackingCode";

        private const string TicketNumberSessionKey =
            "RepairTicketNumber";

        private readonly ApplicationDbContext
            _context;

        public TrackingController(
            ApplicationDbContext context)
        {
            _context = context;
        }

        // PAPARKAN BORANG TRACKING
        [HttpGet]
        public IActionResult Index()
        {
            return View(
                new RepairTrackingSearchViewModel());
        }

        // SEMAK TRACKING CODE DAN TICKET
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(
            RepairTrackingSearchViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            string trackingCode =
                model.TrackingCode
                    .Trim()
                    .ToUpperInvariant();

            string ticketNumber =
                model.TicketNumber
                    .Trim()
                    .ToUpperInvariant();

            var repair =
                await FindRepairAsync(
                    trackingCode,
                    ticketNumber);

            if (repair == null)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Tracking Code or Ticket " +
                    "number cannot be found");

                return View(model);
            }

            // SIMPAN DALAM SESSION SUPAYA
            // KOD TIDAK TERDEDAH DALAM URL
            HttpContext.Session.SetString(
                TrackingCodeSessionKey,
                trackingCode);

            HttpContext.Session.SetString(
                TicketNumberSessionKey,
                ticketNumber);

            return RedirectToAction(
                nameof(Result));
        }

        // PAPARKAN RESULT TRACKING
        [HttpGet]
        public async Task<IActionResult> Result()
        {
            string? trackingCode =
                HttpContext.Session.GetString(
                    TrackingCodeSessionKey);

            string? ticketNumber =
                HttpContext.Session.GetString(
                    TicketNumberSessionKey);

            if (string.IsNullOrWhiteSpace(
                    trackingCode) ||
                string.IsNullOrWhiteSpace(
                    ticketNumber))
            {
                TempData["Info"] =
                    "Enter tracking code " +
                    "and ticket code first";

                return RedirectToAction(
                    nameof(Index));
            }

            var model =
                await BuildResultAsync(
                    trackingCode,
                    ticketNumber);

            if (model == null)
            {
                HttpContext.Session.Remove(
                    TrackingCodeSessionKey);

                HttpContext.Session.Remove(
                    TicketNumberSessionKey);

                TempData["Info"] =
                    "Repair tracking cannot be found.";

                return RedirectToAction(
                    nameof(Index));
            }

            return View(model);
        }

        // AUTO-REFRESH CURRENT STATUS
        [HttpGet]
        public async Task<IActionResult> CurrentStatus()
        {
            string? trackingCode =
                HttpContext.Session.GetString(
                    TrackingCodeSessionKey);

            string? ticketNumber =
                HttpContext.Session.GetString(
                    TicketNumberSessionKey);

            if (string.IsNullOrWhiteSpace(
                    trackingCode) ||
                string.IsNullOrWhiteSpace(
                    ticketNumber))
            {
                return Unauthorized(
                    new
                    {
                        message =
                            "Tracking session expired."
                    });
            }

            var repair =
                await FindRepairAsync(
                    trackingCode,
                    ticketNumber);

            if (repair == null)
                return NotFound();

            return Json(
                new
                {
                    repairStatus =
                        repair.RepairStatus,

                    updatedAt =
                        repair.UpdatedAt.ToString(
                            "O"),

                    completedAt =
                        repair.CompletedAt?
                            .ToString("O")
                });
        }

        // CARI REPAIR MENGGUNAKAN
        // TRACKING CODE DAN TICKET NUMBER
        private async Task<RepairTicket?>
            FindRepairAsync(
                string trackingCode,
                string ticketNumber)
        {
            return await _context.RepairTickets
                .AsNoTracking()
                .Include(repair =>
                    repair.Customer)
                .FirstOrDefaultAsync(repair =>
                    repair.TicketNumber ==
                        ticketNumber &&

                    repair.Customer != null &&

                    repair.Customer.TrackingCode ==
                        trackingCode);
        }

        // BINA RESULT DAN STATUS HISTORY
        private async Task<RepairTrackingResultViewModel?>
            BuildResultAsync(
                string trackingCode,
                string ticketNumber)
        {
            var repair =
                await FindRepairAsync(
                    trackingCode,
                    ticketNumber);

            if (repair == null)
                return null;

            var statusHistory =
                await _context
                    .RepairStatusHistories
                    .AsNoTracking()
                    .Where(history =>
                        history.RepairId ==
                        repair.RepairId)
                    .OrderBy(history =>
                        history.ChangedAt)
                    .Select(history =>
                        new TrackingHistoryItemViewModel
                        {
                            Status =
                                history.NewStatus,

                            Notes =
                                history.StatusNotes,

                            ChangedAt =
                                history.ChangedAt
                        })
                    .ToListAsync();

            statusHistory.Insert(
                0,
                new TrackingHistoryItemViewModel
                {
                    Status =
                        "Received",

                    Notes =
                        "Device already received " +
                        "OK Computer Solution.",

                    ChangedAt =
                        repair.ReceivedAt
                });

            return new RepairTrackingResultViewModel
            {
                TicketNumber =
                    repair.TicketNumber,

                DeviceType =
                    repair.DeviceType,

                BrandModel =
                    repair.BrandModel,

                RepairStatus =
                    repair.RepairStatus,

                ReceivedAt =
                    repair.ReceivedAt,

                UpdatedAt =
                    repair.UpdatedAt,

                CompletedAt =
                    repair.CompletedAt,

                StatusHistory =
                    statusHistory
            };
        }
    }
}