using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureOKCS.Data;
using SecureOKCS.Models;
using SecureOKCS.Services;
using SecureOKCS.ViewModels;

namespace SecureOKCS.Controllers
{
    public class AccountController : Controller
    {
        private const string
            PendingUserIdKey =
                "PendingFaceUserId";

        private const string
            PendingRememberMeKey =
                "PendingFaceRememberMe";

        private const string
            PendingStartedAtKey =
                "PendingFaceStartedAt";

        private const string
            AttemptCountKey =
                "FaceVerificationAttempts";

        private readonly ApplicationDbContext
            _context;

        private readonly IPasswordHasher<User>
            _passwordHasher;

        private readonly AuditLogService
            _auditLog;

        private readonly FaceLoginSecurityService
            _faceLoginSecurity;

        public AccountController(
            ApplicationDbContext context,
            IPasswordHasher<User> passwordHasher,
            AuditLogService auditLog,
            FaceLoginSecurityService
                faceLoginSecurity)
        {
            _context =
                context;

            _passwordHasher =
                passwordHasher;

            _auditLog =
                auditLog;

            _faceLoginSecurity =
                faceLoginSecurity;
        }

        // DISPLAY REGISTRATION PAGE
        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        // CREATE NEW CUSTOMER ACCOUNT
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(
            RegisterViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            string email =
                model.Email
                    .Trim()
                    .ToLowerInvariant();

            bool emailExists =
                await _context.Users.AnyAsync(
                    user =>
                        user.Email == email);

            if (emailExists)
            {
                ModelState.AddModelError(
                    nameof(model.Email),
                    "Email is already in use.");

                return View(model);
            }

            var customerRole =
                await _context.Roles
                    .FirstOrDefaultAsync(role =>
                        role.RoleName ==
                            "Customer");

            if (customerRole == null)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Customer role was not found.");

                return View(model);
            }

            var user = new User
            {
                RoleId =
                    customerRole.RoleId,

                FullName =
                    model.FullName.Trim(),

                Email =
                    email,

                PhoneNumber =
                    model.PhoneNumber?.Trim(),

                IsActive =
                    true,

                FaceEnrolled =
                    false,

                CreatedAt =
                    DateTime.Now,

                UpdatedAt =
                    DateTime.Now
            };

            user.PasswordHash =
                _passwordHasher.HashPassword(
                    user,
                    model.Password);

            _context.Users.Add(user);

            await _context.SaveChangesAsync();

            await _auditLog.LogAsync(
                "REGISTER",
                "users",
                user.UserId.ToString(),
                "New customer account registered.");

            TempData["Success"] =
                "Registration completed successfully. " +
                "Please sign in.";

            return RedirectToAction(
                nameof(Login));
        }

        // DISPLAY LOGIN PAGE
        [HttpGet]
        public IActionResult Login()
        {
            if (User.Identity?.IsAuthenticated ==
                true)
            {
                return RedirectToAction(
                    "Index",
                    "Dashboard");
            }

            ClearPendingFaceLogin();

            return View();
        }

        // PROCESS LOGIN
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(
            LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            ClearPendingFaceLogin();

            string email =
                model.Email
                    .Trim()
                    .ToLowerInvariant();

            var user = await _context.Users
                .Include(user =>
                    user.Role)
                .FirstOrDefaultAsync(user =>
                    user.Email == email);

            if (user == null ||
                !user.IsActive)
            {
                await _auditLog.LogAsync(
                    "LOGIN_FAILED",
                    "users",
                    user?.UserId.ToString(),
                    "Login failed because the account " +
                    "was not found or inactive.");

                ModelState.AddModelError(
                    string.Empty,
                    "The email address or password is incorrect.");

                return View(model);
            }

            // CHECK WHETHER THIS IP ADDRESS
            // IS BLOCKED FOR THIS ACCOUNT
            var faceSecurityStatus =
                await _faceLoginSecurity
                    .GetStatusAsync(
                        user.UserId);

            if (faceSecurityStatus.IsBlocked)
            {
                int remainingMinutes =
                    Math.Max(
                        1,
                        (int)Math.Ceiling(
                            faceSecurityStatus
                                .RemainingSeconds /
                            60.0));

                await _auditLog.LogAsync(
                    "BLOCKED_LOGIN_ATTEMPT",
                    "users",
                    user.UserId.ToString(),
                    "Login attempted while Face ID " +
                    "intrusion block was active. " +
                    $"IP: " +
                    $"{faceSecurityStatus.IpAddress}. " +
                    $"Remaining seconds: " +
                    $"{faceSecurityStatus.RemainingSeconds}.");

                ModelState.AddModelError(
                    string.Empty,
                    "Login attempts from this device " +
                    "have been blocked because of " +
                    "multiple Face ID mismatches. " +
                    $"Try again in " +
                    $"{remainingMinutes} minute(s).");

                return View(model);
            }

            var passwordResult =
                _passwordHasher
                    .VerifyHashedPassword(
                        user,
                        user.PasswordHash,
                        model.Password);

            if (passwordResult ==
                PasswordVerificationResult.Failed)
            {
                await _auditLog.LogAsync(
                    "LOGIN_FAILED",
                    "users",
                    user.UserId.ToString(),
                    "Login failed because the " +
                    "password was incorrect.");

                ModelState.AddModelError(
                    string.Empty,
                    "The email address or password is incorrect.");

                return View(model);
            }

            // UPDATE THE HASH WHEN THE PASSWORD
            // HASHER FORMAT HAS CHANGED
            if (passwordResult ==
                PasswordVerificationResult
                    .SuccessRehashNeeded)
            {
                user.PasswordHash =
                    _passwordHasher.HashPassword(
                        user,
                        model.Password);

                user.UpdatedAt =
                    DateTime.Now;

                await _context.SaveChangesAsync();
            }

            // DO NOT CREATE THE LOGIN COOKIE
            // BEFORE FACE ID VERIFICATION
            if (user.FaceEnrolled)
            {
                bool faceProfileAvailable =
                    await _context.FaceProfiles
                        .AsNoTracking()
                        .AnyAsync(profile =>
                            profile.UserId ==
                                user.UserId &&
                            profile.IsActive);

                if (!faceProfileAvailable)
                {
                    await _auditLog.LogAsync(
                        "FACE_CONFIGURATION_ERROR",
                        "face_profiles",
                        user.UserId.ToString(),
                        "User was marked as face " +
                        "enrolled but no active face " +
                        "profile was found.");

                    ModelState.AddModelError(
                        string.Empty,
                        "Face ID verification could not start. " +
                        "Please contact an administrator.");

                    return View(model);
                }

                HttpContext.Session.SetInt32(
                    PendingUserIdKey,
                    user.UserId);

                HttpContext.Session.SetInt32(
                    PendingRememberMeKey,
                    model.RememberMe
                        ? 1
                        : 0);

                HttpContext.Session.SetString(
                    PendingStartedAtKey,
                    DateTimeOffset.UtcNow
                        .ToUnixTimeSeconds()
                        .ToString());

                HttpContext.Session.SetInt32(
                    AttemptCountKey,
                    0);

                await _auditLog.LogAsync(
                    "PASSWORD_VERIFIED",
                    "users",
                    user.UserId.ToString(),
                    "Password verified. Waiting " +
                    "for Face ID verification.");

                return RedirectToAction(
                    "Verify",
                    "FaceVerification");
            }

            // PASSWORD LOGIN FOR USERS WHO HAVE
            // NOT ENROLLED FACE ID
            await SignInUserAsync(
                user,
                model.RememberMe);

            await _auditLog.LogAsync(
                "LOGIN",
                "users",
                user.UserId.ToString(),
                "User logged in successfully " +
                "using password.");

            return RedirectToAction(
                "Index",
                "Dashboard");
        }

        // LOGOUT
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            string? userId =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier);

            await _auditLog.LogAsync(
                "LOGOUT",
                "users",
                userId,
                "User logged out successfully.");

            await HttpContext.SignOutAsync(
                CookieAuthenticationDefaults
                    .AuthenticationScheme);

            HttpContext.Session.Clear();

            return RedirectToAction(
                nameof(Login));
        }

        // KEEP ACTIVE ADMIN SESSION ALIVE
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult>
            KeepAlive()
        {
            var authenticationResult =
                await HttpContext.AuthenticateAsync(
                    CookieAuthenticationDefaults
                        .AuthenticationScheme);

            if (!authenticationResult.Succeeded ||
                authenticationResult.Principal == null)
            {
                return Unauthorized();
            }

            var authenticationProperties =
                authenticationResult.Properties
                ?? new AuthenticationProperties();

            authenticationProperties.IssuedUtc =
                DateTimeOffset.UtcNow;

            authenticationProperties.ExpiresUtc =
                DateTimeOffset.UtcNow
                    .AddMinutes(10);

            authenticationProperties.AllowRefresh =
                true;

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults
                    .AuthenticationScheme,
                authenticationResult.Principal,
                authenticationProperties);

            HttpContext.Session.SetString(
                "LastAdminActivityUtc",
                DateTime.UtcNow.ToString("O"));

            return NoContent();
        }

        // AUTOMATIC ADMIN SESSION TIMEOUT
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult>
            SessionTimeout()
        {
            string? userId =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier);

            try
            {
                await _auditLog.LogAsync(
                    "SESSION_TIMEOUT",
                    "users",
                    userId,
                    "Administrator session expired " +
                    "after 1 minute of inactivity.");
            }
            catch
            {
                // Logout must continue even when
                // the audit log cannot be recorded
            }

            await HttpContext.SignOutAsync(
                CookieAuthenticationDefaults
                    .AuthenticationScheme);

            HttpContext.Session.Clear();

            TempData["SessionExpired"] =
                "Your session expired after " +
                "1 minutes of inactivity. " +
                "Please sign in again.";

            return RedirectToAction(
                nameof(Login));
        }

        // ACCESS DENIED
        public IActionResult AccessDenied()
        {
            return View();
        }

        // CREATE AUTHENTICATION COOKIE
        private async Task SignInUserAsync(
            User user,
            bool rememberMe)
        {
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
        }

        // CLEAR PENDING FACE LOGIN SESSION
        private void ClearPendingFaceLogin()
        {
            HttpContext.Session.Remove(
                PendingUserIdKey);

            HttpContext.Session.Remove(
                PendingRememberMeKey);

            HttpContext.Session.Remove(
                PendingStartedAtKey);

            HttpContext.Session.Remove(
                AttemptCountKey);
        }
    }
}