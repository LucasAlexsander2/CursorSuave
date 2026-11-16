using CursorSuave;

namespace CursorSuave.Tests;

public class SeguidorTests
{
    private const double Limite = 8;
    private static readonly double AtrasoPadrao = -1.0 / 60 / Math.Log(0.85);

    [Fact]
    public void MouseParado_ConvergeParaOMouse()
    {
        var s = new Seguidor(AtrasoPadrao, x: 0, y: 0);
        for (int i = 0; i < 600; i++) s.Atualizar(100, 50, 1.0 / 60, Limite);
        Assert.Equal(100, s.X, 3);
        Assert.Equal(50, s.Y, 3);
    }

    [Fact]
    public void UmQuadroDe60fps_Anda15PorCento()
    {
        var s = new Seguidor(AtrasoPadrao, x: 0, y: 0);
        s.Atualizar(10, 0, 1.0 / 60, 1000);
        Assert.Equal(1.5, s.X, 9);
        Assert.Equal(0, s.Y, 9);
    }

    [Fact]
    public void Dt30fps_EquivaleADoisQuadrosDe60()
    {
        var s = new Seguidor(AtrasoPadrao, x: 0, y: 0);
        s.Atualizar(100, 0, 1.0 / 30, 1000);
        Assert.Equal(100 * (1 - 0.85 * 0.85), s.X, 9);
    }

    [Fact]
    public void SaltoGrande_FicaNoLimiteDaTrava()
    {
        var s = new Seguidor(AtrasoPadrao, x: 0, y: 0);
        s.Atualizar(500, 0, 1.0 / 60, Limite);
        double d = Math.Sqrt(Math.Pow(s.X - 500, 2) + Math.Pow(s.Y, 2));
        Assert.Equal(Limite, d, 9);
    }

    [Fact]
    public void DistanciaDentroDoLimite_TravaNaoAltera()
    {
        var s = new Seguidor(AtrasoPadrao, x: 0, y: 0);
        s.Atualizar(10, 0, 1.0 / 60, 100);
        Assert.Equal(1.5, s.X, 9);
    }

    [Fact]
    public void DistanciaZero_SemDivisaoPorZero()
    {
        var s = new Seguidor(AtrasoPadrao, x: 7, y: 7);
        s.Atualizar(7, 7, 1.0 / 60, 0);
        Assert.Equal(7, s.X);
        Assert.Equal(7, s.Y);
    }

    [Theory]
    [InlineData(8)]
    [InlineData(17)]
    [InlineData(6.4)]
    public void LimiteVariavel_TravaNoRaioInformado(double limite)
    {
        var s = new Seguidor(AtrasoPadrao, x: 0, y: 0);
        s.Atualizar(500, 0, 1.0 / 60, limite);
        double d = Math.Sqrt(Math.Pow(s.X - 500, 2) + Math.Pow(s.Y, 2));
        Assert.Equal(limite, d, 9);
    }

    [Fact]
    public void Atraso0_AnelFicaIgualAoMouse()
    {
        var s = new Seguidor(0, x: 0, y: 0);
        s.Atualizar(100, 40, 1.0 / 60, 1000);
        Assert.Equal(100, s.X, 9);
        Assert.Equal(40, s.Y, 9);
    }

    [Fact]
    public void Atraso100ms_Apos100ms_Anda63Por()
    {
        var s = new Seguidor(0.1, x: 0, y: 0);
        s.Atualizar(100, 0, 0.1, 1000);
        Assert.Equal(100 * (1 - Math.Exp(-1)), s.X, 9);
    }

    [Fact]
    public void SemTrava_DistanciaPodePassarDoLimite()
    {
        var s = new Seguidor(0.5, x: 0, y: 0) { ManterDentro = false };
        s.Atualizar(500, 0, 1.0 / 60, Limite);
        Assert.True(Math.Abs(s.X - 500) > Limite);
    }
}
