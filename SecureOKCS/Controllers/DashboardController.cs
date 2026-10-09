using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureOKCS.Data;

namespace SecureOKCS.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext
            _context;

        public DashboardController(
            ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            string role =
                User.FindFirst(
                    ClaimTypes.Role)?.Value
                ?? "Unknown";

            ViewBag.FullName =
                User.Identity?.Name;

            ViewBag.Role =
                role;

            // INTRUSION ALERT HANYA
            // UNTUK ADMIN
            if (role == "Admin")
            {
                DateTime lastTwentyFourHours =
                    DateTime.Now.AddHours(-24);

                var intrusionQuery =
                    _context.AuditLogs
                        .AsNoTracking()
                        .Where(log =>
                            log.ActionType ==
                                "FACE_INTRUSION_BLOCKED");

                ViewBag.IntrusionCount =
                    await intrusionQuery
                        .CountAsync(log =>
                            log.CreatedAt >=
                                lastTwentyFourHours);

                ViewBag.IntrusionAlerts =
                    await intrusionQuery
                        .OrderByDescending(log =>
                            log.CreatedAt)
                        .Take(5)
                        .ToListAsync();

                var latestBlockedAttempt =
                    await _context.AuditLogs
                        .AsNoTracking()
                        .Where(log =>
                            log.ActionType ==
                                "BLOCKED_LOGIN_ATTEMPT")
                        .OrderByDescending(log =>
                            log.CreatedAt)
                        .FirstOrDefaultAsync();

                ViewBag.LatestBlockedAttempt =
                    latestBlockedAttempt;
            }

            return View();
        }
    }
}