using CursorSuave;

namespace CursorSuave.Tests;

public class CursorSistemaVisualTests
{
    private static (byte B, byte G, byte R, byte A) Pixel(byte[] b, int x, int y)
    {
        int offset = BitConverter.ToInt32(b, 18) + 40;
        int i = offset + ((31 - y) * 32 + x) * 4;
        return (b[i], b[i + 1], b[i + 2], b[i + 3]);
    }

    // Formula antiga (miolo branco, cinza pre-multiplicado) para garantir que opacidade 1 e branco nao mudam nada.
    private static (byte C, byte A) Antigo(int diametro, int x, int y)
    {
        double raioBranco = diametro / 2.0, raioContorno = raioBranco + 1;
        double dx = x + 0.5 - 16, dy = y + 0.5 - 16;
        double d = Math.Sqrt(dx * dx + dy * dy);
        double aContorno = Math.Clamp(raioContorno + 0.5 - d, 0, 1);
        double aBranco = Math.Clamp(raioBranco + 0.5 - d, 0, 1);
        double cor = aContorno > 0 ? 255 * Math.Min(aBranco / aContorno, 1) : 0;
        return ((byte)Math.Round(cor * aContorno), (byte)Math.Round(aContorno * 255));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(12)]
    public void OpacidadeUmEBranco_PixelsIguaisAosAntigos(int d)
    {
        byte[] novo = CursorSistema.GerarCur(d, 1, "#FFFFFF");
        Assert.Equal(novo, CursorSistema.GerarCur(d));
        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                (byte c, byte a) = Antigo(d, x, y);
                Assert.Equal((c, c, c, a), Pixel(novo, x, y));
            }
        }
    }

    [Fact]
    public void Opacidade05_AlfaCentralMetade()
    {
        var (b, g, r, a) = Pixel(CursorSistema.GerarCur(6, 0.5), 16, 16);
        Assert.InRange((int)a, 126, 129);
        Assert.InRange((int)r, 126, 129); // pre-multiplicado
    }

    [Fact]
    public void Opacidade_CabecalhoEHotspotValidos()
    {
        byte[] b = CursorSistema.GerarCur(6, 0.1, "#FF453A");
        Assert.Equal(new byte[] { 0, 0, 2, 0, 1, 0 }, b[..6]);
        Assert.Equal(16, BitConverter.ToUInt16(b, 10));
        Assert.Equal(16, BitConverter.ToUInt16(b, 12));
        Assert.Equal(b.Length - BitConverter.ToInt32(b, 18), BitConverter.ToInt32(b, 14));
    }

    [Fact]
    public void Cor_PixelCentralComACorPedida()
    {
        var (b, g, r, a) = Pixel(CursorSistema.GerarCur(6, 1, "#FF453A"), 16, 16);
        Assert.Equal((0x3A, 0x45, 0xFF, 255), ((int)b, (int)g, (int)r, (int)a));
    }

    [Fact]
    public void Cor_PreMultiplicadaComOpacidade()
    {
        var (b, g, r, a) = Pixel(CursorSistema.GerarCur(6, 0.5, "#FF453A"), 16, 16);
        Assert.InRange((int)a, 127, 128);
        Assert.InRange((int)r, 127, 128);
        Assert.InRange((int)g, 34, 35);
        Assert.InRange((int)b, 29, 30);
    }

    [Fact]
    public void Cor_InvalidaUsaBranco()
    {
        Assert.Equal(CursorSistema.GerarCur(6), CursorSistema.GerarCur(6, 1, "azulzinho"));
    }

    [Fact]
    public void Barra_PadraoIgualAoBrancoOpaco()
    {
        Assert.Equal(CursorSistema.GerarCurBarra(), CursorSistema.GerarCurBarra(18, 2, "#FFFFFF", 1));
    }

    [Fact]
    public void Barra_CorEOpacidade()
    {
        byte[] b = CursorSistema.GerarCurBarra(18, 2, "#0A84FF", 0.5);
        var (pb, pg, pr, pa) = Pixel(b, 16, 16);
        Assert.InRange((int)pa, 127, 128);
        Assert.InRange((int)pr, 5, 6);
        Assert.InRange((int)pg, 66, 67);
        Assert.InRange((int)pb, 127, 128);
        var contorno = Pixel(b, 14, 16);
        Assert.InRange((int)contorno.A, 127, 128);
        Assert.Equal(0, contorno.B);
        Assert.Equal(0, Pixel(b, 0, 0).A);
        Assert.Equal(new byte[] { 0, 0, 2, 0, 1, 0 }, b[..6]);
    }
}

public class ConfiguracaoBolinhaTests
{
    [Fact]
    public void Padroes()
    {
        var c = new Configuracao();
        Assert.Equal(1.0, c.OpacidadeBolinha);
        Assert.Equal(1.0, c.OpacidadeBolinhaEmLinks);
        Assert.Equal(1.0, c.OpacidadeBolinhaParado);
        Assert.Equal("#FFFFFF", c.CorBolinha);
        Assert.False(c.MesmoVisualBolinhaAnel);
    }

    [Fact]
    public void Faixas_ELimitesDaBolinha()
    {
        var c = Configuracao.Carregar(
            "{\"OpacidadeBolinha\":0,\"OpacidadeBolinhaEmLinks\":5,\"OpacidadeBolinhaParado\":-2,\"CorBolinha\":\"xyz\"}", out _);
        Assert.Equal(0.1, c.OpacidadeBolinha);
        Assert.Equal(1, c.OpacidadeBolinhaEmLinks);
        Assert.Equal(0.1, c.OpacidadeBolinhaParado);
        Assert.Equal("#FFFFFF", c.CorBolinha);
    }

    [Fact]
    public void JsonAntigoSemCampos_UsaPadrao()
    {
        var c = Configuracao.Carregar("{\"TamanhoAnel\":30}", out _);
        Assert.Equal(1.0, c.OpacidadeBolinha);
        Assert.Equal("#FFFFFF", c.CorBolinha);
        Assert.False(c.MesmoVisualBolinhaAnel);
    }

    [Fact]
    public void IdaEVolta_DosCamposDaBolinha()
    {
        var original = new Configuracao
        {
            OpacidadeBolinha = 0.7, OpacidadeBolinhaEmLinks = 0.5, OpacidadeBolinhaParado = 0.2,
            CorBolinha = "#30D158", MesmoVisualBolinhaAnel = true
        };
        var lido = Configuracao.Carregar(original.Salvar(), out bool corrompido);
        Assert.False(corrompido);
        Assert.Equal(0.7, lido.OpacidadeBolinha);
        Assert.Equal(0.5, lido.OpacidadeBolinhaEmLinks);
        Assert.Equal(0.2, lido.OpacidadeBolinhaParado);
        Assert.Equal("#30D158", lido.CorBolinha);
        Assert.True(lido.MesmoVisualBolinhaAnel);
    }

    [Fact]
    public void VisualBolinha_Ligado_UsaValoresDoAnel()
    {
        var c = new Configuracao
        {
            MesmoVisualBolinhaAnel = true, Cor = "#0A84FF", Opacidade = 0.6, OpacidadeEmLinks = 0.9, OpacidadeParado = 0.4,
            CorBolinha = "#FF453A", OpacidadeBolinha = 0.3
        };
        Assert.Equal(new VisualDaBolinha("#0A84FF", 0.6, 0.9, 0.4), c.VisualBolinha());
    }

    [Fact]
    public void VisualBolinha_Desligado_UsaValoresDaBolinha()
    {
        var c = new Configuracao
        {
            Cor = "#0A84FF", Opacidade = 0.6, CorBolinha = "#FF453A",
            OpacidadeBolinha = 0.8, OpacidadeBolinhaEmLinks = 0.7, OpacidadeBolinhaParado = 0.5
        };
        Assert.Equal(new VisualDaBolinha("#FF453A", 0.8, 0.7, 0.5), c.VisualBolinha());
    }

    [Fact]
    public void VisualBolinha_Ligado_OpacidadeParadoZeroDaUmDecimo()
    {
        var c = new Configuracao { MesmoVisualBolinhaAnel = true, OpacidadeParado = 0 };
        Assert.Equal(0.1, c.VisualBolinha().OpacidadeParado);
    }
}
