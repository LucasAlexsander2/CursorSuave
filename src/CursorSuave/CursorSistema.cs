using System;
using System.IO;

namespace CursorSuave;

public static class CursorSistema
{
    public const int Tamanho = 32;
    public const int Hotspot = 16;
    public const int DiametroPadrao = 6;

    private static (byte R, byte G, byte B) LerCor(string? cor)
    {
        if (!Configuracao.TentarLerCor(cor, out byte r, out byte g, out byte b))
        {
            r = g = b = 255;
        }
        return (r, g, b);
    }

    private static double LimitarOpacidade(double o) => double.IsNaN(o) ? 1 : Math.Clamp(o, 0, 1);

    /// <summary>Gera os bytes de um .cur 32x32 (32 bpp) com o ponto (diâmetro 4 a 12) no centro.
    /// O miolo tem a cor pedida, o contorno é escuro, e a opacidade multiplica o alfa (pré-multiplicado).</summary>
    public static byte[] GerarCur(int diametro = DiametroPadrao, double opacidade = 1, string cor = Configuracao.CorPadrao)
    {
        diametro = Math.Clamp(diametro, 4, 12);
        opacidade = LimitarOpacidade(opacidade);
        (byte cr, byte cg, byte cb) = LerCor(cor);
        double raioBranco = diametro / 2.0;
        double raioContorno = raioBranco + 1; // contorno escuro de 1px
        return Montar((x, y) =>
        {
            double dx = x + 0.5 - Hotspot;
            double dy = y + 0.5 - Hotspot;
            double d = Math.Sqrt(dx * dx + dy * dy);
            double aContorno = Math.Clamp(raioContorno + 0.5 - d, 0, 1);
            double aBranco = Math.Clamp(raioBranco + 0.5 - d, 0, 1);
            // Miolo por cima do contorno escuro (preto = 0), cor pré-multiplicada.
            double f = aContorno > 0 ? Math.Min(aBranco / aContorno, 1) : 0;
            byte Canal(byte c) => (byte)Math.Round(c * f * aContorno * opacidade);
            return (Canal(cb), Canal(cg), Canal(cr), (byte)Math.Round(aContorno * 255 * opacidade));
        });
    }

    public const int BarraAlturaPadrao = 18;
    public const int BarraLarguraPadrao = 2;

    /// <summary>Gera um .cur 32x32 com uma barra vertical (contorno escuro de 1 px) centralizada no hotspot.</summary>
    public static byte[] GerarCurBarra(int alturaPx = BarraAlturaPadrao, int larguraPx = BarraLarguraPadrao,
        string cor = Configuracao.CorPadrao, double opacidade = 1)
    {
        alturaPx = Math.Clamp(alturaPx, 8, 28);
        larguraPx = Math.Clamp(larguraPx, 1, 6);
        opacidade = LimitarOpacidade(opacidade);
        (byte cr, byte cg, byte cb) = LerCor(cor);
        byte alfa = (byte)Math.Round(255 * opacidade);
        byte Canal(byte c) => (byte)Math.Round(c * opacidade);
        (byte B, byte G, byte R, byte A) miolo = (Canal(cb), Canal(cg), Canal(cr), alfa);
        (byte B, byte G, byte R, byte A) contorno = (0, 0, 0, alfa);
        int x0 = Hotspot - larguraPx / 2, x1 = x0 + larguraPx; // [x0, x1)
        int y0 = Hotspot - alturaPx / 2, y1 = y0 + alturaPx;   // [y0, y1)
        return Montar((x, y) =>
        {
            if (x >= x0 && x < x1 && y >= y0 && y < y1)
            {
                return miolo;
            }
            if (x >= x0 - 1 && x < x1 + 1 && y >= y0 - 1 && y < y1 + 1)
            {
                return contorno;
            }
            return (0, 0, 0, 0);
        });
    }

    /// <summary>Monta o .cur; o delegado devolve (B, G, R pré-multiplicados, alfa) do pixel (x, y), y de cima para baixo.</summary>
    private static byte[] Montar(Func<int, int, (byte B, byte G, byte R, byte A)> pixel)
    {
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
                (byte b, byte g, byte r, byte a) = pixel(x, y);
                bw.Write(b);
                bw.Write(g);
                bw.Write(r);
                bw.Write(a);
            }
        }

        // Máscara AND zerada (o alfa define a transparência)
        bw.Write(new byte[bytesMascara * w]);
        bw.Flush();
        return ms.ToArray();
    }

    private static string Pasta => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CursorSuave");

    private static string CaminhoArquivo(CursorDaBolinha tipo, int degrau) => Path.Combine(Pasta,
        tipo switch
        {
            CursorDaBolinha.Barra => degrau == 0 ? "barra.cur" : $"barra-n{degrau}.cur",
            CursorDaBolinha.Mao => $"ponto-mao-n{degrau}.cur",
            // Os de espera usam o arquivo da bolinha normal (um LoadCursorFromFile por cursor trocado).
            _ => degrau == 0 ? "ponto.cur" : $"ponto-n{degrau}.cur"
        });

    private static uint IdDoSistema(CursorDaBolinha tipo) => tipo switch
    {
        CursorDaBolinha.Barra => Nativo.OCR_IBEAM,
        CursorDaBolinha.Mao => Nativo.OCR_HAND,
        CursorDaBolinha.Espera => Nativo.OCR_WAIT,
        CursorDaBolinha.EsperaSegundoPlano => Nativo.OCR_APPSTARTING,
        _ => Nativo.OCR_NORMAL
    };

    // "Ocultar ponteiro ao digitar": valor original guardado uma única vez.
    private static bool? _vanishOriginal;
    private static volatile IntPtr _maoAplicada;
    private static volatile IntPtr _maoArquivo;
    // Handles dos cursores de espera (ampulheta e seta com círculo): do sistema e dos arquivos trocados.
    private static volatile IntPtr _esperaAplicada;
    private static volatile IntPtr _esperaArquivo;
    private static volatile IntPtr _segPlanoAplicada;
    private static volatile IntPtr _segPlanoArquivo;

    // Estado das trocas: lido/escrito com _trava (thread do anel e thread da interface).
    private static readonly object _trava = new();
    private static bool _aplicado;
    private static volatile int _nivel;
    private static bool _barraAtiva;
    private static bool _marcacaoAtiva;
    private static int _maxDegrau;

    /// <summary>O cursor atual é a ampulheta ou a seta com círculo (já trocados pela bolinha)?</summary>
    public static bool EhCarregando(IntPtr hCursor)
    {
        if (hCursor == IntPtr.Zero)
        {
            return false;
        }
        return hCursor == _esperaAplicada || hCursor == _esperaArquivo
            || hCursor == _segPlanoAplicada || hCursor == _segPlanoArquivo;
    }

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

    /// <summary>Troca os cursores normal e mão pela bolinha (e a I-beam pela barra, se pedido), no degrau atual.
    /// Cor e opacidades vêm de <see cref="Configuracao.VisualBolinha"/>.</summary>
    public static void Aplicar(Configuracao cfg)
    {
        VisualDaBolinha v = cfg.VisualBolinha();
        try
        {
            lock (_trava)
            {
                Directory.CreateDirectory(Pasta);
                _barraAtiva = cfg.BarraEmTexto;
                // Marcação desligada depois de ligada: recarrega os originais (a ampulheta de volta); TrocarCursores reaplica o resto.
                if (_aplicado && _marcacaoAtiva && !cfg.MarcacaoCarregando)
                {
                    Nativo.SystemParametersInfo(Nativo.SPI_SETCURSORS, 0, IntPtr.Zero, 0);
                }
                _marcacaoAtiva = cfg.MarcacaoCarregando;
                // Sem apagar quando parado (opacidade 1): só existe o degrau 0.
                _maxDegrau = v.OpacidadeParado >= 1 ? 0 : NivelBolinha.Degraus;
                if (_nivel > _maxDegrau)
                {
                    _nivel = _maxDegrau;
                }
                for (int n = 0; n <= _maxDegrau; n++)
                {
                    double fator = NivelBolinha.FatorDoDegrau(n, v.OpacidadeParado);
                    File.WriteAllBytes(CaminhoArquivo(CursorDaBolinha.Normal, n),
                        GerarCur(cfg.TamanhoBolinha, v.Opacidade * fator, v.Cor));
                    File.WriteAllBytes(CaminhoArquivo(CursorDaBolinha.Mao, n),
                        GerarCur(cfg.TamanhoBolinha, v.OpacidadeEmLinks * fator, v.Cor));
                    if (cfg.BarraEmTexto)
                    {
                        File.WriteAllBytes(CaminhoArquivo(CursorDaBolinha.Barra, n),
                            GerarCurBarra(cfg.AlturaBarra, cfg.LarguraBarra, v.Cor, v.Opacidade * fator));
                    }
                }
                _aplicado = true;
                TrocarCursores(_nivel);
                DesligarOcultarAoDigitar();
                // Depois de DesligarOcultarAoDigitar, que guarda o valor original que o guardião devolve.
                Sessao.Gravar(Sessao.CaminhoPadrao, new Sessao(Environment.ProcessId, _vanishOriginal));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Sem permissão de escrita: mantém o cursor original.
        }
    }

    /// <summary>Carrega e instala os cursores do degrau (só com _trava). Um handle novo para cada
    /// cursor, porque o SetSystemCursor destrói o handle recebido.</summary>
    private static void TrocarCursores(int degrau)
    {
        // Sem a marcação, os handles de espera ficam zerados (EhCarregando falso).
        _esperaArquivo = _segPlanoArquivo = _esperaAplicada = _segPlanoAplicada = IntPtr.Zero;
        foreach (CursorDaBolinha tipo in NivelBolinha.CursoresDoDegrau(_barraAtiva, _marcacaoAtiva))
        {
            IntPtr h = Nativo.LoadCursorFromFile(CaminhoArquivo(tipo == CursorDaBolinha.Espera
                || tipo == CursorDaBolinha.EsperaSegundoPlano ? CursorDaBolinha.Normal : tipo, degrau));
            switch (tipo)
            {
                case CursorDaBolinha.Mao: _maoArquivo = h; break;
                case CursorDaBolinha.Espera: _esperaArquivo = h; break;
                case CursorDaBolinha.EsperaSegundoPlano: _segPlanoArquivo = h; break;
            }
            if (h != IntPtr.Zero)
            {
                Nativo.SetSystemCursor(h, IdDoSistema(tipo));
            }
        }
        _maoAplicada = Nativo.LoadCursor(IntPtr.Zero, (IntPtr)Nativo.IDC_HAND);
        if (_marcacaoAtiva)
        {
            _esperaAplicada = Nativo.LoadCursor(IntPtr.Zero, (IntPtr)Nativo.IDC_WAIT);
            _segPlanoAplicada = Nativo.LoadCursor(IntPtr.Zero, (IntPtr)Nativo.IDC_APPSTARTING);
        }
    }

    /// <summary>Muda para o degrau (0 a 4) de apagar. Não faz nada se o degrau não mudou ou se
    /// a bolinha não está aplicada (pausa, Restaurar). Chamado pela thread do anel.</summary>
    public static void DefinirNivel(int degrau)
    {
        if (degrau == _nivel)
        {
            return;
        }
        lock (_trava)
        {
            degrau = Math.Clamp(degrau, 0, _maxDegrau);
            if (!_aplicado || degrau == _nivel)
            {
                return;
            }
            _nivel = degrau;
            TrocarCursores(degrau);
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
        lock (_trava)
        {
            _aplicado = false;
            _nivel = 0;
            if (_vanishOriginal is bool original)
            {
                Nativo.SystemParametersInfo(Nativo.SPI_SETMOUSEVANISH, 0, original ? (IntPtr)1 : IntPtr.Zero, 0);
                _vanishOriginal = null;
            }
            _maoAplicada = IntPtr.Zero;
            _maoArquivo = IntPtr.Zero;
            _marcacaoAtiva = false;
            _esperaAplicada = _esperaArquivo = _segPlanoAplicada = _segPlanoArquivo = IntPtr.Zero;
            Nativo.SystemParametersInfo(Nativo.SPI_SETCURSORS, 0, IntPtr.Zero, 0);
            // Arquivo apagado = fechamento limpo; o guardião não faz nada.
            Sessao.Apagar(Sessao.CaminhoPadrao);
        }
    }
}
