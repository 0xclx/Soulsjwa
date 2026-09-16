using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace Soulsjwa.ApiTests;

public class SearchUsersEndpointTests : ApiTestBase
{
    [Fact]
    public async Task SearchUsers_Unauthenticated_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/users/search?q=foo");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

}
