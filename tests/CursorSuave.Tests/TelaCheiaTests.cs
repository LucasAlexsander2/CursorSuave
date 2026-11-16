using CursorSuave;

namespace CursorSuave.Tests;

public class TelaCheiaTests
{
    private static readonly Retangulo Monitor = new(0, 0, 1920, 1080);

    [Fact]
    public void JanelaIgualAoMonitor_ContaComoTelaCheia() =>
        Assert.True(TelaCheia.CobreMonitor(Monitor, Monitor));

    [Fact]
    public void JanelaUmPixelMenor_NaoConta() =>
        Assert.False(TelaCheia.CobreMonitor(new Retangulo(0, 0, 1920, 1079), Monitor));

    [Fact]
    public void JanelaMaiorQueOMonitor_Conta() =>
        Assert.True(TelaCheia.CobreMonitor(new Retangulo(-8, -8, 1928, 1088), Monitor));

    [Theory]
    [InlineData("Progman")]
    [InlineData("WorkerW")]
    public void AreaDeTrabalho_NuncaConta(string classe) =>
        Assert.True(TelaCheia.EhAreaDeTrabalho(classe));

    [Fact]
    public void OutraClasse_NaoEhAreaDeTrabalho() =>
        Assert.False(TelaCheia.EhAreaDeTrabalho("Chrome_WidgetWin_1"));
}
