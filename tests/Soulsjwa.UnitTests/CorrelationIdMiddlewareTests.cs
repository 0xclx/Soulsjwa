using System.Diagnostics;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Soulsjwa.Api.Middleware;
using Xunit;

namespace Soulsjwa.UnitTests;

public class CorrelationIdMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_PreservesIncomingCorrelationId()
    {
        var context = CreateContext();
        context.Request.Headers["X-Correlation-Id"] = "incoming-correlation-id";
        var middleware = new CorrelationIdMiddleware(async httpContext =>
        {
            await httpContext.Response.WriteAsync("ok");
        }, NullLogger<CorrelationIdMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        context.Response.Headers["X-Correlation-Id"].ToString().Should().Be("incoming-correlation-id");
    }

    [Fact]
    public async Task InvokeAsync_UsesCurrentTraceIdWhenHeaderIsMissing()
    {
        using var activity = new Activity("test-request").Start();
        var context = CreateContext();
        var middleware = new CorrelationIdMiddleware(async httpContext =>
        {
            await httpContext.Response.WriteAsync("ok");
        }, NullLogger<CorrelationIdMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        context.Response.Headers["X-Correlation-Id"].ToString().Should().Be(activity.TraceId.ToString());
    }

    private static DefaultHttpContext CreateContext()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        return context;
    }
}
