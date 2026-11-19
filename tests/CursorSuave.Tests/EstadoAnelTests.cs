using CursorSuave;

namespace CursorSuave.Tests;

public class EstadoAnelTests
{
    private const double Dt = 1.0 / 60;

    private static void Rodar(EstadoAnel e, double segundos, bool mexeu, bool pressionado, bool link, bool visivel = true)
    {
        int n = (int)Math.Round(segundos / Dt);
        for (int i = 0; i < n; i++) e.Atualizar(Dt, mexeu, pressionado, link, visivel);
    }

    [Fact]
    public void SobreLink_DiametroContinua26ePreenchimentoVai1()
    {
        var e = new EstadoAnel();
        Rodar(e, 3, true, false, true);
        Assert.Equal(26, e.Diametro, 6);
        Assert.Equal(1, e.Preenchimento, 2);
    }

    [Fact]
    public void BotaoPressionado_EscalaConvergePara08()
    {
        var e = new EstadoAnel();
        Rodar(e, 3, true, true, false);
        Assert.Equal(0.8, e.EscalaClique, 3);
    }

    [Fact]
    public void ParadoPor3sOuMais_OpacidadeFicaApagadaSemSumir()
    {
        var e = new EstadoAnel();
        Rodar(e, 5, false, false, false);
        Assert.Equal(EstadoAnel.OpacidadeParadoPadrao, e.Opacidade, 6);
    }

    [Fact]
    public void ParadoMenosDe3s_OpacidadeContinua1()
    {
        var e = new EstadoAnel();
        Rodar(e, 2.9, false, false, false);
        Assert.Equal(1, e.Opacidade, 6);
    }

    [Fact]
    public void Mexer_DevolveOpacidadeA1Em120ms()
    {
        var e = new EstadoAnel();
        Rodar(e, 4, false, false, false);
        Assert.Equal(EstadoAnel.OpacidadeParadoPadrao, e.Opacidade, 6);
        Rodar(e, 0.125, true, false, false);
        Assert.Equal(1, e.Opacidade, 6);
    }

    [Fact]
    public void CursorInvisivel_AnelContinuaVisivel()
    {
        var e = new EstadoAnel();
        var (_, op) = e.Atualizar(Dt, true, false, false, false);
        Assert.Equal(1, op);
    }

    [Fact]
    public void DtGrande_NaoUltrapassaOAlvo()
    {
        var e = new EstadoAnel();
        var (d, op) = e.Atualizar(10, true, true, true, true);
        Assert.Equal(26, d, 6);
        Assert.InRange(e.EscalaClique, 0.8, 1);
        Assert.InRange(e.Preenchimento, 0, 1);
        Assert.InRange(op, 0, 1);
    }

    [Fact]
    public void Carregamento_SobeEm120msEDesceEm200ms()
    {
        var e = new EstadoAnel();
        double dt = 0.01;
        for (int i = 0; i < 6; i++) e.Atualizar(dt, false, false, false, true, true, true);
        Assert.Equal(0.5, e.Carregamento, 6);
        for (int i = 0; i < 6; i++) e.Atualizar(dt, false, false, false, true, true, true);
        Assert.Equal(1, e.Carregamento, 6);
        for (int i = 0; i < 10; i++) e.Atualizar(dt, false, false, false, true, true, false);
        Assert.Equal(0.5, e.Carregamento, 6);
        for (int i = 0; i < 10; i++) e.Atualizar(dt, false, false, false, true, true, false);
        Assert.Equal(0, e.Carregamento, 6);
    }

    [Fact]
    public void AnguloCarregamento_Avanca360PorSegundoEVoltaA0()
    {
        var e = new EstadoAnel();
        e.Atualizar(0.5, false, false, false, true, true, true);
        Assert.Equal(180, e.AnguloCarregamento, 6);
        e.Atualizar(0.75, false, false, false, true, true, true);
        Assert.Equal(90, e.AnguloCarregamento, 6); // 180 + 270 = 450 -> 90
        Assert.InRange(e.AnguloCarregamento, 0, 360);
    }

    [Fact]
    public void SemCarregar_CarregamentoFicaEm0_EMouseParadoNaoApagaMarcacao()
    {
        var e = new EstadoAnel();
        Rodar(e, 1, false, false, false);
        Assert.Equal(0, e.Carregamento);
        // Mouse parado por muito tempo (anel apaga) com carregando: a marcação segue em 1.
        e.Configurar(26, 1, 0.3, true);
        for (int i = 0; i < 300; i++) e.Atualizar(Dt, false, false, false, true, true, true);
        Assert.Equal(1, e.Carregamento, 6);
        Assert.True(e.Opacidade < 1);
    }
}


public class EstadoAnelOpacidadeLinksTests
{
    private const double Dt = 1.0 / 60;

    private static void Rodar(EstadoAnel e, double s, bool link, bool fundo = true)
    {
        for (int i = 0; i < (int)Math.Round(s / Dt); i++) e.Atualizar(Dt, true, false, link, true, fundo);
    }

    [Fact]
    public void SobreClicavel_BordaConvergeParaOpacidadeEmLinks_ESaindoVolta()
    {
        var e = new EstadoAnel();
        e.Configurar(26, 3, 0.3, true, 0.6, 1.0);
        Assert.Equal(0.6, e.OpacidadeBorda, 6);
        Rodar(e, 3, true);
        Assert.Equal(1.0, e.OpacidadeBorda, 2);
        Rodar(e, 3, false);
        Assert.Equal(0.6, e.OpacidadeBorda, 2);
    }

    [Fact]
    public void SemFundoEmLinks_BordaAindaRealcaEPreenchimentoFica0()
    {
        var e = new EstadoAnel();
        e.Configurar(26, 3, 0.3, true, 0.5, 0.9);
        Rodar(e, 3, true, fundo: false);
        Assert.Equal(0.9, e.OpacidadeBorda, 2);
        Assert.Equal(0, e.Preenchimento, 2);
    }

    [Fact]
    public void OpacidadeEmLinksMenorQueOpacidade_ValeOValorConfigurado()
    {
        var e = new EstadoAnel();
        e.Configurar(26, 3, 0.3, true, 0.8, 0.4);
        Rodar(e, 3, true);
        Assert.Equal(0.4, e.OpacidadeBorda, 2);
    }
}
