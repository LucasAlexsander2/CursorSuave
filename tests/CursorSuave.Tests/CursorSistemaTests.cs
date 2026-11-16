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
