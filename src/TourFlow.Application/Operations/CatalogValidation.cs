using FluentValidation;

namespace TourFlow.Application.Operations;

internal static class CatalogValidation
{
    internal static void CatalogImage<T>(this IRuleBuilder<T, string?> rule) =>
        rule.MaximumLength(2048)
            .Must(value => string.IsNullOrWhiteSpace(value) ||
                (Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) &&
                    uri.Scheme is ("https" or "http") && !string.IsNullOrEmpty(uri.Host)))
            .WithMessage("Use an absolute HTTP or HTTPS image URL.");
}
