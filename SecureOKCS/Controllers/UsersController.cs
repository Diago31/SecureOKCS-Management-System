using System.Security.Claims;
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
    [Authorize(Roles = "Admin")]
    public class UsersController : Controller
    {
        private readonly ApplicationDbContext
            _context;

        private readonly IPasswordHasher<User>
            _passwordHasher;

        private readonly AuditLogService
            _auditLog;

        public UsersController(
            ApplicationDbContext context,
            IPasswordHasher<User> passwordHasher,
            AuditLogService auditLog)
        {
            _context = context;
            _passwordHasher = passwordHasher;
            _auditLog = auditLog;
        }

        // USER MANAGEMENT PAGE
        public async Task<IActionResult> Index()
        {
            var users =
                await _context.Users
                    .Include(user => user.Role)
                    .OrderBy(user => user.FullName)
                    .ToListAsync();

            // Used for the role selection
            // inside the Edit popup.
            ViewBag.Roles =
                await _context.Roles
                    .OrderBy(role => role.RoleName)
                    .ToListAsync();

            return View(users);
        }

        // ADD STAFF PAGE
        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        // SAVE NEW STAFF
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            CreateStaffViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            string email =
                model.Email
                    .Trim()
                    .ToLowerInvariant();

            bool emailExists =
                await _context.Users
                    .AnyAsync(user =>
                        user.Email == email);

            if (emailExists)
            {
                ModelState.AddModelError(
                    nameof(model.Email),
                    "This email address is already used.");

                return View(model);
            }

            var staffRole =
                await _context.Roles
                    .FirstOrDefaultAsync(role =>
                        role.RoleName == "Staff");

            if (staffRole == null)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "The Staff role was not found.");

                return View(model);
            }

            DateTime currentTime =
                DateTime.Now;

            var staff = new User
            {
                RoleId =
                    staffRole.RoleId,

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
                    currentTime,

                UpdatedAt =
                    currentTime
            };

            staff.PasswordHash =
                _passwordHasher.HashPassword(
                    staff,
                    model.Password);

            _context.Users.Add(staff);

            await _context.SaveChangesAsync();

            await _auditLog.LogAsync(
                "CREATE",
                "users",
                staff.UserId.ToString(),
                $"Staff account '{staff.Email}' was created.");

            TempData["Success"] =
                "The Staff account was added successfully.";

            return RedirectToAction(
                nameof(Index));
        }

        // UPDATE USER FROM POPUP
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(
            EditUserViewModel model)
        {
            var user =
                await _context.Users
                    .Include(existingUser =>
                        existingUser.Role)
                    .FirstOrDefaultAsync(
                        existingUser =>
                            existingUser.UserId ==
                            model.UserId);

            if (user == null)
            {
                TempData["Error"] =
                    "The selected user was not found.";

                return RedirectToAction(
                    nameof(Index));
            }

            if (!ModelState.IsValid)
            {
                TempData["Error"] =
                    "The account could not be updated. " +
                    "Please check the entered information.";

                return RedirectToAction(
                    nameof(Index));
            }

            string email =
                model.Email
                    .Trim()
                    .ToLowerInvariant();

            bool emailExists =
                await _context.Users
                    .AnyAsync(existingUser =>
                        existingUser.UserId !=
                            model.UserId &&
                        existingUser.Email ==
                            email);

            if (emailExists)
            {
                TempData["Error"] =
                    "This email address is already used.";

                return RedirectToAction(
                    nameof(Index));
            }

            var selectedRole =
                await _context.Roles
                    .FirstOrDefaultAsync(role =>
                        role.RoleId ==
                        model.RoleId);

            if (selectedRole == null)
            {
                TempData["Error"] =
                    "The selected account role is invalid.";

                return RedirectToAction(
                    nameof(Index));
            }

            int? currentUserId =
                GetCurrentUserId();

            // Prevent the logged-in Admin from
            // removing their own Admin access.
            if (currentUserId ==
                    user.UserId &&
                (selectedRole.RoleName != "Admin" ||
                 !model.IsActive))
            {
                TempData["Error"] =
                    "You cannot remove your own Admin " +
                    "role or deactivate your own account.";

                return RedirectToAction(
                    nameof(Index));
            }

            user.FullName =
                model.FullName.Trim();

            user.Email =
                email;

            user.PhoneNumber =
                string.IsNullOrWhiteSpace(
                    model.PhoneNumber)
                    ? null
                    : model.PhoneNumber.Trim();

            user.RoleId =
                model.RoleId;

            user.IsActive =
                model.IsActive;

            user.UpdatedAt =
                DateTime.Now;

            // Only change the password when
            // a new password was entered.
            if (!string.IsNullOrWhiteSpace(
                    model.NewPassword))
            {
                user.PasswordHash =
                    _passwordHasher.HashPassword(
                        user,
                        model.NewPassword);
            }

            await _context.SaveChangesAsync();

            await _auditLog.LogAsync(
                "UPDATE",
                "users",
                user.UserId.ToString(),
                $"Account '{user.Email}' was updated.");

            TempData["Success"] =
                "The user account was updated successfully.";

            return RedirectToAction(
                nameof(Index));
        }

        // PERMANENTLY DELETE USER
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(
            int id)
        {
            int? currentUserId =
                GetCurrentUserId();

            if (currentUserId == null)
                return Forbid();

            // Prevent Admin from deleting
            // their own account.
            if (currentUserId == id)
            {
                TempData["Error"] =
                    "You cannot delete your own account.";

                return RedirectToAction(
                    nameof(Index));
            }

            var user =
                await _context.Users
                    .Include(existingUser =>
                        existingUser.Role)
                    .FirstOrDefaultAsync(
                        existingUser =>
                            existingUser.UserId ==
                            id);

            if (user == null)
            {
                TempData["Error"] =
                    "The selected user was not found.";

                return RedirectToAction(
                    nameof(Index));
            }

            // Prevent deletion of the final
            // active Administrator account.
            if (user.Role?.RoleName == "Admin")
            {
                bool anotherActiveAdminExists =
                    await _context.Users
                        .AnyAsync(existingUser =>
                            existingUser.UserId != id &&
                            existingUser.IsActive &&
                            existingUser.Role != null &&
                            existingUser.Role.RoleName ==
                                "Admin");

                if (!anotherActiveAdminExists)
                {
                    TempData["Error"] =
                        "The final active Administrator " +
                        "account cannot be deleted.";

                    return RedirectToAction(
                        nameof(Index));
                }
            }

            string deletedEmail =
                user.Email;

            try
            {
                // This permanently removes the account
                // from the users database table.
                _context.Users.Remove(user);

                await _context.SaveChangesAsync();

                await _auditLog.LogAsync(
                    "DELETE",
                    "users",
                    id.ToString(),
                    $"Account '{deletedEmail}' was " +
                    "permanently deleted.");

                TempData["Success"] =
                    "The user account was permanently deleted.";
            }
            catch (DbUpdateException)
            {
                // Restore entity state after a failed deletion.
                _context.Entry(user).State =
                    EntityState.Unchanged;

                TempData["Error"] =
                    "This account cannot be deleted because " +
                    "it is connected to important repair, " +
                    "inventory or invoice records.";
            }

            return RedirectToAction(
                nameof(Index));
        }

        private int? GetCurrentUserId()
        {
            string? userIdValue =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier);

            return int.TryParse(
                userIdValue,
                out int userId)
                    ? userId
                    : null;
        }
    }
}