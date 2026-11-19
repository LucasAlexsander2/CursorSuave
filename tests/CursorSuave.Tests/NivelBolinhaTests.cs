using CursorSuave;

namespace CursorSuave.Tests;

public class NivelBolinhaTests
{
    [Fact]
    public void AntesDoTempo_DegrauZero()
    {
        Assert.Equal((0, 1.0), NivelBolinha.Calcular(2.9, 3, 0.3));
        Assert.Equal((0, 1.0), NivelBolinha.Calcular(3.0, 3, 0.3));
    }

    [Theory]
    [InlineData(3.075, 1)]
    [InlineData(3.15, 2)]
    [InlineData(3.225, 3)]
    [InlineData(3.3, 4)]
    [InlineData(10, 4)]
    public void Degraus_SobemACada75Ms(double desde, int esperado)
    {
        Assert.Equal(esperado, NivelBolinha.Calcular(desde, 3, 0.2).Degrau);
    }

    [Fact]
    public void OpacidadeAlvo_VaiDe1AteOParado()
    {
        Assert.Equal(1.0, NivelBolinha.Calcular(3.01, 3, 0.2).Opacidade, 6);
        Assert.Equal(0.2, NivelBolinha.Calcular(4, 3, 0.2).Opacidade, 6);
        Assert.Equal(0.6, NivelBolinha.Calcular(3.15, 3, 0.2).Opacidade, 6);
    }

    [Fact]
    public void OpacidadeParadoUm_DegrauSempreZero()
    {
        Assert.Equal((0, 1.0), NivelBolinha.Calcular(100, 3, 1));
    }

    [Fact]
    public void Cursores_BarraSoEntraSeLigada()
    {
        Assert.Equal(new[] { CursorDaBolinha.Normal, CursorDaBolinha.Mao }, NivelBolinha.CursoresDoDegrau(false));
        Assert.Equal(new[] { CursorDaBolinha.Normal, CursorDaBolinha.Mao, CursorDaBolinha.Barra },
            NivelBolinha.CursoresDoDegrau(true));
    }

    [Fact]
    public void Cursores_EsperaSoEntraComMarcacaoLigada()
    {
        Assert.Equal(new[] { CursorDaBolinha.Normal, CursorDaBolinha.Mao, CursorDaBolinha.Espera, CursorDaBolinha.EsperaSegundoPlano },
            NivelBolinha.CursoresDoDegrau(false, true));
        Assert.Equal(new[] { CursorDaBolinha.Normal, CursorDaBolinha.Mao, CursorDaBolinha.Barra, CursorDaBolinha.Espera, CursorDaBolinha.EsperaSegundoPlano },
            NivelBolinha.CursoresDoDegrau(true, true));
        Assert.DoesNotContain(CursorDaBolinha.Espera, NivelBolinha.CursoresDoDegrau(true, false));
    }
}
