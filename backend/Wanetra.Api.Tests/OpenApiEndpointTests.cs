using System.Net;
using System.Text.Json;

namespace Wanetra.Api.Tests;

public class OpenApiEndpointTests(WanetraApiFactory factory) : IClassFixture<WanetraApiFactory>
{
    [Fact]
    public async Task OpenApi_document_describes_the_API()
    {
        var response = await factory.CreateClient().GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = document.RootElement.GetProperty("paths");
        Assert.True(paths.TryGetProperty("/api/speedtests/run", out _));
        Assert.True(paths.TryGetProperty("/api/settings", out _));
    }
}
