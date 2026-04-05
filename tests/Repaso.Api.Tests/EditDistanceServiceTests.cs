using FluentAssertions;
using SpanishPractice.Api.Services;

namespace Repaso.Api.Tests;

public class EditDistanceServiceTests
{
    private readonly EditDistanceService _service = new();

    [Theory]
    [InlineData("casa", "casa", 0)]
    [InlineData("casa", "casas", 1)]
    [InlineData("gato", "gata", 1)]
    [InlineData("perro", "gato", 4)]
    public void Calculate_ReturnsExpectedDistance(string source, string target, int expected)
    {
        var result = _service.Calculate(source, target);
        result.Should().Be(expected);
    }
}
