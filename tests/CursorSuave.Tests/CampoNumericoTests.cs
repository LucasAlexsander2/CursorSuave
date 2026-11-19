using CursorSuave;

namespace CursorSuave.Tests;

public class CampoNumericoTests
{
    [Theory]
    [InlineData("12", 12)]
    [InlineData("12,5", 12.5)]
    [InlineData("12.5", 12.5)]
    [InlineData("120 ms", 120)]
    [InlineData("60%", 60)]
    [InlineData("  7 px ", 7)]
    public void AceitaFormatosComUnidade(string texto, double esperado)
    {
        Assert.Equal(esperado, CampoNumerico.Interpretar(texto, 0, 1000, 1));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("12abc34")]
    [InlineData(null)]
    public void TextoInvalido_DevolveNull(string? texto)
    {
        Assert.Null(CampoNumerico.Interpretar(texto, 0, 100, 0));
    }

    [Fact]
    public void ForaDaFaixa_ELimitado()
    {
        Assert.Equal(10, CampoNumerico.Interpretar("3", 10, 100, 0));
        Assert.Equal(100, CampoNumerico.Interpretar("999", 10, 100, 0));
    }

    [Fact]
    public void ArredondaNasCasasPedidas()
    {
        Assert.Equal(12.35, CampoNumerico.Interpretar("12,347", 0, 100, 2));
        Assert.Equal(12, CampoNumerico.Interpretar("12,4", 0, 100, 0));
        Assert.Equal(13, CampoNumerico.Interpretar("12,6", 0, 100, 0));
    }

    [Fact]
    public void OpacidadeEPorcento_IdaEVolta()
    {
        Assert.Equal(0.6, CampoNumerico.PorcentoParaOpacidade(60));
        Assert.Equal(60, CampoNumerico.OpacidadeParaPorcento(0.6));
        Assert.Equal(30, CampoNumerico.OpacidadeParaPorcento(0.3));
    }
}

public class ConfiguracaoFase4Tests
{
    [Fact]
    public void NovosPadroes_ValemParaNewEParaJsonVazio()
    {
        foreach (Configuracao c in new[] { new Configuracao(), Configuracao.Carregar("{}", out _) })
        {
            Assert.Equal(25, c.TamanhoAnel);
            Assert.Equal(5, c.TamanhoBolinha);
            Assert.Equal(0.6, c.Opacidade);
            Assert.Equal(1.0, c.OpacidadeEmLinks);
            Assert.Equal(14, c.AlturaBarra);
            Assert.Equal(1, c.LarguraBarra);
            Assert.Equal(100, c.AtrasoMs);
            Assert.False(c.SumirParado);
            Assert.Equal(0.3, c.OpacidadeParado);
            Assert.False(c.PausarTelaCheia);
        }
    }

    [Fact]
    public void ComPadroesVisuais_VoltaOVisualEMantemProgramasEAtualizacoes()
    {
        var alterada = new Configuracao
        {
            TamanhoAnel = 40, TamanhoBolinha = 9, Cor = "#FF453A", Opacidade = 0.9, AtrasoMs = 300,
            SumirParado = true, PausarTelaCheia = true, MesmoVisualBolinhaAnel = true, BarraEmTexto = false,
            VerificarAtualizacoes = false, ProgramasPausados = new[] { "game", "obs64" }
        };
        Configuracao r = alterada.ComPadroesVisuais();
        Assert.Equal(new Configuracao { VerificarAtualizacoes = false, ProgramasPausados = alterada.ProgramasPausados }, r);
        Assert.Equal(new[] { "game", "obs64" }, r.ProgramasPausados);
        Assert.False(r.VerificarAtualizacoes);
        Assert.Equal(25, r.TamanhoAnel);
    }
}
