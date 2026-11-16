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
}
