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
    public class FaceController : Controller
    {
        private const double MinimumLivenessScore =
            0.65;

        private readonly ApplicationDbContext
            _context;

        private readonly FaceTemplateService
            _faceTemplateService;

        private readonly AuditLogService
            _auditLogService;

        private readonly ILogger<FaceController>
            _logger;

        public FaceController(
            ApplicationDbContext context,
            FaceTemplateService faceTemplateService,
            AuditLogService auditLogService,
            ILogger<FaceController> logger)
        {
            _context = context;
            _faceTemplateService =
                faceTemplateService;
            _auditLogService =
                auditLogService;
            _logger = logger;
        }

        // PAPARKAN HALAMAN FACE ENROLLMENT
        [HttpGet]
        public async Task<IActionResult> Enroll()
        {
            if (!TryGetCurrentUserId(
                    out int userId))
            {
                return Forbid();
            }

            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(user =>
                    user.UserId == userId);

            if (user == null)
                return NotFound();

            await LoadEnrollmentInformation(
                user);

            return View(
                new FaceEnrollmentViewModel());
        }

        // SIMPAN FACE ENROLLMENT
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(128 * 1024)]
        public async Task<IActionResult> Enroll(
            FaceEnrollmentViewModel model)
        {
            if (!TryGetCurrentUserId(
                    out int userId))
            {
                return Forbid();
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(user =>
                    user.UserId == userId);

            if (user == null)
                return NotFound();

            if (!model.ConsentGiven)
            {
                ModelState.AddModelError(
                    nameof(model.ConsentGiven),
                    "You need to provide consent " +
                    "before registering Face ID");
            }

            if (model.LivenessScore <
                MinimumLivenessScore)
            {
                ModelState.AddModelError(
                    nameof(model.LivenessScore),
                    "Face ID failed " +
                    "Please try again");
            }

            if (!ModelState.IsValid)
            {
                await LoadEnrollmentInformation(
                    user);

                return View(model);
            }

            var existingProfile =
                await _context.FaceProfiles
                    .FirstOrDefaultAsync(profile =>
                        profile.UserId == userId);

            string? oldTemplateReference =
                existingProfile?
                    .TemplateReference;

            string? newTemplateReference =
                null;

            await using var transaction =
                await _context.Database
                    .BeginTransactionAsync();

            try
            {
                var savedTemplate =
                    await _faceTemplateService
                        .SaveTemplateAsync(
                            model.DescriptorJson);

                newTemplateReference =
                    savedTemplate.Reference;

                FaceProfile profile;

                if (existingProfile == null)
                {
                    profile = new FaceProfile
                    {
                        UserId = userId,

                        TemplateReference =
                            savedTemplate.Reference,

                        TemplateHash =
                            savedTemplate.Hash,

                        LivenessRequired =
                            true,

                        IsActive =
                            true,

                        EnrolledAt =
                            DateTime.Now,

                        LastVerifiedAt =
                            null
                    };

                    _context.FaceProfiles.Add(
                        profile);
                }
                else
                {
                    profile =
                        existingProfile;

                    profile.TemplateReference =
                        savedTemplate.Reference;

                    profile.TemplateHash =
                        savedTemplate.Hash;

                    profile.LivenessRequired =
                        true;

                    profile.IsActive =
                        true;

                    profile.EnrolledAt =
                        DateTime.Now;

                    profile.LastVerifiedAt =
                        null;
                }

                user.FaceEnrolled =
                    true;

                user.UpdatedAt =
                    DateTime.Now;

                await _context.SaveChangesAsync();

                string auditDetails =
                    existingProfile == null
                        ? "Face ID successfully registered."
                        : "Face ID successfully registered";

                await _auditLogService.LogAsync(
                    "FACE_ENROLLMENT",
                    "face_profiles",
                    profile.FaceProfileId.ToString(),
                    auditDetails);

                await transaction.CommitAsync();

                // TEMPLATE LAMA DIPADAM SELEPAS
                // DATABASE BERJAYA DIKEMAS KINI
                if (!string.IsNullOrWhiteSpace(
                        oldTemplateReference) &&
                    oldTemplateReference !=
                        newTemplateReference)
                {
                    try
                    {
                        _faceTemplateService
                            .DeleteTemplate(
                                oldTemplateReference);
                    }
                    catch (Exception exception)
                    {
                        _logger.LogWarning(
                            exception,
                            "Old Face ID Template " +
                            "could not be deleted" +
                            "UserId {UserId}.",
                            userId);
                    }
                }

                TempData["Success"] =
                    existingProfile == null
                        ? "Face ID successfully registered."
                        : "Face ID successfuly registered.";

                return RedirectToAction(
                    nameof(Enroll));
            }
            catch (Exception exception)
            {
                await transaction.RollbackAsync();

                // PADAM FAIL BAHARU JIKA
                // DATABASE GAGAL DISIMPAN
                if (!string.IsNullOrWhiteSpace(
                        newTemplateReference))
                {
                    try
                    {
                        _faceTemplateService
                            .DeleteTemplate(
                                newTemplateReference);
                    }
                    catch (Exception deleteException)
                    {
                        _logger.LogWarning(
                            deleteException,
                            "Template Face ID Failed " +
                            "Face ID cannot be cleaned.");
                    }
                }

                _logger.LogError(
                    exception,
                    "Face enrollment failed " +
                    "UserId {UserId}.",
                    userId);

                ModelState.AddModelError(
                    string.Empty,
                    "Face ID cannot be stored. " +
                    "Scan face again.");

                await LoadEnrollmentInformation(
                    user);

                return View(model);
            }
        }

        // MAKLUMAT UNTUK HALAMAN ENROLLMENT
        private async Task
            LoadEnrollmentInformation(
                User user)
        {
            bool profileIsActive =
                await _context.FaceProfiles
                    .AsNoTracking()
                    .AnyAsync(profile =>
                        profile.UserId ==
                            user.UserId &&
                        profile.IsActive);

            ViewBag.FullName =
                user.FullName;

            ViewBag.FaceEnrolled =
                user.FaceEnrolled &&
                profileIsActive;
        }

        // DAPATKAN USER ID DARIPADA COOKIE
        private bool TryGetCurrentUserId(
            out int userId)
        {
            string? userIdValue =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier);

            return int.TryParse(
                userIdValue,
                out userId);
        }
    }
}