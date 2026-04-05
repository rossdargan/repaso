using FluentAssertions;
using SpanishPractice.Api.Services;

namespace Repaso.Api.Tests;

public class CloseMatchServiceTests
{
    private readonly CloseMatchService _service = new(new EditDistanceService());

    [Theory]
    [InlineData("casa", "csa", true)]
    [InlineData("ventana", "ventnaa", true)]
    [InlineData("biblioteca", "coche", false)]
    public void Evaluate_FlagsCloseMatches(string submitted, string expected, bool isClose)
    {
        var result = _service.Evaluate(submitted, expected);
        result.IsClose.Should().Be(isClose);
    }
}
