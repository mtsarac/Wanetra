using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Wanetra.Domain;

namespace Wanetra.Infrastructure.Persistence;

public class WanetraDbContext(DbContextOptions<WanetraDbContext> options) : DbContext(options)
{
    public DbSet<SpeedTestResult> SpeedTestResults => Set<SpeedTestResult>();
    public DbSet<ScheduleSettings> ScheduleSettings => Set<ScheduleSettings>();
    public DbSet<AlertRule> AlertRules => Set<AlertRule>();
    public DbSet<AlertState> AlertStates => Set<AlertState>();
    public DbSet<DegradationEvent> DegradationEvents => Set<DegradationEvent>();
    public DbSet<NotificationConfiguration> NotificationConfigurations => Set<NotificationConfiguration>();
    public DbSet<NotificationDelivery> NotificationDeliveries => Set<NotificationDelivery>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();
    public DbSet<ProbeTarget> ProbeTargets => Set<ProbeTarget>();
    public DbSet<ProbeSample> ProbeSamples => Set<ProbeSample>();
    public DbSet<ProbeRollup> ProbeRollups => Set<ProbeRollup>();
    public DbSet<Outage> Outages => Set<Outage>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<DeviceEvent> DeviceEvents => Set<DeviceEvent>();
    public DbSet<ScanRun> ScanRuns => Set<ScanRun>();
    public DbSet<DeviceAlert> DeviceAlerts => Set<DeviceAlert>();
    public DbSet<AuthAccount> AuthAccounts => Set<AuthAccount>();
    public DbSet<AuthSession> AuthSessions => Set<AuthSession>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
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
            entity.Property(x => x.FailureKind).HasConversion<string>().HasMaxLength(32);
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

        modelBuilder.Entity<AlertState>(entity =>
        {
            entity.HasKey(x => x.Id);
        });

        modelBuilder.Entity<DegradationEvent>(entity =>
        {
            entity.Ignore(x => x.IsRecovered);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            entity.Property(x => x.Reason).HasMaxLength(1024);
            entity.Property(x => x.ClosureReason).HasMaxLength(128);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.StartedAt);
        });

        modelBuilder.Entity<NotificationConfiguration>(entity =>
        {
            entity.HasIndex(x => x.Provider);
        });
        modelBuilder.Entity<NotificationDelivery>(entity =>
        {
            entity.Property(x => x.Trigger).HasConversion<string>().HasMaxLength(16);
            entity.Property(x => x.Provider).HasMaxLength(32);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            entity.Property(x => x.LastErrorSummary).HasMaxLength(128);
            entity.HasIndex(x => new { x.DegradationEventId, x.Trigger, x.ConfigurationId }).IsUnique();
            entity.HasIndex(x => new { x.OutageId, x.Trigger, x.ConfigurationId }).IsUnique();
            entity.HasIndex(x => new { x.DeviceAlertId, x.Trigger, x.ConfigurationId }).IsUnique();
            entity.HasOne(delivery => delivery.DegradationEvent)
                .WithMany(degradationEvent => degradationEvent.NotificationDeliveries)
                .HasForeignKey(delivery => delivery.DegradationEventId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(delivery => delivery.Outage)
                .WithMany(outage => outage.NotificationDeliveries)
                .HasForeignKey(delivery => delivery.OutageId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(delivery => delivery.DeviceAlert)
                .WithMany(alert => alert.NotificationDeliveries)
                .HasForeignKey(delivery => delivery.DeviceAlertId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProbeTarget>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(64).UseCollation("NOCASE");
            entity.Property(x => x.Kind).HasConversion<string>().HasMaxLength(16);
            entity.Property(x => x.Role).HasConversion<string>().HasMaxLength(16);
            entity.Property(x => x.Target).HasMaxLength(2048);
            entity.Property(x => x.Resolver).HasMaxLength(64);
            entity.HasIndex(x => x.Name).IsUnique();
            entity.HasIndex(x => x.DeviceId);
        });

        modelBuilder.Entity<ProbeSample>(entity =>
        {
            entity.Property(x => x.Error).HasMaxLength(64);
            entity.HasIndex(x => new { x.TargetId, x.Timestamp });
            entity.HasIndex(x => x.Timestamp);
            entity.HasOne<ProbeTarget>().WithMany().HasForeignKey(x => x.TargetId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProbeRollup>(entity =>
        {
            entity.Ignore(x => x.LossPercent);
            entity.Property(x => x.Resolution).HasConversion<string>().HasMaxLength(16);
            entity.HasIndex(x => new { x.TargetId, x.Resolution, x.BucketStart }).IsUnique();
            entity.HasIndex(x => new { x.Resolution, x.BucketStart });
            entity.HasOne<ProbeTarget>().WithMany().HasForeignKey(x => x.TargetId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Device>(entity =>
        {
            entity.Property(x => x.Mac).HasMaxLength(17);
            entity.Property(x => x.CurrentIp).HasMaxLength(45);
            entity.Property(x => x.Hostname).HasMaxLength(64);
            entity.Property(x => x.Vendor).HasMaxLength(256);
            entity.Property(x => x.Description).HasMaxLength(64);
            entity.Property(x => x.Name).HasMaxLength(64);
            entity.HasIndex(x => x.Mac).IsUnique();
            entity.HasIndex(x => x.CurrentIp);
        });

        modelBuilder.Entity<DeviceEvent>(entity =>
        {
            entity.Property(x => x.Type).HasConversion<string>().HasMaxLength(16);
            entity.Property(x => x.Detail).HasMaxLength(128);
            entity.HasIndex(x => new { x.DeviceId, x.Timestamp });
            entity.HasIndex(x => x.Timestamp);
            entity.HasOne<Device>().WithMany().HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ScanRun>(entity =>
        {
            entity.Property(x => x.Networks).HasMaxLength(256);
            entity.Property(x => x.Error).HasMaxLength(512);
            entity.Property(x => x.Trigger).HasMaxLength(16);
            entity.HasIndex(x => x.StartedAt);
        });

        modelBuilder.Entity<DeviceAlert>(entity =>
        {
            entity.Ignore(x => x.IsRecovered);
            entity.Property(x => x.Kind).HasConversion<string>().HasMaxLength(16);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            entity.Property(x => x.DeviceLabel).HasMaxLength(256);
            entity.Property(x => x.Mac).HasMaxLength(17);
            entity.Property(x => x.Ip).HasMaxLength(45);
            entity.Property(x => x.Vendor).HasMaxLength(256);
            entity.HasIndex(x => new { x.DeviceId, x.Kind, x.Status });
            entity.HasOne(x => x.Device).WithMany().HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Outage>(entity =>
        {
            entity.Ignore(x => x.IsRecovered);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            entity.Property(x => x.Scope).HasConversion<string>().HasMaxLength(16);
            entity.Property(x => x.Reason).HasMaxLength(256);
            entity.Property(x => x.ClosureReason).HasMaxLength(64);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.StartedAt);
        });

        modelBuilder.Entity<AppSetting>(entity =>
        {
            entity.HasKey(x => x.Key);
            entity.Property(x => x.Key).HasMaxLength(128);
            entity.Property(x => x.Value).HasMaxLength(1024);
        });

        modelBuilder.Entity<AuthAccount>(entity =>
        {
            entity.Property(x => x.Username).HasMaxLength(64);
            entity.Property(x => x.PasswordHash).HasMaxLength(256);
            entity.Property(x => x.MetricsTokenHash).HasMaxLength(64);
        });

        modelBuilder.Entity<AuthSession>(entity =>
        {
            entity.Property(x => x.TokenHash).HasMaxLength(64);
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.HasIndex(x => x.ExpiresAt);
        });
    }

    private sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
        value => value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : value,
        value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
