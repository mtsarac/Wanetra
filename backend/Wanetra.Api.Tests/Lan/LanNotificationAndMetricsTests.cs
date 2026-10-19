using Microsoft.Extensions.DependencyInjection;
using Wanetra.Api.Prometheus;
using Wanetra.Application.Notifications;
using Wanetra.Domain;
using static Wanetra.Api.Tests.Lan.LanApiFactory;

namespace Wanetra.Api.Tests.Lan;

public class LanNotificationAndMetricsTests
{
    private static DeviceAlert Alert(DeviceAlertKind kind, DeviceAlertStatus status = DeviceAlertStatus.Active) => new()
    {
        Kind = kind,
        Status = status,
        StartedAt = new DateTime(2026, 10, 18, 10, 0, 0, DateTimeKind.Utc),
        EndedAt = status == DeviceAlertStatus.Resolved ? new DateTime(2026, 10, 18, 10, 12, 30, DateTimeKind.Utc) : null,
        DeviceLabel = "NAS",
        Mac = "aa:bb:cc:00:00:02",
        Ip = "192.168.1.20",
        Vendor = "Synology Incorporated",
    };

    [Fact]
    public void A_new_device_notice_names_it_and_points_to_the_devices_page()
    {
        var message = NotificationMessageBuilder.Build(NotificationTrigger.Opened, Alert(DeviceAlertKind.NewDevice));

        Assert.Equal("New device on your network", message.Title);
        Assert.Equal("device.new", message.Event);
        Assert.Contains("NAS joined the network", message.Body);
        Assert.Contains("MAC: aa:bb:cc:00:00:02", message.Body);
        Assert.Contains("IP: 192.168.1.20", message.Body);
        Assert.Contains("Vendor: Synology Incorporated", message.Body);
        Assert.Contains("Devices page", message.Body);
    }

    [Fact]
    public void An_offline_notice_and_its_recovery_are_distinct_messages()
    {
        var offline = NotificationMessageBuilder.Build(NotificationTrigger.Opened, Alert(DeviceAlertKind.Offline));
        var back = NotificationMessageBuilder.Build(NotificationTrigger.Recovered, Alert(DeviceAlertKind.Offline, DeviceAlertStatus.Resolved));

        Assert.Equal("device.offline", offline.Event);
        Assert.Contains("NAS stopped answering", offline.Body);
        Assert.Equal("device.online", back.Event);
        Assert.Contains("NAS is back online after 12m 30s", back.Body);
    }

    [Fact]
    public void Only_an_offline_alert_has_a_recovery()
    {
        Assert.False(Alert(DeviceAlertKind.NewDevice, DeviceAlertStatus.Resolved).IsRecovered);
        Assert.True(Alert(DeviceAlertKind.Offline, DeviceAlertStatus.Resolved).IsRecovered);
        Assert.False(Alert(DeviceAlertKind.Offline).IsRecovered);
    }

    [Fact]
    public async Task A_device_notice_that_failed_is_delivered_by_the_retry_pass()
    {
        await using var factory = new LanApiFactory();
        _ = factory.CreateClient();
        await factory.AddWebhookDestinationAsync();
        factory.Scanner.Hosts.Add(Host("192.168.1.1", "b8:27:eb:00:00:01"));
        await factory.ScanAsync();
        factory.Provider.Failing = true;
        factory.Scanner.Hosts.Add(Host("192.168.1.40", "aa:bb:cc:00:00:04", "cam-7"));

        await factory.ScanAsync();

        Assert.False((await factory.AlertsAsync()).Single().NotificationSent);
        factory.Provider.Failing = false;
        await factory.WithScopeAsync(async services =>
        {
            await services.GetRequiredService<NotificationDispatcher>().DispatchPendingAsync(CancellationToken.None);
            return 0;
        });

        Assert.True((await factory.AlertsAsync()).Single().NotificationSent);
        Assert.Single(factory.Provider.Sent);
    }

    [Fact]
    public async Task Device_counts_and_scans_are_exposed_as_metrics()
    {
        var metrics = new WanetraMetrics();

        metrics.SetDeviceCounts(total: 12, online: 9, unknown: 2);
        metrics.RecordScan(success: true, durationSeconds: 11);
        metrics.RecordScan(success: false, durationSeconds: 1);

        using var buffer = new MemoryStream();
        await metrics.Registry.CollectAndExportAsTextAsync(buffer);
        var text = System.Text.Encoding.UTF8.GetString(buffer.ToArray());
        Assert.Contains("wanetra_lan_devices 12", text);
        Assert.Contains("wanetra_lan_devices_online 9", text);
        Assert.Contains("wanetra_lan_devices_unknown 2", text);
        Assert.Contains("wanetra_lan_scans_total 2", text);
        Assert.Contains("wanetra_lan_last_scan_success 0", text);
    }

    [Fact]
    public async Task Metrics_follow_the_scan_results()
    {
        await using var factory = new LanApiFactory();
        var client = factory.CreateClient();
        factory.Scanner.Hosts.AddRange([Host("192.168.1.1", "b8:27:eb:00:00:01"), Host("192.168.1.20", "aa:bb:cc:00:00:02")]);
        await factory.ScanAsync();
        factory.Scanner.Hosts.Add(Host("192.168.1.40", "aa:bb:cc:00:00:04"));
        await factory.ScanAsync();

        var text = await client.GetStringAsync("/metrics");

        Assert.Contains("wanetra_lan_devices 3", text);
        Assert.Contains("wanetra_lan_devices_online 3", text);
        Assert.Contains("wanetra_lan_devices_unknown 1", text);
        Assert.Contains("wanetra_lan_last_scan_success 1", text);
    }
}
