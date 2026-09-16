using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Soulsjwa.Api.Features.Audits;
using Soulsjwa.Api.Features.Auth.Entities;
using Soulsjwa.Api.Infrastructure.Data;
using Xunit;

namespace Soulsjwa.ApiTests;

public class LegalDocumentsEndpointTests : ApiTestBase
{
    [Fact]
    public async Task GetDocument_AnonymousUnsetDocument_Returns200WithNullContent()
    {
        var response = await Client.GetAsync("/api/v1/legal/Impressum");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<LegalDocumentDto>();
        body!.Content.Should().BeNull();
    }

    [Fact]
    public async Task GetDocument_UnknownKind_Returns400()
    {
        var response = await Client.GetAsync("/api/v1/legal/NotAKind");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetDocument_LowercaseKind_Returns400()
    {
        // Enum.TryParse uses ignoreCase: false — case matters.
        var response = await Client.GetAsync("/api/v1/legal/impressum");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PutDocument_NotAdmin_Returns403()
    {
        var (_, userKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "user", UserRole.User);
        using var client = TestAuth.CreateAuthenticatedClient(Factory, userKey);

        var response = await client.PutAsJsonAsync(
            "/api/v1/legal/Impressum", new { content = "Operator info here." });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PutDocument_Admin_Returns200AndPersists()
    {
        var (admin, adminKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "admin");
        using var client = TestAuth.CreateAuthenticatedClient(Factory, adminKey);

        var response = await client.PutAsJsonAsync(
            "/api/v1/legal/Datenschutz", new { content = "# Privacy\n\nWe log nothing." });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<LegalDocumentDto>();
        body!.Content.Should().Be("# Privacy\n\nWe log nothing.");

        var get = await Client.GetFromJsonAsync<LegalDocumentDto>("/api/v1/legal/Datenschutz");
        get!.Content.Should().Be("# Privacy\n\nWe log nothing.");

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var audit = await db.AuditLogs
            .Where(a => a.Type == AuditEventTypes.LegalDocumentUpdated && a.ActorUserId == admin.Id)
            .FirstOrDefaultAsync();
        audit.Should().NotBeNull();
        // The audit row records a digest, not the raw content.
        audit!.AfterJson.Should().Contain("Datenschutz");
        audit.AfterJson.Should().NotContain("We log nothing", "the document body must not be duplicated into the audit row");
    }

    [Fact]
    public async Task PutDocument_AtByteLimit_AuditRowIsFarSmallerThanTheDocument()
    {
        // The audit row used to duplicate the full 64 KiB document on both
        // before and after, so one edit wrote ~128 KiB into AuditLogs. It must
        // record a SHA-256 digest and length instead, keeping the row a few
        // hundred bytes regardless of document size.
        var (admin, adminKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "admin");
        using var client = TestAuth.CreateAuthenticatedClient(Factory, adminKey);
        var atLimit = new string('a', 64 * 1024);

        var response = await client.PutAsJsonAsync("/api/v1/legal/Impressum", new { content = atLimit });
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var audit = await db.AuditLogs
            .Where(a => a.Type == AuditEventTypes.LegalDocumentUpdated && a.ActorUserId == admin.Id)
            .FirstOrDefaultAsync();

        audit.Should().NotBeNull();
        audit!.AfterJson!.Length.Should().BeLessThan(500);
        audit.AfterJson.Should().NotContain(atLimit);
    }

    [Fact]
    public async Task PutDocument_UnknownKind_Returns400()
    {
        var (_, adminKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "admin");
        using var client = TestAuth.CreateAuthenticatedClient(Factory, adminKey);

        var response = await client.PutAsJsonAsync("/api/v1/legal/NotAKind", new { content = "x" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PutDocument_OverByteLimit_ReturnsValidationProblem()
    {
        var (_, adminKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "admin");
        using var client = TestAuth.CreateAuthenticatedClient(Factory, adminKey);
        var tooLarge = new string('a', 64 * 1024 + 1);

        var response = await client.PutAsJsonAsync("/api/v1/legal/Impressum", new { content = tooLarge });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PutDocument_AtByteLimit_Succeeds()
    {
        var (_, adminKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "admin");
        using var client = TestAuth.CreateAuthenticatedClient(Factory, adminKey);
        var atLimit = new string('a', 64 * 1024);

        var response = await client.PutAsJsonAsync("/api/v1/legal/Impressum", new { content = atLimit });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PutDocument_ClearingContent_SetsNull()
    {
        var (_, adminKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "admin");
        using var client = TestAuth.CreateAuthenticatedClient(Factory, adminKey);
        await client.PutAsJsonAsync("/api/v1/legal/Impressum", new { content = "Some info." });

        var response = await client.PutAsJsonAsync("/api/v1/legal/Impressum", new { content = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<LegalDocumentDto>();
        body!.Content.Should().BeNull();
    }

    private sealed record LegalDocumentDto(string? Content, DateTime? UpdatedAt);
}
