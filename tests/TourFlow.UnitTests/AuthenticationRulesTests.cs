using TourFlow.Application.Authentication;
using TourFlow.Domain.Entities;
using TourFlow.Domain.Enums;
using Xunit;

namespace TourFlow.UnitTests;

public sealed class AuthenticationRulesTests
{
    [Theory]
    [InlineData("invalid", "StrongPassword1!")]
    [InlineData("test@example.test", "short")]
    public void RegistrationRejectsInvalidCredentials(string email, string password)
    {
        var request = new RegisterRequest(email, password, "Test", "Customer", "555-0100");
        Assert.False(new RegisterRequestValidator().Validate(request).IsValid);
    }

    [Fact]
    public void RotatedTokenIsRevokedAndReplacementKeepsFamily()
    {
        var user = new ApplicationUser("test@example.test", UserRole.Customer);
        var now = DateTime.UtcNow;
        var original = new RefreshToken(user, "original-test-digest", now.AddDays(1));
        original.MarkRotated(now);
        var replacement = new RefreshToken(user, "replacement-test-digest", original.ExpiresAt, original.FamilyId);

        Assert.False(original.IsActive);
        Assert.True(original.WasRotated);
        Assert.Equal(now, original.RevokedAt);
        Assert.Equal(original.FamilyId, replacement.FamilyId);
        Assert.Equal(original.ExpiresAt, replacement.ExpiresAt);
    }
}
