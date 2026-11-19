using CursorSuave;

namespace CursorSuave.Tests;

public class ModoTelaTests
{
    [Fact]
    public void Avancado_MostraTudo()
    {
        foreach (CampoTela c in Enum.GetValues<CampoTela>())
        {
            Assert.True(ModoTela.Visivel(c, true));
            Assert.True(ModoTela.Visivel(c, true, true));
        }
    }

    [Theory]
    [InlineData(CampoTela.IniciarComWindows)]
    [InlineData(CampoTela.OcultarEmCapturas)]
    [InlineData(CampoTela.TamanhoAnel)]
    [InlineData(CampoTela.CorAnel)]
    [InlineData(CampoTela.OpacidadeAnel)]
    [InlineData(CampoTela.AtrasoAnel)]
    [InlineData(CampoTela.TamanhoBolinha)]
    [InlineData(CampoTela.MesmoVisualBolinha)]
    [InlineData(CampoTela.MostrarBarra)]
    [InlineData(CampoTela.TrocarAmpulheta)]
    [InlineData(CampoTela.CorMarcacao)]
    [InlineData(CampoTela.PreviaMarcacao)]
    [InlineData(CampoTela.ApagarParado)]
    [InlineData(CampoTela.PausarTelaCheia)]
    public void Simples_MostraOEssencial(CampoTela campo) => Assert.True(ModoTela.Visivel(campo, false));

    [Theory]
    [InlineData(CampoTela.OpacidadeEmClicaveis)]
    [InlineData(CampoTela.FundoEmClicaveis)]
    [InlineData(CampoTela.ManterBolinhaDentro)]
    [InlineData(CampoTela.EfeitoClique)]
    [InlineData(CampoTela.OpacidadesBolinha)]
    [InlineData(CampoTela.AlturaBarra)]
    [InlineData(CampoTela.LarguraBarra)]
    [InlineData(CampoTela.TamanhoMarcacao)]
    [InlineData(CampoTela.OpacidadeMarcacao)]
    [InlineData(CampoTela.SegundosParado)]
    [InlineData(CampoTela.OpacidadeParada)]
    [InlineData(CampoTela.ListaProgramas)]
    public void Simples_EscondeOAvancado(CampoTela campo) => Assert.False(ModoTela.Visivel(campo, false));

    [Fact]
    public void Simples_CorDaBolinhaSoQuandoNaoUsaOMesmoVisual()
    {
        Assert.True(ModoTela.Visivel(CampoTela.CorBolinha, false, false));
        Assert.False(ModoTela.Visivel(CampoTela.CorBolinha, false, true));
    }
}
