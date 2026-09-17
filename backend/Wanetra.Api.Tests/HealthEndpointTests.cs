using System.Net;

namespace Wanetra.Api.Tests;

public class HealthEndpointTests(WanetraApiFactory factory) : IClassFixture<WanetraApiFactory>
{
    [Theory]
    [InlineData("/health")]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task Health_endpoints_return_Healthy(string path)
    {
        var response = await factory.CreateClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }
}
