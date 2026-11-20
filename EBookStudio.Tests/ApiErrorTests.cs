using EBookStudio.Models;
using Xunit;

namespace EBookStudio.Tests;

public sealed class ApiErrorTests
{
    [Fact]
    public void UnconfiguredEmailErrorIsLocalized()
    {
        var error = new ApiError(
            ApiErrorKind.Conflict,
            "Email delivery is not configured");

        Assert.Contains("인증 메일 서비스", error.UserMessage);
        Assert.Contains("SMTP", error.UserMessage);
    }
}
