using CursorSuave;

namespace CursorSuave.Tests;

public class CursorSistemaTests
{
    [Fact]
    public void CabecalhoComeca000002000100()
    {
        byte[] b = CursorSistema.GerarCur();
        Assert.Equal(new byte[] { 0, 0, 2, 0, 1, 0 }, b[..6]);
    }

    [Fact]
    public void HotspotNoCentro()
    {
        byte[] b = CursorSistema.GerarCur();
        Assert.Equal(16, BitConverter.ToUInt16(b, 10));
        Assert.Equal(16, BitConverter.ToUInt16(b, 12));
    }

    [Fact]
    public void TamanhoDeclaradoIgualAoTamanhoReal()
    {
        byte[] b = CursorSistema.GerarCur();
        int declarado = BitConverter.ToInt32(b, 14);
        int offset = BitConverter.ToInt32(b, 18);
        Assert.Equal(b.Length - offset, declarado);
    }
}

public class CursorSistemaDiametroTests
{
    [Theory]
    [InlineData(4)]
    [InlineData(12)]
    public void CabecalhoValido_ParaDiametrosExtremos(int d)
    {
        byte[] b = CursorSistema.GerarCur(d);
        Assert.Equal(new byte[] { 0, 0, 2, 0, 1, 0 }, b[..6]);
        Assert.Equal(b.Length - BitConverter.ToInt32(b, 18), BitConverter.ToInt32(b, 14));
        Assert.Equal(16, BitConverter.ToUInt16(b, 10));
        Assert.Equal(16, BitConverter.ToUInt16(b, 12));
    }

    [Fact]
    public void DiametroMaior_PintaMaisPixels()
    {
        static int Opacos(byte[] b)
        {
            int n = 0;
            for (int i = 22 + 40 + 3; i < 22 + 40 + 32 * 32 * 4; i += 4) if (b[i] > 0) n++;
            return n;
        }
        Assert.True(Opacos(CursorSistema.GerarCur(12)) > Opacos(CursorSistema.GerarCur(4)));
    }
}

public class CursorSistemaBarraTests
{
    // Pixel (x, y) contado de cima para baixo; o arquivo guarda as linhas de baixo para cima.
    private static (byte B, byte A) Pixel(byte[] b, int x, int y)
    {
        int offset = BitConverter.ToInt32(b, 18) + 40;
        int i = offset + ((31 - y) * 32 + x) * 4;
        return (b[i], b[i + 3]);
    }

    [Fact]
    public void Barra_CabecalhoEHotspotValidos()
    {
        byte[] b = CursorSistema.GerarCurBarra();
        Assert.Equal(new byte[] { 0, 0, 2, 0, 1, 0 }, b[..6]);
        Assert.Equal(16, BitConverter.ToUInt16(b, 10));
        Assert.Equal(16, BitConverter.ToUInt16(b, 12));
        Assert.Equal(b.Length - BitConverter.ToInt32(b, 18), BitConverter.ToInt32(b, 14));
    }

    [Fact]
    public void Barra_PixelCentralBrancoOpaco()
    {
        (byte c, byte a) = Pixel(CursorSistema.GerarCurBarra(), 16, 16);
        Assert.Equal(255, a);
        Assert.Equal(255, c);
    }

    [Fact]
    public void Barra_ContornoEscuroEForaTransparente()
    {
        byte[] b = CursorSistema.GerarCurBarra();
        Assert.Equal((byte)0, Pixel(b, 0, 0).A);
        Assert.Equal((byte)0, Pixel(b, 16, 2).A);
        (byte c, byte a) = Pixel(b, 14, 16); // 1 px à esquerda da barra (x 15 e 16)
        Assert.Equal(255, a);
        Assert.Equal(0, c);
    }

    [Theory]
    [InlineData(8, 1)]
    [InlineData(28, 6)]
    public void Barra_Extremos(int altura, int largura)
    {
        byte[] b = CursorSistema.GerarCurBarra(altura, largura);
        Assert.Equal(new byte[] { 0, 0, 2, 0, 1, 0 }, b[..6]);
        Assert.Equal(16, BitConverter.ToUInt16(b, 10));
        Assert.Equal(16, BitConverter.ToUInt16(b, 12));
        (byte c, byte a) = Pixel(b, 16, 16);
        Assert.Equal(255, a);
        Assert.Equal(255, c);
        int opacos = 0;
        for (int y = 0; y < 32; y++)
        {
            (byte cy, byte ay) = Pixel(b, 16, y);
            if (ay == 255 && cy == 255)
            {
                opacos++;
            }
        }
        Assert.InRange(opacos, altura - 1, altura + 1);
    }
}
