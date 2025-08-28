using AttechServer.Domains.Entities;
using AttechServer.Domains.Entities.Main;
using AttechServer.Domains.EntityBase;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace AttechServer.Infrastructures.Persistances
{
    public class ApplicationDbContext : DbContext
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly int? UserId;
        private readonly ILogger<ApplicationDbContext> _logger;

        #region User
        public DbSet<User> Users { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<ApiEndpoint> ApiEndpoints { get; set; }
        #endregion

        #region Main
        public DbSet<News> News { get; set; }
        public DbSet<NewsCategory> NewsCategories { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<NotificationCategory> NotificationCategories { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<ProductCategory> ProductCategories { get; set; }
        public DbSet<Service> Services { get; set; }
        public DbSet<Contact> Contacts { get; set; }
        public DbSet<Attachment> Attachments { get; set; }
        public DbSet<ActivityLog> ActivityLogs { get; set; }
        public DbSet<SystemMonitoring> SystemMonitorings { get; set; }
        public DbSet<LanguageContent> LanguageContents { get; set; }
        public DbSet<LanguageContentCategory> LanguageContentCategories { get; set; }
        #endregion


        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options,
            IHttpContextAccessor httpContextAccessor,
            ILogger<ApplicationDbContext> logger)
            : base(options)
        {
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;

            // L?y UserId t? HttpContext n?u c�
            if (_httpContextAccessor.HttpContext != null)
            {
                var claims = _httpContextAccessor.HttpContext.User?.Identity as ClaimsIdentity;
                var claim = claims?.FindFirst("user_id");
                if (claim != null && int.TryParse(claim.Value, out int userId))
                {
                    UserId = userId;
                }
            }

            if (!UserId.HasValue)
            {
                _logger.LogWarning("UserId is null during DbContext initialization. No user context available.");
            }
        }

        private void CheckAudit()
        {
            ChangeTracker.DetectChanges();

            // X? l� c�c entity m?i th�m
            var added = ChangeTracker.Entries()
                .Where(t => t.State == EntityState.Added)
                .Select(t => t.Entity);

            foreach (var entity in added)
            {
                if (entity is ICreatedBy createdEntity)
                {
                    if (!createdEntity.CreatedDate.HasValue)
                    {
                        createdEntity.CreatedDate = DateTime.Now; // S? d?ng th?i gian local c?a server
                    }
                    if (!createdEntity.CreatedBy.HasValue)
                    {
                        createdEntity.CreatedBy = UserId;
                    }
                    _logger.LogInformation($"Set CreatedDate for {entity.GetType().Name}: {createdEntity.CreatedDate}");
                }
            }

            var modified = ChangeTracker.Entries()
                .Where(t => t.State == EntityState.Modified)
                .Select(t => t.Entity);

            foreach (var entity in modified)
            {
                if (entity is IModifiedBy modifiedEntity)
                {
                    modifiedEntity.ModifiedDate = DateTime.Now;
                    if (!modifiedEntity.ModifiedBy.HasValue)
                    {
                        modifiedEntity.ModifiedBy = UserId;
                    }
                    _logger.LogInformation($"Set ModifiedDate for {entity.GetType().Name}: {modifiedEntity.ModifiedDate}");
                }
            }
        }

        public override int SaveChanges()
        {
            CheckAudit();
            return base.SaveChanges();
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            CheckAudit();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            CheckAudit();
            return base.SaveChangesAsync(cancellationToken);
        }

        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            CheckAudit();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            foreach (var entity in modelBuilder.Model.GetEntityTypes())
            {
                if (entity.ClrType.IsAssignableTo(typeof(ICreatedBy)))
                {
                    
                }
            }

            #region User
            modelBuilder.Entity<User>(e =>
            {
                e.ToTable("Users");
                e.HasKey(i => i.Id);
                e.HasOne(u => u.Role)
                    .WithMany(r => r.Users)
                    .HasForeignKey(u => u.RoleId)
                    .OnDelete(DeleteBehavior.Restrict);
                e.Property(u => u.Deleted).HasDefaultValue(false);
            });


            modelBuilder.Entity<Role>(e =>
            {
                e.ToTable("Roles");
                e.HasKey(r => r.Id);
                e.Property(r => r.Deleted).HasDefaultValue(false);
            });


            modelBuilder.Entity<ApiEndpoint>(e =>
            {
                e.ToTable("ApiEndpoints");
                e.HasKey(a => a.Id);
                e.Property(a => a.Deleted).HasDefaultValue(false);
                e.Property(a => a.RequireAuthentication).HasDefaultValue(true);

            });

            #endregion

            #region Main Configuration

            modelBuilder.Entity<News>(entity =>
            {
                entity.ToTable("News");
                entity.HasIndex(e => e.SlugVi).IsUnique();
                entity.HasIndex(e => e.SlugEn).IsUnique();
                entity.HasIndex(e => e.NewsCategoryId);
                entity.HasIndex(e => new { e.Id, e.Deleted }).HasDatabaseName("IX_News");
                entity.Property(e => e.TitleVi).HasMaxLength(200).IsRequired();
                entity.Property(e => e.TitleEn).HasMaxLength(200).IsRequired();
                entity.Property(e => e.SlugVi).HasMaxLength(200).IsRequired();
                entity.Property(e => e.SlugEn).HasMaxLength(200).IsRequired();
                entity.Property(e => e.DescriptionVi).HasMaxLength(700).IsRequired();
                entity.Property(e => e.DescriptionEn).HasMaxLength(700).IsRequired();
                entity.Property(e => e.ContentVi).IsRequired();
                entity.Property(e => e.ContentEn).IsRequired();
                entity.Property(e => e.TimePosted).IsRequired();
                entity.Property(e => e.Deleted).HasDefaultValue(false);
                entity.HasOne(n => n.NewsCategory)
                    .WithMany(c => c.News)
                    .HasForeignKey(n => n.NewsCategoryId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<NewsCategory>(entity =>
            {
                entity.ToTable("NewsCategories");
                entity.HasIndex(e => e.SlugVi).IsUnique();
                entity.HasIndex(e => e.SlugEn).IsUnique();
                entity.Property(e => e.TitleVi).HasMaxLength(100).IsRequired();
                entity.Property(e => e.TitleEn).HasMaxLength(100).IsRequired();
                entity.Property(e => e.SlugVi).HasMaxLength(100).IsRequired();
                entity.Property(e => e.SlugEn).HasMaxLength(100).IsRequired();
                entity.Property(e => e.DescriptionVi).HasMaxLength(700);
                entity.Property(e => e.DescriptionEn).HasMaxLength(700);
                entity.Property(e => e.Deleted).HasDefaultValue(false);
            });

            modelBuilder.Entity<Notification>(entity =>
            {
                entity.ToTable("Notifications");
                entity.HasIndex(e => e.SlugVi).IsUnique();
                entity.HasIndex(e => e.SlugEn).IsUnique();
                entity.HasIndex(e => e.NotificationCategoryId);
                entity.HasIndex(e => new { e.Id, e.Deleted }).HasDatabaseName("IX_Notification");
                entity.Property(e => e.TitleVi).HasMaxLength(200).IsRequired();
                entity.Property(e => e.TitleEn).HasMaxLength(200).IsRequired();
                entity.Property(e => e.SlugVi).HasMaxLength(200).IsRequired();
                entity.Property(e => e.SlugEn).HasMaxLength(200).IsRequired();
                entity.Property(e => e.DescriptionVi).HasMaxLength(700).IsRequired();
                entity.Property(e => e.DescriptionEn).HasMaxLength(700).IsRequired();
                entity.Property(e => e.ContentVi).IsRequired();
                entity.Property(e => e.ContentEn).IsRequired();
                entity.Property(e => e.TimePosted).IsRequired();
                entity.Property(e => e.Deleted).HasDefaultValue(false);
                entity.HasOne(n => n.NotificationCategory)
                    .WithMany(c => c.Notifications)
                    .HasForeignKey(n => n.NotificationCategoryId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<NotificationCategory>(entity =>
            {
                entity.ToTable("NotificationCategories");
                entity.HasIndex(e => e.SlugVi).IsUnique();
                entity.HasIndex(e => e.SlugEn).IsUnique();
                entity.Property(e => e.TitleVi).HasMaxLength(100).IsRequired();
                entity.Property(e => e.TitleEn).HasMaxLength(100).IsRequired();
                entity.Property(e => e.SlugVi).HasMaxLength(100).IsRequired();
                entity.Property(e => e.SlugEn).HasMaxLength(100).IsRequired();
                entity.Property(e => e.DescriptionVi).HasMaxLength(700);
                entity.Property(e => e.DescriptionEn).HasMaxLength(700);
                entity.Property(e => e.Deleted).HasDefaultValue(false);
            });

            modelBuilder.Entity<Product>(entity =>
            {
                entity.ToTable("Products");
                entity.HasIndex(e => e.SlugVi).IsUnique();
                entity.HasIndex(e => e.SlugEn).IsUnique();
                entity.HasIndex(e => e.ProductCategoryId);
                entity.HasIndex(e => new { e.Id, e.Deleted }).HasDatabaseName("IX_Product");
                entity.Property(e => e.TitleVi).HasMaxLength(200).IsRequired();
                entity.Property(e => e.TitleEn).HasMaxLength(200).IsRequired();
                entity.Property(e => e.SlugVi).HasMaxLength(200).IsRequired();
                entity.Property(e => e.SlugEn).HasMaxLength(200).IsRequired();
                entity.Property(e => e.DescriptionVi).HasMaxLength(700).IsRequired();
                entity.Property(e => e.DescriptionEn).HasMaxLength(700).IsRequired();
                entity.Property(e => e.ContentVi).IsRequired();
                entity.Property(e => e.ContentEn).IsRequired();
                entity.Property(e => e.TimePosted).IsRequired();
                entity.Property(e => e.Deleted).HasDefaultValue(false);
                entity.HasOne(p => p.ProductCategory)
                    .WithMany(c => c.Products)
                    .HasForeignKey(p => p.ProductCategoryId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<ProductCategory>(entity =>
            {
                entity.ToTable("ProductCategories");
                entity.HasIndex(e => e.SlugVi).IsUnique();
                entity.HasIndex(e => e.SlugEn).IsUnique();
                entity.Property(e => e.TitleVi).HasMaxLength(100).IsRequired();
                entity.Property(e => e.TitleEn).HasMaxLength(100).IsRequired();
                entity.Property(e => e.SlugVi).HasMaxLength(100).IsRequired();
                entity.Property(e => e.SlugEn).HasMaxLength(100).IsRequired();
                entity.Property(e => e.DescriptionVi).HasMaxLength(700);
                entity.Property(e => e.DescriptionEn).HasMaxLength(700);
                entity.Property(e => e.Deleted).HasDefaultValue(false);
            });

            modelBuilder.Entity<Service>(entity =>
            {
                entity.ToTable("Services");
                entity.HasIndex(e => e.SlugVi).IsUnique();
                entity.HasIndex(e => e.SlugEn).IsUnique();
                entity.Property(e => e.TitleVi).HasMaxLength(200).IsRequired();
                entity.Property(e => e.TitleEn).HasMaxLength(200).IsRequired();
                entity.Property(e => e.SlugVi).HasMaxLength(200).IsRequired();
                entity.Property(e => e.SlugEn).HasMaxLength(200).IsRequired();
                entity.Property(e => e.DescriptionVi).HasMaxLength(700).IsRequired();
                entity.Property(e => e.DescriptionEn).HasMaxLength(700).IsRequired();
                entity.Property(e => e.ContentVi).IsRequired();
                entity.Property(e => e.ContentEn).IsRequired();
                entity.Property(e => e.TimePosted).IsRequired();
                entity.Property(e => e.Deleted).HasDefaultValue(false);
            });

            modelBuilder.Entity<Attachment>(entity =>
            {
                entity.ToTable("Attachments");
                entity.HasKey(e => e.Id);
            });


            modelBuilder.Entity<ActivityLog>(entity =>
            {
                entity.ToTable("ActivityLogs");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Type);
                entity.HasIndex(e => e.Timestamp);
                entity.HasIndex(e => e.CreatedBy);
                entity.Property(e => e.Type).HasMaxLength(50).IsRequired();
                entity.Property(e => e.Message).HasMaxLength(500).IsRequired();
                entity.Property(e => e.Severity).HasMaxLength(20).IsRequired();
                entity.Property(e => e.IpAddress).HasMaxLength(45);
                entity.Property(e => e.UserAgent).HasMaxLength(500);
                entity.Property(e => e.Deleted).HasDefaultValue(false);
            });

            modelBuilder.Entity<SystemMonitoring>(entity =>
            {
                entity.ToTable("SystemMonitorings");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.MetricName);
                entity.HasIndex(e => e.Category);
                entity.HasIndex(e => e.RecordedAt);
                entity.Property(e => e.MetricName).HasMaxLength(100).IsRequired();
                entity.Property(e => e.Unit).HasMaxLength(20).IsRequired();
                entity.Property(e => e.Category).HasMaxLength(50).IsRequired();
                entity.Property(e => e.Description).HasMaxLength(200);
                entity.Property(e => e.Deleted).HasDefaultValue(false);
            });

            modelBuilder.Entity<LanguageContent>(entity =>
            {
                entity.ToTable("LanguageContents");
                entity.HasIndex(e => e.ContentKey).IsUnique();
                entity.HasIndex(e => e.Category);
                entity.HasIndex(e => new { e.Id, e.Deleted }).HasDatabaseName("IX_LanguageContent");
                entity.Property(e => e.ContentKey).HasMaxLength(255).IsRequired();
                entity.Property(e => e.ValueVi);
                entity.Property(e => e.ValueEn);
                entity.Property(e => e.Category).HasMaxLength(100).HasDefaultValue("common");
                entity.Property(e => e.Description).HasMaxLength(500);
                entity.Property(e => e.Deleted).HasDefaultValue(false);
            });

            modelBuilder.Entity<LanguageContentCategory>(entity =>
            {
                entity.ToTable("LanguageContentCategories");
                entity.HasIndex(e => e.Name).IsUnique();
                entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
                entity.Property(e => e.DisplayName).HasMaxLength(200).IsRequired();
            });
            #endregion

            base.OnModelCreating(modelBuilder);
        }
    }
}
