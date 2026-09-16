using FluentAssertions;
using Soulsjwa.Api.Common.Models;
using Xunit;

namespace Soulsjwa.UnitTests;

public class PaginatedResponseTests
{
    [Fact]
    public void HasNextPage_TrueWhenMoreItemsExist()
    {
        var response = new PaginatedResponse<string>(["a", "b"], TotalCount: 10, Page: 1, PageSize: 2);
        response.HasNextPage.Should().BeTrue();
    }

    [Fact]
    public void HasNextPage_FalseWhenOnLastPage()
    {
        var response = new PaginatedResponse<string>(["a", "b"], TotalCount: 4, Page: 2, PageSize: 2);
        response.HasNextPage.Should().BeFalse();
    }

    [Fact]
    public void HasNextPage_FalseWhenExactlyOnePage()
    {
        var response = new PaginatedResponse<string>(["a", "b"], TotalCount: 2, Page: 1, PageSize: 2);
        response.HasNextPage.Should().BeFalse();
    }

    [Fact]
    public void HasPreviousPage_FalseOnFirstPage()
    {
        var response = new PaginatedResponse<string>(["a"], TotalCount: 5, Page: 1, PageSize: 2);
        response.HasPreviousPage.Should().BeFalse();
    }

    [Fact]
    public void HasPreviousPage_TrueOnSecondPage()
    {
        var response = new PaginatedResponse<string>(["c", "d"], TotalCount: 5, Page: 2, PageSize: 2);
        response.HasPreviousPage.Should().BeTrue();
    }

    [Fact]
    public void Items_ReturnsCorrectItems()
    {
        var items = new List<int> { 1, 2, 3 };
        var response = new PaginatedResponse<int>(items, TotalCount: 10, Page: 1, PageSize: 3);
        response.Items.Should().BeEquivalentTo(new[] { 1, 2, 3 });
    }

    [Fact]
    public void TotalCount_ReflectsInput()
    {
        var response = new PaginatedResponse<int>([], TotalCount: 42, Page: 1, PageSize: 10);
        response.TotalCount.Should().Be(42);
    }

    [Fact]
    public void EmptyItems_HasNoNextOrPreviousPage()
    {
        var response = new PaginatedResponse<string>([], TotalCount: 0, Page: 1, PageSize: 10);
        response.HasNextPage.Should().BeFalse();
        response.HasPreviousPage.Should().BeFalse();
    }

    [Fact]
    public void SinglePage_HasCorrectPagination()
    {
        var response = new PaginatedResponse<string>(["a"], TotalCount: 1, Page: 1, PageSize: 10);
        response.HasNextPage.Should().BeFalse();
        response.HasPreviousPage.Should().BeFalse();
    }
}
