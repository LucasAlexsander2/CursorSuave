using CursorSuave;

namespace CursorSuave.Tests;

public class EstimativaDownloadTests
{
    [Fact]
    public void SemDadosBastantes_DaNull()
    {
        var e = new EstimativaDownload();
        Assert.Null(e.Registrar(2, TimeSpan.FromSeconds(5)));
        Assert.Null(e.Registrar(50, TimeSpan.FromSeconds(0.5)));
    }

    [Fact]
    public void TaxaConstante_DaEstimativaCerta()
    {
        var e = new EstimativaDownload();
        // 10% em 10 s: faltam 90 s. Depois, 50% em 50 s (mesma taxa): o bruto é 50 s e a média anda 30%.
        Assert.Equal(90, e.Registrar(10, TimeSpan.FromSeconds(10))!.Value, 6);
        Assert.Equal(0.3 * 50 + 0.7 * 90, e.Registrar(50, TimeSpan.FromSeconds(50))!.Value, 6);
    }

    [Fact]
    public void Suavizacao_NaoDeixaAEstimativaPular()
    {
        var e = new EstimativaDownload();
        double primeira = e.Registrar(10, TimeSpan.FromSeconds(10))!.Value; // 90
        // Salto grande: bruto seria 5 s (80% em 20 s), mas a média só anda 30% do caminho.
        double segunda = e.Registrar(80, TimeSpan.FromSeconds(20))!.Value;
        Assert.InRange(segunda, 5, primeira);
        Assert.Equal(0.3 * 5 + 0.7 * 90, segunda, 6);
    }

    [Theory]
    [InlineData(0.5, "menos de 1 s")]
    [InlineData(8, "cerca de 8 s")]
    [InlineData(65, "cerca de 1 min 05 s")]
    public void Formatar(double segundos, string esperado) =>
        Assert.Equal(esperado, EstimativaDownload.Formatar(segundos));
}
