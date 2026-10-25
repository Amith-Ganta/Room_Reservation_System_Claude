using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using RoomReservation.Api.Configuration;
using RoomReservation.Api.Domain;
using RoomReservation.Api.Dtos;

namespace RoomReservation.Tests.Domain;

public class PagingTests
{
    private static readonly PagingOptions Limits = new() { DefaultPageSize = 20, MaxPageSize = 100 };

    [Fact]
    public void An_empty_query_asks_for_the_first_page_of_the_default_size()
    {
        var request = new PageQuery().ToRequest(Limits);

        request.Should().Be(new PageRequest(1, 20));
        request.Skip.Should().Be(0);
    }

    [Fact]
    public void A_page_size_above_the_maximum_is_clamped()
    {
        new PageQuery { PageSize = 5000 }.ToRequest(Limits).PageSize.Should().Be(100);
    }

    [Fact]
    public void Skip_counts_the_rows_of_the_pages_before()
    {
        new PageQuery { Page = 4, PageSize = 25 }.ToRequest(Limits).Skip.Should().Be(75);
    }

    [Theory]
    [InlineData(20, 100, true)]
    [InlineData(100, 100, true)]
    [InlineData(101, 100, false)]
    [InlineData(0, 100, false)]
    [InlineData(20, 1001, false)]
    public void Options_are_valid_only_when_default_is_within_the_maximum(int defaultSize, int maxSize, bool valid)
    {
        var options = new PagingOptions { DefaultPageSize = defaultSize, MaxPageSize = maxSize };

        var results = new List<ValidationResult>();
        var ok = Validator.TryValidateObject(options, new ValidationContext(options), results, validateAllProperties: true);

        ok.Should().Be(valid);
    }
}
