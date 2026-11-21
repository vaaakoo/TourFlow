using System.Globalization;

namespace TourFlow.Application.Operations.Destinations;

public static class DestinationCountries
{
    private static readonly Dictionary<string, string> CountryNames = BuildCountryNames();

    public static string? Resolve(string? value) =>
        value is not null && CountryNames.TryGetValue(value.Trim(), out var name) ? name : null;

    private static Dictionary<string, string> BuildCountryNames()
    {
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var region in CultureInfo.GetCultures(CultureTypes.SpecificCultures)
            .Where(culture => !string.IsNullOrEmpty(culture.Name))
            .Select(culture => new RegionInfo(culture.Name)))
        {
            if (region.TwoLetterISORegionName.Length != 2) continue;
            names.TryAdd(region.EnglishName, region.EnglishName);
            names.TryAdd(region.TwoLetterISORegionName, region.EnglishName);
            names.TryAdd(region.ThreeLetterISORegionName, region.EnglishName);
        }

        return names;
    }
}
