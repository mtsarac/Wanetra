using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Wanetra.Domain;

namespace Wanetra.Infrastructure.Persistence;

public class WanetraDbContext(DbContextOptions<WanetraDbContext> options) : DbContext(options)
{
    public DbSet<SpeedTestResult> SpeedTestResults => Set<SpeedTestResult>();
    public DbSet<ScheduleSettings> ScheduleSettings => Set<ScheduleSettings>();
    public DbSet<AlertRule> AlertRules => Set<AlertRule>();
    public DbSet<DegradationEvent> DegradationEvents => Set<DegradationEvent>();
    public DbSet<NotificationConfiguration> NotificationConfigurations => Set<NotificationConfiguration>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // SQLite has no date type and drops DateTime.Kind; store UTC text and read it back as UTC.
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<UtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SpeedTestResult>(entity =>
        {
            entity.Property(x => x.Engine).HasMaxLength(64);
            entity.Property(x => x.ServerName).HasMaxLength(256);
            entity.Property(x => x.ServerLocation).HasMaxLength(256);
            entity.Property(x => x.ServerId).HasMaxLength(64);
            entity.Property(x => x.Isp).HasMaxLength(256);
            entity.Property(x => x.ExternalIp).HasMaxLength(45);
            entity.Property(x => x.ErrorMessage).HasMaxLength(2048);
            entity.HasIndex(x => x.Timestamp);
        });

        modelBuilder.Entity<ScheduleSettings>(entity =>
        {
            entity.Property(x => x.CronExpression).HasMaxLength(128);
            entity.Property(x => x.Timezone).HasMaxLength(64);
        });

        modelBuilder.Entity<AlertRule>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(128);
        });

        modelBuilder.Entity<DegradationEvent>(entity =>
        {
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            entity.Property(x => x.Reason).HasMaxLength(1024);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.StartedAt);
        });

        modelBuilder.Entity<NotificationConfiguration>(entity =>
        {
            entity.Property(x => x.Provider).HasMaxLength(32);
            entity.HasIndex(x => x.Provider);
        });
    }

    private sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
        value => value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : value,
        value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
