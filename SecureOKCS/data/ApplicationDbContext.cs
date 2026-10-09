using Microsoft.EntityFrameworkCore;
using SecureOKCS.Models;

namespace SecureOKCS.Data
{
    public class ApplicationDbContext
        : DbContext
    {
        public ApplicationDbContext(
            DbContextOptions options)
            : base(options)
        {
        }

        public DbSet<Role> Roles
        {
            get;
            set;
        }

        public DbSet<User> Users
        {
            get;
            set;
        }

        public DbSet<Customer> Customers
        {
            get;
            set;
        }

        public DbSet<RepairTicket> RepairTickets
        {
            get;
            set;
        }

        public DbSet<RepairStatusHistory>
            RepairStatusHistories
        {
            get;
            set;
        }

        public DbSet<InventoryItem>
            InventoryItems
        {
            get;
            set;
        }

        public DbSet<InventoryTransaction>
            InventoryTransactions
        {
            get;
            set;
        }

        public DbSet<EmailNotification>
            EmailNotifications
        {
            get;
            set;
        }

        public DbSet<Invoice> Invoices
        {
            get;
            set;
        }

        public DbSet<AuditLog> AuditLogs
        {
            get;
            set;
        }

        public DbSet<FaceProfile> FaceProfiles
        {
            get;
            set;
        }

        public DbSet<FaceLoginAttempt>
            FaceLoginAttempts
        {
            get;
            set;
        }

        protected override void OnModelCreating(
            ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // USER DAN ROLE
            modelBuilder.Entity<User>()
                .HasOne(user =>
                    user.Role)
                .WithMany(role =>
                    role.Users)
                .HasForeignKey(user =>
                    user.RoleId);

            // CUSTOMER DAN USER
            modelBuilder.Entity<Customer>()
                .HasOne(customer =>
                    customer.User)
                .WithMany()
                .HasForeignKey(customer =>
                    customer.UserId)
                .OnDelete(
                    DeleteBehavior.SetNull);

            modelBuilder.Entity<Customer>()
                .HasIndex(customer =>
                    customer.TrackingCode)
                .IsUnique();

            // REPAIR DAN CUSTOMER
            modelBuilder.Entity<RepairTicket>()
                .HasOne(repair =>
                    repair.Customer)
                .WithMany()
                .HasForeignKey(repair =>
                    repair.CustomerId);

            // REPAIR DAN TECHNICIAN
            modelBuilder.Entity<RepairTicket>()
                .HasOne(repair =>
                    repair.Technician)
                .WithMany()
                .HasForeignKey(repair =>
                    repair.TechnicianId)
                .OnDelete(
                    DeleteBehavior.SetNull);

            modelBuilder.Entity<RepairTicket>()
                .HasIndex(repair =>
                    repair.TicketNumber)
                .IsUnique();

            modelBuilder.Entity<RepairTicket>()
                .Property(repair =>
                    repair.EstimatedCost)
                .HasPrecision(10, 2);

            modelBuilder.Entity<RepairTicket>()
                .Property(repair =>
                    repair.FinalCost)
                .HasPrecision(10, 2);

            // REPAIR STATUS HISTORY
            modelBuilder
                .Entity<RepairStatusHistory>()
                .HasOne(history =>
                    history.RepairTicket)
                .WithMany()
                .HasForeignKey(history =>
                    history.RepairId)
                .OnDelete(
                    DeleteBehavior.Cascade);

            modelBuilder
                .Entity<RepairStatusHistory>()
                .HasOne(history =>
                    history.ChangedByUser)
                .WithMany()
                .HasForeignKey(history =>
                    history.ChangedBy)
                .OnDelete(
                    DeleteBehavior.Restrict);

            // INVENTORY ITEM
            modelBuilder.Entity<InventoryItem>()
                .HasIndex(item =>
                    item.ItemCode)
                .IsUnique();

            modelBuilder.Entity<InventoryItem>()
                .HasIndex(item =>
                    item.Barcode)
                .IsUnique();

            modelBuilder.Entity<InventoryItem>()
                .Property(item =>
                    item.UnitPrice)
                .HasPrecision(10, 2);

            // INVENTORY TRANSACTION DAN ITEM
            modelBuilder
                .Entity<InventoryTransaction>()
                .HasOne(transaction =>
                    transaction.InventoryItem)
                .WithMany(item =>
                    item.InventoryTransactions)
                .HasForeignKey(transaction =>
                    transaction.InventoryId)
                .OnDelete(
                    DeleteBehavior.Restrict);

            // INVENTORY TRANSACTION DAN USER
            modelBuilder
                .Entity<InventoryTransaction>()
                .HasOne(transaction =>
                    transaction.User)
                .WithMany()
                .HasForeignKey(transaction =>
                    transaction.UserId)
                .OnDelete(
                    DeleteBehavior.Restrict);

            // EMAIL NOTIFICATION DAN REPAIR
            modelBuilder
                .Entity<EmailNotification>()
                .HasOne(notification =>
                    notification.RepairTicket)
                .WithMany()
                .HasForeignKey(notification =>
                    notification.RepairId)
                .OnDelete(
                    DeleteBehavior.Restrict);

            // EMAIL NOTIFICATION DAN CUSTOMER
            modelBuilder
                .Entity<EmailNotification>()
                .HasOne(notification =>
                    notification.Customer)
                .WithMany()
                .HasForeignKey(notification =>
                    notification.CustomerId)
                .OnDelete(
                    DeleteBehavior.Restrict);

            modelBuilder
                .Entity<EmailNotification>()
                .HasIndex(notification =>
                    notification.CreatedAt);

            // INVOICE DAN REPAIR
            modelBuilder.Entity<Invoice>()
                .HasOne(invoice =>
                    invoice.RepairTicket)
                .WithMany()
                .HasForeignKey(invoice =>
                    invoice.RepairId)
                .OnDelete(
                    DeleteBehavior.Restrict);

            // INVOICE DAN USER
            // YANG MENGELUARKAN
            modelBuilder.Entity<Invoice>()
                .HasOne(invoice =>
                    invoice.IssuedByUser)
                .WithMany()
                .HasForeignKey(invoice =>
                    invoice.IssuedBy)
                .OnDelete(
                    DeleteBehavior.Restrict);

            // INVOICE NUMBER MESTI UNIK
            modelBuilder.Entity<Invoice>()
                .HasIndex(invoice =>
                    invoice.InvoiceNumber)
                .IsUnique();

            // NILAI KEWANGAN
            modelBuilder.Entity<Invoice>()
                .Property(invoice =>
                    invoice.Subtotal)
                .HasPrecision(10, 2);

            modelBuilder.Entity<Invoice>()
                .Property(invoice =>
                    invoice.TaxAmount)
                .HasPrecision(10, 2);

            modelBuilder.Entity<Invoice>()
                .Property(invoice =>
                    invoice.TotalAmount)
                .HasPrecision(10, 2);

            // AUDIT LOG DAN USER
            modelBuilder.Entity<AuditLog>()
                .HasOne(log =>
                    log.User)
                .WithMany()
                .HasForeignKey(log =>
                    log.UserId)
                .OnDelete(
                    DeleteBehavior.SetNull);

            // INDEX AUDIT LOG
            modelBuilder.Entity<AuditLog>()
                .HasIndex(log =>
                    log.CreatedAt);

            modelBuilder.Entity<AuditLog>()
                .HasIndex(log =>
                    log.ActionType);

            // FACE PROFILE DAN USER
            modelBuilder.Entity<FaceProfile>()
                .HasOne(profile =>
                    profile.User)
                .WithOne()
                .HasForeignKey<FaceProfile>(
                    profile =>
                        profile.UserId)
                .OnDelete(
                    DeleteBehavior.Cascade);

            // SEORANG USER HANYA
            // SATU FACE PROFILE
            modelBuilder.Entity<FaceProfile>()
                .HasIndex(profile =>
                    profile.UserId)
                .IsUnique();

            modelBuilder.Entity<FaceProfile>()
                .HasIndex(profile =>
                    profile.IsActive);

            // FACE LOGIN ATTEMPT DAN USER
            modelBuilder
                .Entity<FaceLoginAttempt>()
                .HasOne(attempt =>
                    attempt.User)
                .WithMany()
                .HasForeignKey(attempt =>
                    attempt.UserId)
                .OnDelete(
                    DeleteBehavior.Cascade);

            // SATU REKOD UNTUK SETIAP
            // KOMBINASI USER DAN IP
            modelBuilder
                .Entity<FaceLoginAttempt>()
                .HasIndex(attempt =>
                    new
                    {
                        attempt.UserId,
                        attempt.IpAddress
                    })
                .IsUnique();

            // INDEX UNTUK SEMAK LOCKOUT
            modelBuilder
                .Entity<FaceLoginAttempt>()
                .HasIndex(attempt =>
                    attempt.BlockedUntil);
        }
    }
}