using Wanetra.Application.Probes;
using Wanetra.Domain;

namespace Wanetra.Api.Tests.Probes;

public class OutageClassifierTests
{
    private static Diagnosis Classify(params (ProbeRole Role, bool Success)[] results) =>
        OutageClassifier.Classify(results);

    [Fact]
    public void Everything_answering_is_healthy()
    {
        var diagnosis = Classify(
            (ProbeRole.Gateway, true), (ProbeRole.Internet, true), (ProbeRole.Internet, true), (ProbeRole.Dns, true));

        Assert.Null(diagnosis.Scope);
        Assert.Equal(LayerState.Up, diagnosis.Gateway);
        Assert.Equal(LayerState.Up, diagnosis.Internet);
        Assert.Equal(LayerState.Up, diagnosis.Dns);
    }

    [Fact]
    public void One_internet_target_answering_keeps_the_layer_up()
    {
        var diagnosis = Classify((ProbeRole.Gateway, true), (ProbeRole.Internet, false), (ProbeRole.Internet, true));

        Assert.Null(diagnosis.Scope);
        Assert.Equal(LayerState.Up, diagnosis.Internet);
    }

    [Fact]
    public void Unreachable_internet_with_a_responding_gateway_is_a_wan_outage()
    {
        var diagnosis = Classify((ProbeRole.Gateway, true), (ProbeRole.Internet, false), (ProbeRole.Internet, false));

        Assert.Equal(OutageScope.Wan, diagnosis.Scope);
        Assert.Contains("gateway responds", diagnosis.Reason);
    }

    [Fact]
    public void Unreachable_internet_and_gateway_is_a_local_network_outage()
    {
        var diagnosis = Classify((ProbeRole.Gateway, false), (ProbeRole.Internet, false), (ProbeRole.Dns, false));

        Assert.Equal(OutageScope.LocalNetwork, diagnosis.Scope);
    }

    [Fact]
    public void Unreachable_internet_without_a_gateway_target_is_still_a_wan_outage()
    {
        var diagnosis = Classify((ProbeRole.Internet, false));

        Assert.Equal(OutageScope.Wan, diagnosis.Scope);
        Assert.Equal(LayerState.Unknown, diagnosis.Gateway);
    }

    [Fact]
    public void A_dead_gateway_alone_is_not_an_outage_because_routers_often_ignore_ping()
    {
        var diagnosis = Classify((ProbeRole.Gateway, false), (ProbeRole.Internet, true), (ProbeRole.Dns, true));

        Assert.Null(diagnosis.Scope);
        Assert.Equal(LayerState.Down, diagnosis.Gateway);
    }

    [Fact]
    public void Failing_lookups_with_reachable_internet_is_a_dns_outage()
    {
        var diagnosis = Classify((ProbeRole.Gateway, true), (ProbeRole.Internet, true), (ProbeRole.Dns, false));

        Assert.Equal(OutageScope.Dns, diagnosis.Scope);
    }

    [Fact]
    public void Custom_targets_never_raise_an_outage()
    {
        var diagnosis = Classify((ProbeRole.Custom, false), (ProbeRole.Internet, true));

        Assert.Null(diagnosis.Scope);
    }

    [Fact]
    public void No_results_means_every_layer_is_unknown()
    {
        var diagnosis = Classify();

        Assert.Null(diagnosis.Scope);
        Assert.Equal(LayerState.Unknown, diagnosis.Internet);
    }
}
