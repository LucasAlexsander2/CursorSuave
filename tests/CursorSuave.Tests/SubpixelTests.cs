using CursorSuave;
using Xunit;

namespace CursorSuave.Tests;

public class SubpixelTests
{
    [Fact]
    public void Positivo_SeparaOrigemEFracao()
    {
        (int origem, double frac) = Subpixel.Dividir(100.25, 32);
        Assert.Equal(68, origem);
        Assert.Equal(0.25, frac, 6);
    }

    [Fact]
    public void Negativo_MonitorAEsquerda_UsaPiso()
    {
        (int origem, double frac) = Subpixel.Dividir(-10.25, 32);
        Assert.Equal(-11 - 32, origem);
        Assert.Equal(0.75, frac, 6);
    }

    [Fact]
    public void Inteiro_FracaoZero()
    {
        (int origem, double frac) = Subpixel.Dividir(200, 40);
        Assert.Equal(160, origem);
        Assert.Equal(0, frac);
    }
}
