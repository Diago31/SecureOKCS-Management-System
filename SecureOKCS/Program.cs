using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SecureOKCS.Data;
using SecureOKCS.Models;
using SecureOKCS.Services;

var builder =
    WebApplication.CreateBuilder(args);

var connectionString =
    builder.Configuration
        .GetConnectionString(
            "DefaultConnection")
    ?? throw new InvalidOperationException(
        "Database connection string was not found.");

// DATABASE
builder.Services.AddDbContext<
    ApplicationDbContext>(
    options =>
        options.UseMySql(
            connectionString,
            ServerVersion.AutoDetect(
                connectionString)));

// PASSWORD HASHER
builder.Services.AddScoped<
    IPasswordHasher<User>,
    PasswordHasher<User>>();

// DATA PROTECTION
builder.Services.AddDataProtection();

// HTTP CONTEXT ACCESSOR
builder.Services.AddHttpContextAccessor();

// CUSTOMER DATA ENCRYPTION
builder.Services.AddScoped<
    CustomerEncryptionService>();

// FACE TEMPLATE ENCRYPTION
builder.Services.AddScoped<
    FaceTemplateService>();

// FACE LOGIN INTRUSION PROTECTION
builder.Services.AddScoped<
    FaceLoginSecurityService>();

// AUDIT LOG SERVICE
builder.Services.AddScoped<
    AuditLogService>();

// EMAIL SETTINGS AND SERVICE
builder.Services.Configure<
    EmailSettings>(
    builder.Configuration.GetSection(
        "EmailSettings"));

builder.Services.AddScoped<
    EmailNotificationService>();

// COOKIE AUTHENTICATION
builder.Services
    .AddAuthentication(
        CookieAuthenticationDefaults
            .AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath =
            "/Account/Login";

        options.AccessDeniedPath =
            "/Account/AccessDenied";

        // Authentication expires after
        // 10 minutes without a request
        options.ExpireTimeSpan =
            TimeSpan.FromMinutes(10);

        // Renew the authentication cookie
        // while the user remains active
        options.SlidingExpiration =
            true;

        // Authentication cookie security
        options.Cookie.Name =
            "SecureOKCS.Authentication";

        options.Cookie.HttpOnly =
            true;

        options.Cookie.IsEssential =
            true;

        options.Cookie.SecurePolicy =
            CookieSecurePolicy.Always;

        options.Cookie.SameSite =
            SameSiteMode.Lax;
    });

// SESSION
builder.Services
    .AddDistributedMemoryCache();

builder.Services.AddSession(
    options =>
    {
        // Session expires after
        // 10 minutes without activity
        options.IdleTimeout =
            TimeSpan.FromMinutes(10);

        // Session cookie security
        options.Cookie.Name =
            "SecureOKCS.Session";

        options.Cookie.HttpOnly =
            true;

        options.Cookie.IsEssential =
            true;

        options.Cookie.SecurePolicy =
            CookieSecurePolicy.Always;

        options.Cookie.SameSite =
            SameSiteMode.Lax;
    });

// MVC
builder.Services
    .AddControllersWithViews();

var app = builder.Build();

// CREATE DEFAULT ADMIN AND STAFF
await DbSeeder.SeedAsync(
    app.Services);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(
        "/Home/Error");

    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

// SESSION MUST RUN BEFORE
// AUTHENTICATION AND AUTHORIZATION
app.UseSession();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern:
        "{controller=Home}/{action=Index}/{id?}");

app.Run();