using FluentAssertions;
using Soulsjwa.Connector.Services;

namespace Soulsjwa.ConnectorTests;

public class ApiResultTests
{
    [Fact]
    public void Success_ShouldExposeDataAndNoError()
    {
        var result = ApiResult<string>.Success("hello");

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().Be("hello");
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void Failure_ShouldExposeErrorAndNoData()
    {
        var result = ApiResult<string>.Failure("boom");

        result.IsSuccess.Should().BeFalse();
        result.Data.Should().BeNull();
        result.ErrorMessage.Should().Be("boom");
    }
}
