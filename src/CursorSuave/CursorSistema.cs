using System;
using System.IO;

namespace CursorSuave;

public static class CursorSistema
{
    public const int Tamanho = 32;
    public const int Hotspot = 16;
    public const int DiametroPadrao = 6;

    /// <summary>Gera os bytes de um .cur 32x32 (32 bpp) com o ponto (diâmetro 4 a 12) no centro.</summary>
    public static byte[] GerarCur(int diametro = DiametroPadrao)
    {
        diametro = Math.Clamp(diametro, 4, 12);
        double raioBranco = diametro / 2.0;
        double raioContorno = raioBranco + 1; // contorno escuro de 1px
        const int w = Tamanho;
        const int bytesPixels = w * w * 4;
        const int bytesMascara = w * 4; // 32 bits por linha
        const int tamanhoDados = 40 + bytesPixels + bytesMascara * w;

        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        // ICONDIR
        bw.Write((ushort)0);
        bw.Write((ushort)2); // 2 = cursor
        bw.Write((ushort)1);
        // ICONDIRENTRY
        bw.Write((byte)w);
        bw.Write((byte)w);
        bw.Write((byte)0);
        bw.Write((byte)0);
        bw.Write((ushort)Hotspot); // hotspot X
        bw.Write((ushort)Hotspot); // hotspot Y
        bw.Write(tamanhoDados);
        bw.Write(22);              // offset dos dados
        // BITMAPINFOHEADER
        bw.Write(40);
        bw.Write(w);
        bw.Write(w * 2);           // altura dobrada (XOR + AND)
        bw.Write((ushort)1);
        bw.Write((ushort)32);
        bw.Write(0);
        bw.Write(bytesPixels + bytesMascara * w);
        bw.Write(0);
        bw.Write(0);
        bw.Write(0);
        bw.Write(0);

        // Pixels BGRA, de baixo para cima
        for (int y = w - 1; y >= 0; y--)
        {
            for (int x = 0; x < w; x++)
            {
                double dx = x + 0.5 - Hotspot;
                double dy = y + 0.5 - Hotspot;
                double d = Math.Sqrt(dx * dx + dy * dy);
                double aContorno = Math.Clamp(raioContorno + 0.5 - d, 0, 1);
                double aBranco = Math.Clamp(raioBranco + 0.5 - d, 0, 1);
                // Branco por cima do contorno escuro (preto = 0), cor pré-multiplicada.
                double cor = aContorno > 0 ? 255 * Math.Min(aBranco / aContorno, 1) : 0;
                byte a = (byte)Math.Round(aContorno * 255);
                byte c = (byte)Math.Round(cor * aContorno);
                bw.Write(c);
                bw.Write(c);
                bw.Write(c);
                bw.Write(a);
            }
        }

        // Máscara AND zerada (o alfa define a transparência)
        bw.Write(new byte[bytesMascara * w]);
        bw.Flush();
        return ms.ToArray();
    }

    private static string CaminhoCur => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CursorSuave", "ponto.cur");

    // "Ocultar ponteiro ao digitar": valor original guardado uma única vez.
    private static bool? _vanishOriginal;
    private static IntPtr _maoAplicada;
    private static IntPtr _maoArquivo;

    /// <summary>O cursor atual é a mão trocada pela bolinha (sobre um link)?</summary>
    public static bool EhMao(IntPtr hCursor)
    {
        if (hCursor == IntPtr.Zero)
        {
            return false;
        }
        IntPtr a = _maoAplicada;
        IntPtr b = _maoArquivo;
        return hCursor == a || hCursor == b;
    }

    /// <summary>Troca os cursores normal e mão pela bolinha.</summary>
    public static void Aplicar(int diametro = DiametroPadrao)
    {
        try
        {
            string caminho = CaminhoCur;
            Directory.CreateDirectory(Path.GetDirectoryName(caminho)!);
            File.WriteAllBytes(caminho, GerarCur(diametro));

            foreach (uint id in new[] { Nativo.OCR_NORMAL, Nativo.OCR_HAND })
            {
                // SetSystemCursor destrói o handle recebido: um handle novo para cada cursor.
                IntPtr h = Nativo.LoadCursorFromFile(caminho);
                if (id == Nativo.OCR_HAND)
                {
                    _maoArquivo = h;
                }
                if (h != IntPtr.Zero)
                {
                    Nativo.SetSystemCursor(h, id);
                }
            }

            _maoAplicada = Nativo.LoadCursor(IntPtr.Zero, (IntPtr)Nativo.IDC_HAND);

            DesligarOcultarAoDigitar();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Sem permissão de escrita: mantém o cursor original.
        }
    }

    private static void DesligarOcultarAoDigitar()
    {
        if (_vanishOriginal is null
            && Nativo.SystemParametersInfoLerInt(Nativo.SPI_GETMOUSEVANISH, 0, out int atual, 0))
        {
            _vanishOriginal = atual != 0;
        }
        // fWinIni = 0: vale só na sessão, não grava no perfil.
        Nativo.SystemParametersInfo(Nativo.SPI_SETMOUSEVANISH, 0, IntPtr.Zero, 0);
    }

    /// <summary>Recarrega os cursores originais do Windows.</summary>
    public static void Restaurar()
    {
        if (_vanishOriginal is bool original)
        {
            Nativo.SystemParametersInfo(Nativo.SPI_SETMOUSEVANISH, 0, original ? (IntPtr)1 : IntPtr.Zero, 0);
            _vanishOriginal = null;
        }
        _maoAplicada = IntPtr.Zero;
        _maoArquivo = IntPtr.Zero;
        Nativo.SystemParametersInfo(Nativo.SPI_SETCURSORS, 0, IntPtr.Zero, 0);
    }
}
