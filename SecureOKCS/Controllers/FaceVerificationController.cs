using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureOKCS.Data;
using SecureOKCS.Services;
using SecureOKCS.ViewModels;

namespace SecureOKCS.Controllers
{
    [AllowAnonymous]
    [Route("Face")]
    public class FaceVerificationController : Controller
    {
        private const string PendingUserIdKey =
            "PendingFaceUserId";

        private const string PendingRememberMeKey =
            "PendingFaceRememberMe";

        private const string PendingStartedAtKey =
            "PendingFaceStartedAt";

        private const string LegacyAttemptCountKey =
            "FaceVerificationAttempts";

        private const int VerificationTimeoutSeconds =
            180;

        private const double MinimumLivenessScore =
            0.65;

        private const double MinimumSimilarity =
            0.60;

        private readonly ApplicationDbContext
            _context;

        private readonly FaceTemplateService
            _faceTemplateService;

        private readonly FaceLoginSecurityService
            _faceLoginSecurity;

        private readonly AuditLogService
            _auditLogService;

        private readonly ILogger<FaceVerificationController>
            _logger;

        public FaceVerificationController(
            ApplicationDbContext context,
            FaceTemplateService faceTemplateService,
            FaceLoginSecurityService faceLoginSecurity,
            AuditLogService auditLogService,
            ILogger<FaceVerificationController> logger)
        {
            _context = context;

            _faceTemplateService =
                faceTemplateService;

            _faceLoginSecurity =
                faceLoginSecurity;

            _auditLogService =
                auditLogService;

            _logger = logger;
        }

        // DISPLAY FACE ID VERIFICATION PAGE
        [HttpGet("Verify")]
        public async Task<IActionResult> Verify()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction(
                    "Index",
                    "Dashboard");
            }

            if (!TryGetPendingLogin(
                    out int userId,
                    out _))
            {
                TempData["Error"] =
                    "The Face ID session has expired. " +
                    "Please sign in again.";

                return RedirectToAction(
                    "Login",
                    "Account");
            }

            var securityStatus =
                await _faceLoginSecurity
                    .GetStatusAsync(userId);

            if (securityStatus.IsBlocked)
            {
                ClearPendingLogin();

                TempData["Error"] =
                    CreateBlockedMessage(
                        securityStatus);

                return RedirectToAction(
                    "Login",
                    "Account");
            }

            var user =
                await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(user =>
                        user.UserId == userId &&
                        user.IsActive);

            var profile =
                await _context.FaceProfiles
                    .AsNoTracking()
                    .FirstOrDefaultAsync(profile =>
                        profile.UserId == userId &&
                        profile.IsActive);

            if (user == null ||
                profile == null ||
                !user.FaceEnrolled)
            {
                ClearPendingLogin();

                TempData["Error"] =
                    "Face ID is not available for " +
                    "this account.";

                return RedirectToAction(
                    "Login",
                    "Account");
            }

            ViewBag.FullName =
                user.FullName;

            ViewBag.AttemptsRemaining =
                securityStatus.RemainingAttempts;

            return View(
                new FaceVerificationViewModel());
        }

        // PROCESS FACE ID VERIFICATION
        [HttpPost("Verify")]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(128 * 1024)]
        public async Task<IActionResult> Verify(
            FaceVerificationViewModel model)
        {
            if (!TryGetPendingLogin(
                    out int userId,
                    out bool rememberMe))
            {
                TempData["Error"] =
                    "The Face ID session has expired. " +
                    "Please sign in again.";

                return RedirectToAction(
                    "Login",
                    "Account");
            }

            var securityStatus =
                await _faceLoginSecurity
                    .GetStatusAsync(userId);

            if (securityStatus.IsBlocked)
            {
                ClearPendingLogin();

                TempData["Error"] =
                    CreateBlockedMessage(
                        securityStatus);

                return RedirectToAction(
                    "Login",
                    "Account");
            }

            var user =
                await _context.Users
                    .Include(user =>
                        user.Role)
                    .FirstOrDefaultAsync(user =>
                        user.UserId == userId &&
                        user.IsActive);

            var profile =
                await _context.FaceProfiles
                    .FirstOrDefaultAsync(profile =>
                        profile.UserId == userId &&
                        profile.IsActive);

            if (user == null ||
                profile == null ||
                !user.FaceEnrolled)
            {
                ClearPendingLogin();

                TempData["Error"] =
                    "Face ID is not available for " +
                    "this account.";

                return RedirectToAction(
                    "Login",
                    "Account");
            }

            ViewBag.FullName =
                user.FullName;

            if (model.LivenessScore <
                MinimumLivenessScore)
            {
                ModelState.AddModelError(
                    nameof(model.LivenessScore),
                    "Face liveness verification failed.");
            }

            if (!ModelState.IsValid)
            {
                return await HandleFailedVerification(
                    model,
                    user.UserId,
                    profile.FaceProfileId,
                    user.FullName,
                    "Invalid biometric result.");
            }

            try
            {
                float[] storedDescriptor =
                    await _faceTemplateService
                        .LoadTemplateAsync(
                            profile.TemplateReference,
                            profile.TemplateHash);

                double similarity =
                    _faceTemplateService
                        .CalculateSimilarity(
                            storedDescriptor,
                            model.DescriptorJson);

                _logger.LogWarning(
                    "FACE COMPARISON — UserId: {UserId}, " +
                    "Similarity: {Similarity:F4}, " +
                    "Required: {MinimumSimilarity:F2}",
                    user.UserId,
                    similarity,
                    MinimumSimilarity);

                if (similarity <
                    MinimumSimilarity)
                {
                    return await
                        HandleFailedVerification(
                            model,
                            user.UserId,
                            profile.FaceProfileId,
                            user.FullName,
                            $"Face similarity was {similarity:F4}. " +
                            $"Minimum required was " +
                            $"{MinimumSimilarity:F2}.");
                }

                profile.LastVerifiedAt =
                    DateTime.Now;

                await _context.SaveChangesAsync();

                // RESET FAILED ATTEMPTS AFTER
                // SUCCESSFUL FACE VERIFICATION
                await _faceLoginSecurity
                    .ResetAsync(
                        user.UserId);

                var claims = new List<Claim>
                {
                    new Claim(
                        ClaimTypes.NameIdentifier,
                        user.UserId.ToString()),

                    new Claim(
                        ClaimTypes.Name,
                        user.FullName),

                    new Claim(
                        ClaimTypes.Email,
                        user.Email),

                    new Claim(
                        ClaimTypes.Role,
                        user.Role?.RoleName ??
                            "Customer")
                };

                var identity =
                    new ClaimsIdentity(
                        claims,
                        CookieAuthenticationDefaults
                            .AuthenticationScheme);

                var principal =
                    new ClaimsPrincipal(
                        identity);

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults
                        .AuthenticationScheme,
                    principal,
                    new AuthenticationProperties
                    {
                        IsPersistent =
                            rememberMe,

                        ExpiresUtc =
                            DateTimeOffset.UtcNow
                                .AddMinutes(10),

                        AllowRefresh =
                            true
                    });

                HttpContext.User =
                    principal;

                HttpContext.Session.SetInt32(
                    "UserId",
                    user.UserId);

                ClearPendingLogin();

                await _auditLogService.LogAsync(
                    "FACE_LOGIN_SUCCESS",
                    "face_profiles",
                    profile.FaceProfileId.ToString(),
                    "Face ID verification was successful.");

                TempData["Success"] =
                    "Face ID verification was successful.";

                return RedirectToAction(
                    "Index",
                    "Dashboard");
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Face verification error for " +
                    "UserId {UserId}.",
                    userId);

                ClearPendingLogin();

                TempData["Error"] =
                    "Face ID could not be verified. " +
                    "Please sign in again.";

                return RedirectToAction(
                    "Login",
                    "Account");
            }
        }

        // RECORD AN UNSUCCESSFUL
        // FACE ID VERIFICATION
        private async Task<IActionResult>
            HandleFailedVerification(
                FaceVerificationViewModel model,
                int userId,
                int faceProfileId,
                string fullName,
                string failureReason)
        {
            var securityResult =
                await _faceLoginSecurity
                    .RegisterFailureAsync(
                        userId);

            if (securityResult.IsBlocked)
            {
                await _auditLogService.LogAsync(
                    "FACE_INTRUSION_BLOCKED",
                    "users",
                    userId.ToString(),
                    "Possible intrusion detected. " +
                    $"Target UserId: {userId}. " +
                    $"IP: {securityResult.IpAddress}. " +
                    "Face ID failed three times. " +
                    $"Blocked until UTC: " +
                    $"{securityResult.BlockedUntil:O}. " +
                    $"Reason: {failureReason}");

                ClearPendingLogin();

                TempData["Error"] =
                    CreateBlockedMessage(
                        securityResult);

                return RedirectToAction(
                    "Login",
                    "Account");
            }

            await _auditLogService.LogAsync(
                "FACE_LOGIN_FAILED",
                "face_profiles",
                faceProfileId.ToString(),
                "Face ID verification failed. " +
                $"Target UserId: {userId}. " +
                $"IP: {securityResult.IpAddress}. " +
                $"Remaining attempts: " +
                $"{securityResult.RemainingAttempts}. " +
                $"Reason: {failureReason}");

            ModelState.Clear();

            ModelState.AddModelError(
                string.Empty,
                "Face does not match. " +
                $"Remaining attempts: " +
                $"{securityResult.RemainingAttempts}.");

            model.DescriptorJson =
                string.Empty;

            model.LivenessScore =
                0;

            ViewBag.FullName =
                fullName;

            ViewBag.AttemptsRemaining =
                securityResult.RemainingAttempts;

            return View(
                "Verify",
                model);
        }

        // CHECK THE PENDING LOGIN SESSION
        private bool TryGetPendingLogin(
            out int userId,
            out bool rememberMe)
        {
            userId =
                HttpContext.Session.GetInt32(
                    PendingUserIdKey) ?? 0;

            rememberMe =
                HttpContext.Session.GetInt32(
                    PendingRememberMeKey) == 1;

            string? startedAtValue =
                HttpContext.Session.GetString(
                    PendingStartedAtKey);

            bool validStartedAt =
                long.TryParse(
                    startedAtValue,
                    out long startedAt);

            long currentTime =
                DateTimeOffset.UtcNow
                    .ToUnixTimeSeconds();

            bool sessionValid =
                userId > 0 &&
                validStartedAt &&
                currentTime >= startedAt &&
                currentTime - startedAt <=
                    VerificationTimeoutSeconds;

            if (!sessionValid)
            {
                ClearPendingLogin();
            }

            return sessionValid;
        }

        // CREATE TEMPORARY BLOCKING MESSAGE
        private static string CreateBlockedMessage(
            FaceLoginSecurityResult result)
        {
            int remainingMinutes =
                Math.Max(
                    1,
                    (int)Math.Ceiling(
                        result.RemainingSeconds /
                        60.0));

            return
                "The system detected multiple face " +
                "mismatches. Login attempts from this " +
                "device have been blocked for " +
                $"{remainingMinutes} minute(s).";
        }

        // CLEAR THE PENDING LOGIN SESSION
        private void ClearPendingLogin()
        {
            HttpContext.Session.Remove(
                PendingUserIdKey);

            HttpContext.Session.Remove(
                PendingRememberMeKey);

            HttpContext.Session.Remove(
                PendingStartedAtKey);

            HttpContext.Session.Remove(
                LegacyAttemptCountKey);
        }
    }
}
