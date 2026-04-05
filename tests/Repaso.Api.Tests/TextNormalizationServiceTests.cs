using FluentAssertions;
using SpanishPractice.Api.Services;

namespace Repaso.Api.Tests;

public class TextNormalizationServiceTests
{
    private readonly TextNormalizationService _service = new();

    [Theory]
    [InlineData("Canción", "cancion")]
    [InlineData(" árbol  rojo ", "arbol rojo")]
    [InlineData("¿Dónde?", "donde")]
    public void Normalize_RemovesNoiseAndAccents_WhenEnabled(string input, string expected)
    {
        var result = _service.Normalize(input, removeAccents: true);
        result.Should().Be(expected);
    }

    [Fact]
    public void Normalize_PreservesAccents_WhenRequested()
    {
        var result = _service.Normalize("Canción", removeAccents: false);
        result.Should().Be("canción");
    }
}
