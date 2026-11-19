using CursorSuave;

namespace CursorSuave.Tests;

public class PosicaoMenuTests
{
    private static readonly AreaPx Tela = new(0, 0, 1920, 1040);

    [Fact]
    public void PonteiroNoCantoInferiorDireito_AbreParaCimaEEsquerda()
    {
        (int x, int y) = PosicaoMenu.Calcular(1900, 1030, 300, 400, Tela);
        Assert.Equal(1600, x);
        Assert.Equal(630, y);
    }

    [Fact]
    public void PonteiroNoMeio_AbreParaBaixoEDireita()
    {
        (int x, int y) = PosicaoMenu.Calcular(500, 300, 300, 400, Tela);
        Assert.Equal((500, 300), (x, y));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1919, 1039)]
    [InlineData(1919, 0)]
    [InlineData(0, 1039)]
    public void MenuNuncaSaiDaAreaDeTrabalho(int px, int py)
    {
        (int x, int y) = PosicaoMenu.Calcular(px, py, 300, 400, Tela);
        Assert.InRange(x, Tela.Esquerda, Tela.Direita - 300);
        Assert.InRange(y, Tela.Topo, Tela.Base - 400);
    }

    [Fact]
    public void MonitorComCoordenadasNegativas()
    {
        var esquerda = new AreaPx(-1920, 0, 0, 1040);
        (int x, int y) = PosicaoMenu.Calcular(-10, 1035, 300, 400, esquerda);
        Assert.Equal(-310, x);
        Assert.Equal(635, y);
    }
}
