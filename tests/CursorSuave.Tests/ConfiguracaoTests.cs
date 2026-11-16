using CursorSuave;

namespace CursorSuave.Tests;

public class ConfiguracaoTests
{
    [Fact]
    public void JsonVazio_DaOPadrao()
    {
        var c = Configuracao.Carregar("", out bool corrompido);
        Assert.False(corrompido);
        Assert.Equal(new Configuracao(), c);
        Assert.Equal(26, c.TamanhoAnel);
        Assert.Equal(0.3, c.OpacidadeParado);
        Assert.True(c.FundoEmLinks);
    }

    [Fact]
    public void ValoresForaDaFaixa_SaoLimitados()
    {
        var c = Configuracao.Carregar(
            "{\"TamanhoAnel\":500,\"TamanhoBolinha\":1,\"Opacidade\":9,\"AtrasoMs\":900,\"SegundosParado\":99,\"OpacidadeParado\":-1}",
            out bool corrompido);
        Assert.False(corrompido);
        Assert.Equal(60, c.TamanhoAnel);
        Assert.Equal(4, c.TamanhoBolinha);
        Assert.Equal(1, c.Opacidade);
        Assert.Equal(500, c.AtrasoMs);
        Assert.Equal(30, c.SegundosParado);
        Assert.Equal(0, c.OpacidadeParado);
    }

    [Fact]
    public void CorHexInvalida_VoltaAoPadrao()
    {
        var c = Configuracao.Carregar("{\"Cor\":\"azulzinho\"}", out _);
        Assert.Equal("#FFFFFF", c.Cor);
    }

    [Fact]
    public void CorHexValida_ENormalizada()
    {
        var c = Configuracao.Carregar("{\"Cor\":\"#0af\"}", out _);
        Assert.Equal("#00AAFF", c.Cor);
    }

    [Fact]
    public void IdaEVolta_MantemOsValores()
    {
        var original = new Configuracao
        {
            TamanhoAnel = 40, TamanhoBolinha = 9, Cor = "#FF453A", Opacidade = 0.8, AtrasoMs = 250, ManterBolinhaDentro = false,
            FundoEmLinks = false, EfeitoClique = false, SumirParado = false, PausarTelaCheia = false,
            SegundosParado = 10, OpacidadeParado = 0.5,
            ProgramasPausados = new[] { "game", "obs64" }
        };
        var lido = Configuracao.Carregar(original.Salvar(), out bool corrompido);
        Assert.False(corrompido);
        Assert.Equal(original with { ProgramasPausados = Array.Empty<string>() },
                     lido with { ProgramasPausados = Array.Empty<string>() });
        Assert.Equal(new[] { "game", "obs64" }, lido.ProgramasPausados);
    }

    [Fact]
    public void JsonCorrompido_ESinalizadoParaBackup()
    {
        var c = Configuracao.Carregar("{ isso nao e json", out bool corrompido);
        Assert.True(corrompido);
        Assert.Equal(new Configuracao(), c);
    }

    [Fact]
    public void ListaDeProgramas_RemoveVaziosDuplicadosEExe()
    {
        var c = new Configuracao { ProgramasPausados = new[] { "Game.exe", " game ", "", "obs" } }.Validar();
        Assert.Equal(new[] { "Game", "obs" }, c.ProgramasPausados);
    }
}

public class ConfiguracaoAtualizacaoTests
{
    [Fact]
    public void VerificarAtualizacoes_PadraoTrue_EIdaEVolta()
    {
        Assert.True(new Configuracao().VerificarAtualizacoes);

        var desligada = new Configuracao { VerificarAtualizacoes = false };
        var lida = Configuracao.Carregar(desligada.Salvar(), out bool corrompido);
        Assert.False(corrompido);
        Assert.False(lida.VerificarAtualizacoes);

        // Arquivo antigo sem o campo: continua ligado.
        Assert.True(Configuracao.Carregar("{\"TamanhoAnel\":30}", out _).VerificarAtualizacoes);
    }

    [Fact]
    public void Padroes_DoAtrasoEDaTrava()
    {
        var c = new Configuracao();
        Assert.Equal(100, c.AtrasoMs);
        Assert.True(c.ManterBolinhaDentro);
    }

    [Fact]
    public void AtrasoNegativo_VaiParaZero()
    {
        Assert.Equal(0, Configuracao.Carregar("{\"AtrasoMs\":-5}", out _).AtrasoMs);
    }

    [Fact]
    public void SuavidadeAntiga_MigraParaAtrasoMs()
    {
        var c = Configuracao.Carregar("{\"Suavidade\":0.15}", out bool corrompido);
        Assert.False(corrompido);
        Assert.InRange(c.AtrasoMs, 98, 105);
    }

    [Fact]
    public void AtrasoMsPresente_IgnoraSuavidadeAntiga()
    {
        var c = Configuracao.Carregar("{\"Suavidade\":0.15,\"AtrasoMs\":300}", out _);
        Assert.Equal(300, c.AtrasoMs);
    }
}

public class ConfiguracaoLinksBarraTests
{
    [Fact]
    public void Padroes()
    {
        var c = new Configuracao();
        Assert.Equal(1.0, c.OpacidadeEmLinks);
        Assert.True(c.BarraEmTexto);
    }

    [Theory]
    [InlineData(0.0, 0.2)]
    [InlineData(5.0, 1.0)]
    [InlineData(0.7, 0.7)]
    [InlineData(double.NaN, 1.0)]
    public void OpacidadeEmLinks_FicaNaFaixa(double entrada, double esperado)
    {
        Assert.Equal(esperado, (new Configuracao { OpacidadeEmLinks = entrada }).Validar().OpacidadeEmLinks, 6);
    }

    [Fact]
    public void IdaEVolta()
    {
        var c = new Configuracao { OpacidadeEmLinks = 0.75, BarraEmTexto = false };
        Configuracao l = Configuracao.Carregar(c.Salvar(), out bool corrompido);
        Assert.False(corrompido);
        Assert.Equal(0.75, l.OpacidadeEmLinks, 6);
        Assert.False(l.BarraEmTexto);
    }
}
