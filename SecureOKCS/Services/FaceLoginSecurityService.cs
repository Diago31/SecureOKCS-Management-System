using Microsoft.EntityFrameworkCore;
using SecureOKCS.Data;
using SecureOKCS.Models;

namespace SecureOKCS.Services
{
    public class FaceLoginSecurityService
    {
        public const int MaximumAttempts =
            3;

        public const int LockoutMinutes =
            5;

        private readonly ApplicationDbContext
            _context;

        private readonly IHttpContextAccessor
            _httpContextAccessor;

        public FaceLoginSecurityService(
            ApplicationDbContext context,
            IHttpContextAccessor
                httpContextAccessor)
        {
            _context = context;

            _httpContextAccessor =
                httpContextAccessor;
        }

        // SEMAK STATUS SEKATAN
        public async Task<FaceLoginSecurityResult>
            GetStatusAsync(int userId)
        {
            string ipAddress =
                GetIpAddress();

            DateTime currentTime =
                DateTime.UtcNow;

            var attempt =
                await _context.FaceLoginAttempts
                    .FirstOrDefaultAsync(attempt =>
                        attempt.UserId == userId &&
                        attempt.IpAddress ==
                            ipAddress);

            if (attempt == null)
            {
                return CreateResult(
                    null,
                    ipAddress,
                    currentTime);
            }

            // SEKATAN SUDAH TAMAT
            if (attempt.BlockedUntil.HasValue &&
                attempt.BlockedUntil.Value <=
                    currentTime)
            {
                attempt.FailedAttempts = 0;

                attempt.BlockedUntil = null;

                attempt.UpdatedAt =
                    currentTime;

                await _context.SaveChangesAsync();
            }

            return CreateResult(
                attempt,
                ipAddress,
                currentTime);
        }

        // REKOD WAJAH SALAH
        public async Task<FaceLoginSecurityResult>
            RegisterFailureAsync(int userId)
        {
            string ipAddress =
                GetIpAddress();

            DateTime currentTime =
                DateTime.UtcNow;

            var attempt =
                await _context.FaceLoginAttempts
                    .FirstOrDefaultAsync(attempt =>
                        attempt.UserId == userId &&
                        attempt.IpAddress ==
                            ipAddress);

            if (attempt == null)
            {
                attempt =
                    new FaceLoginAttempt
                    {
                        UserId =
                            userId,

                        IpAddress =
                            ipAddress,

                        FailedAttempts =
                            0,

                        BlockedUntil =
                            null,

                        LastFailedAt =
                            null,

                        UpdatedAt =
                            currentTime
                    };

                _context.FaceLoginAttempts.Add(
                    attempt);
            }

            // JIKA SEKATAN LAMA SUDAH TAMAT
            if (attempt.BlockedUntil.HasValue &&
                attempt.BlockedUntil.Value <=
                    currentTime)
            {
                attempt.FailedAttempts = 0;

                attempt.BlockedUntil = null;
            }

            // JIKA MASIH DISEKAT,
            // JANGAN TAMBAH PERCUBAAN
            if (attempt.BlockedUntil.HasValue &&
                attempt.BlockedUntil.Value >
                    currentTime)
            {
                return CreateResult(
                    attempt,
                    ipAddress,
                    currentTime);
            }

            attempt.FailedAttempts++;

            attempt.LastFailedAt =
                currentTime;

            attempt.UpdatedAt =
                currentTime;

            if (attempt.FailedAttempts >=
                MaximumAttempts)
            {
                attempt.FailedAttempts =
                    MaximumAttempts;

                attempt.BlockedUntil =
                    currentTime.AddMinutes(
                        LockoutMinutes);
            }

            await _context.SaveChangesAsync();

            return CreateResult(
                attempt,
                ipAddress,
                currentTime);
        }

        // RESET SELEPAS WAJAH BERJAYA
        public async Task ResetAsync(
            int userId)
        {
            string ipAddress =
                GetIpAddress();

            var attempt =
                await _context.FaceLoginAttempts
                    .FirstOrDefaultAsync(attempt =>
                        attempt.UserId == userId &&
                        attempt.IpAddress ==
                            ipAddress);

            if (attempt == null)
                return;

            attempt.FailedAttempts = 0;

            attempt.BlockedUntil = null;

            attempt.UpdatedAt =
                DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }

        // DAPATKAN IP ADDRESS
        public string GetIpAddress()
        {
            string? ipAddress =
                _httpContextAccessor
                    .HttpContext?
                    .Connection
                    .RemoteIpAddress?
                    .ToString();

            if (string.IsNullOrWhiteSpace(
                    ipAddress))
            {
                return "unknown";
            }

            return ipAddress.Length <= 45
                ? ipAddress
                : ipAddress[..45];
        }

        // HASILKAN RESULT
        private static FaceLoginSecurityResult
            CreateResult(
                FaceLoginAttempt? attempt,
                string ipAddress,
                DateTime currentTime)
        {
            bool isBlocked =
                attempt?.BlockedUntil.HasValue ==
                    true &&
                attempt.BlockedUntil.Value >
                    currentTime;

            int failedAttempts =
                attempt?.FailedAttempts ?? 0;

            int remainingAttempts =
                Math.Max(
                    0,
                    MaximumAttempts -
                    failedAttempts);

            int remainingSeconds = 0;

            if (isBlocked &&
                attempt?.BlockedUntil != null)
            {
                remainingSeconds =
                    Math.Max(
                        0,
                        (int)Math.Ceiling(
                            (
                                attempt.BlockedUntil.Value -
                                currentTime
                            ).TotalSeconds));
            }

            return new FaceLoginSecurityResult
            {
                IsBlocked =
                    isBlocked,

                FailedAttempts =
                    failedAttempts,

                RemainingAttempts =
                    remainingAttempts,

                BlockedUntil =
                    attempt?.BlockedUntil,

                RemainingSeconds =
                    remainingSeconds,

                IpAddress =
                    ipAddress
            };
        }
    }

    public class FaceLoginSecurityResult
    {
        public bool IsBlocked
        {
            get;
            set;
        }

        public int FailedAttempts
        {
            get;
            set;
        }

        public int RemainingAttempts
        {
            get;
            set;
        }

        public DateTime? BlockedUntil
        {
            get;
            set;
        }

        public int RemainingSeconds
        {
            get;
            set;
        }

        public string IpAddress
        {
            get;
            set;
        } = string.Empty;
    }
}