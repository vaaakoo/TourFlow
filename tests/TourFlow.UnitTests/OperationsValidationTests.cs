using TourFlow.Application.Operations.Destinations;
using TourFlow.Application.Operations.Tours;
using TourFlow.Application.Operations.Vehicles;
using Xunit;

namespace TourFlow.UnitTests;

public sealed class OperationsValidationTests
{
    [Theory]
    [InlineData("Georgia", "Georgia")]
    [InlineData("ge", "Georgia")]
    [InlineData("Atlantis", null)]
    public void ResolvesCountryNamesAndCodes(string input, string? expected) =>
        Assert.Equal(expected, DestinationCountries.Resolve(input));

    [Theory]
    [InlineData(0, 20, "name")]
    [InlineData(1, 101, "name")]
    [InlineData(1, 20, "unsupported")]
    public void RejectsInvalidPaginationAndSort(int page, int pageSize, string sort)
    {
        var options = new DestinationListOptions { Page = page, PageSize = pageSize, Sort = sort };
        Assert.False(new DestinationListOptionsValidator().Validate(options).IsValid);
    }

    [Fact]
    public void RejectsInvertedRanges()
    {
        Assert.False(new TourListOptionsValidator().Validate(new TourListOptions { MinPrice = 100, MaxPrice = 90 }).IsValid);
        Assert.False(new VehicleListOptionsValidator().Validate(new VehicleListOptions { MinCapacity = 5, MaxCapacity = 4 }).IsValid);
    }
}
