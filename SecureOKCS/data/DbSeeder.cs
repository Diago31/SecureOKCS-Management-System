using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SecureOKCS.Models;

namespace SecureOKCS.Data
{
    public static class DbSeeder
    {
        public static async Task SeedAsync(
            IServiceProvider services)
        {
            using var scope = services.CreateScope();

            var context = scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();

            var hasher = scope.ServiceProvider
                .GetRequiredService<IPasswordHasher<User>>();

            await CreateUser(
                context,
                hasher,
                "System Administrator",
                "admin@okcs.com",
                "Admin@12345",
                "Admin");

            await CreateUser(
                context,
                hasher,
                "Workshop Staff",
                "staff@okcs.com",
                "Staff@12345",
                "Staff");
        }

        private static async Task CreateUser(
            ApplicationDbContext context,
            IPasswordHasher<User> hasher,
            string fullName,
            string email,
            string password,
            string roleName)
        {
            bool exists = await context.Users
                .AnyAsync(user => user.Email == email);

            if (exists)
                return;

            var role = await context.Roles
                .FirstOrDefaultAsync(role =>
                    role.RoleName == roleName);

            if (role == null)
                return;

            var user = new User
            {
                RoleId = role.RoleId,
                FullName = fullName,
                Email = email,
                IsActive = true,
                FaceEnrolled = false,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            user.PasswordHash =
                hasher.HashPassword(user, password);

            context.Users.Add(user);
            await context.SaveChangesAsync();
        }
    }
}