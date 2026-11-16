using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;

namespace CursorSuave;

/// <summary>
/// Anel que segue o mouse: janela Win32 layered criada numa thread própria,
/// atualizada com UpdateLayeredWindow a cada refresh do DWM, com desenho subpixel.
/// </summary>
public sealed class AnelNativo : IDisposable
{
    private const double MargemJanela = 30;   // janela = anel + 30 lógicos (56 com o anel de 26)
    private const double Borda = 2;

    private readonly Seguidor _seguidor = new();
    private readonly EstadoAnel _estado = new();
    private readonly Thread _thread;
    private readonly ManualResetEventSlim _pronto = new(false);
    private readonly ManualResetEventSlim _ligadoEvento = new(false);
    private readonly Win32.WndProc _wndProc = Win32.DefWindowProc; // mantido vivo (evita GC do delegate)
    private volatile bool _desejaVisivel;
    private volatile bool _sair;
    private volatile bool _reiniciar;
    // Objeto imutável trocado por referência; a thread do anel só lê.
    private volatile Configuracao _config = new Configuracao();
    private bool _disposto;
    private IntPtr _hwnd;
    private string? _classe;

    public AnelNativo()
    {
        _thread = new Thread(Executar) { IsBackground = true, Name = "AnelNativo" };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
        _pronto.Wait();
    }

    /// <summary>Entrega uma nova configuração (imutável) à thread do anel.</summary>
    public void Aplicar(Configuracao config)
    {
        _config = config;
    }

    public void Mostrar()
    {
        _reiniciar = true;
        _desejaVisivel = true;
        _ligadoEvento.Set();
    }

    public void Ocultar()
    {
        _desejaVisivel = false;
        _ligadoEvento.Set();
    }

    public void Dispose()
    {
        if (_disposto)
        {
            return;
        }
        _disposto = true;
        _sair = true;
        _ligadoEvento.Set();
        _thread.Join();
        _pronto.Dispose();
        _ligadoEvento.Dispose();
    }

    private void Executar()
    {
        IntPtr hInstance = Win32.GetModuleHandle(null);
        _classe = "CursorSuaveAnel_" + Guid.NewGuid().ToString("N");
        var wc = new Win32.WNDCLASSEX
        {
            cbSize = Marshal.SizeOf<Win32.WNDCLASSEX>(),
            lpfnWndProc = _wndProc,
            hInstance = hInstance,
            lpszClassName = _classe
        };
        ushort atom = Win32.RegisterClassEx(ref wc);
        if (atom != 0)
        {
            _hwnd = Win32.CreateWindowEx(
                (uint)(Nativo.WS_EX_LAYERED | Nativo.WS_EX_TRANSPARENT | Nativo.WS_EX_TOOLWINDOW |
                       Nativo.WS_EX_NOACTIVATE | Win32.WS_EX_TOPMOST),
                _classe, "CursorSuaveAnel", Win32.WS_POPUP, 0, 0, 1, 1,
                IntPtr.Zero, IntPtr.Zero, hInstance, IntPtr.Zero);
        }
        _pronto.Set();

        try
        {
            if (_hwnd != IntPtr.Zero)
            {
                Laco();
            }
        }
        finally
        {
            if (_hwnd != IntPtr.Zero)
            {
                Win32.DestroyWindow(_hwnd);
                _hwnd = IntPtr.Zero;
            }
            if (atom != 0)
            {
                Win32.UnregisterClass(_classe, hInstance);
            }
        }
    }

    private void Laco()
    {
        IntPtr hdcTela = Win32.GetDC(IntPtr.Zero);
        IntPtr hdcMem = Win32.CreateCompatibleDC(hdcTela);
        IntPtr dib = IntPtr.Zero;
        IntPtr antigo = IntPtr.Zero;
        Bitmap? bmp = null;
        Graphics? g = null;
        int tamanho = 0;
        double escalaAtual = 0;
        int tamanhoLogicoAtual = 0;
        Configuracao? configAplicada = null;
        byte corR = 255, corG = 255, corB = 255;
        double opacidadeParadoBolinha = 1;
        bool visivel = false;
        bool primeiro = true;
        double ultimoX = double.NaN, ultimoY = double.NaN;
        var relogio = Stopwatch.StartNew();
        double ultimo = 0;
        long ultimoTopmost = 0;
        double ultimoDiametro = double.NaN, ultimoPreench = double.NaN, ultimoBorda = double.NaN;
        int ultimoOx = 0, ultimoOy = 0;
        int ultimoAlpha = -1;
        int ultimoPx = int.MinValue, ultimoPy = int.MinValue;

        try
        {
            while (!_sair)
            {
                Bombear();

                if (!_desejaVisivel)
                {
                    if (visivel)
                    {
                        Win32.ShowWindow(_hwnd, Win32.SW_HIDE);
                        visivel = false;
                        ultimoAlpha = -1;
                    }
                    _ligadoEvento.Reset();
                    if (!_desejaVisivel && !_sair)
                    {
                        _ligadoEvento.Wait(100);
                    }
                    continue;
                }

                if (_reiniciar)
                {
                    _reiniciar = false;
                    primeiro = true;
                }

                if (!Nativo.GetCursorPos(out Nativo.POINT p))
                {
                    Esperar();
                    continue;
                }

                double agora = relogio.Elapsed.TotalSeconds;
                double dt = primeiro ? 0 : Math.Min(agora - ultimo, 0.25);
                ultimo = agora;

                Configuracao cfg = _config;
                if (!ReferenceEquals(cfg, configAplicada))
                {
                    configAplicada = cfg;
                    _estado.Configurar(cfg.TamanhoAnel, cfg.SegundosParado, cfg.OpacidadeParado, cfg.SumirParado,
                        cfg.Opacidade, cfg.OpacidadeEmLinks);
                    _seguidor.AtrasoSegundos = cfg.AtrasoMs / 1000.0;
                    _seguidor.ManterDentro = cfg.ManterBolinhaDentro;
                    Configuracao.TentarLerCor(cfg.Cor, out corR, out corG, out corB);
                    opacidadeParadoBolinha = cfg.VisualBolinha().OpacidadeParado;
                    ultimoX = double.NaN; // força o redesenho
                }

                double escala = EscalaDoMonitor(p);
                // GetCursorInfo exige cbSize; se falhar, assume cursor visível e fora de link.
                var info = new Nativo.CURSORINFO { cbSize = Marshal.SizeOf<Nativo.CURSORINFO>() };
                bool cursorVisivel = true;
                bool sobreLink = false;
                if (Nativo.GetCursorInfo(ref info))
                {
                    cursorVisivel = (info.flags & Nativo.CURSOR_SHOWING) != 0;
                    sobreLink = CursorSistema.EhMao(info.hCursor);
                }
                bool pressionado = cfg.EfeitoClique &&
                                   (Nativo.GetAsyncKeyState(Nativo.VK_LBUTTON) < 0 ||
                                    Nativo.GetAsyncKeyState(Nativo.VK_RBUTTON) < 0);
                bool mexeu = p.X != ultimoPx || p.Y != ultimoPy;
                ultimoPx = p.X;
                ultimoPy = p.Y;

                if (primeiro)
                {
                    _estado.Reiniciar();
                }
                _estado.Atualizar(dt, mexeu && !primeiro, pressionado, sobreLink, cursorVisivel, cfg.FundoEmLinks);
                // Bolinha e barra apagam em degraus quando o mouse fica parado (só troca se o degrau mudou).
                int degrauBolinha = cfg.SumirParado
                    ? NivelBolinha.Calcular(_estado.SegundosSemMexer, cfg.SegundosParado, opacidadeParadoBolinha).Degrau
                    : 0;
                CursorSistema.DefinirNivel(degrauBolinha);
                double diametro = _estado.Diametro * _estado.EscalaClique;
                // Trava pelo raio atual do anel e da bolinha.
                double limite = Math.Max(0, diametro / 2 - cfg.TamanhoBolinha / 2.0 - Borda) * escala;

                if (primeiro)
                {
                    _seguidor.Reiniciar(p.X, p.Y);
                    ultimoX = double.NaN;
                }
                else
                {
                    _seguidor.Atualizar(p.X, p.Y, dt, limite);
                }

                int tamanhoLogico = (int)Math.Ceiling(cfg.TamanhoAnel + MargemJanela);
                if (escala != escalaAtual || tamanhoLogico != tamanhoLogicoAtual || dib == IntPtr.Zero)
                {
                    LiberarBitmap(hdcMem, ref dib, ref antigo, ref bmp, ref g);
                    tamanho = (int)Math.Ceiling(tamanhoLogico * escala);
                    dib = CriarDib(hdcMem, tamanho, out IntPtr bits);
                    if (dib == IntPtr.Zero)
                    {
                        escalaAtual = 0;
                        Esperar();
                        continue;
                    }
                    antigo = Win32.SelectObject(hdcMem, dib);
                    bmp = new Bitmap(tamanho, tamanho, tamanho * 4, PixelFormat.Format32bppPArgb, bits);
                    g = Graphics.FromImage(bmp);
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    escalaAtual = escala;
                    tamanhoLogicoAtual = tamanhoLogico;
                    ultimoX = double.NaN;
                }

                // Só redesenha a imagem quando posição, diâmetro ou preenchimento mudam; opacidade não redesenha.
                bool redesenhar = double.IsNaN(ultimoX) ||
                                  Math.Abs(_seguidor.X - ultimoX) > 0.01 ||
                                  Math.Abs(_seguidor.Y - ultimoY) > 0.01 ||
                                  double.IsNaN(ultimoDiametro) ||
                                  Math.Abs(diametro - ultimoDiametro) > 0.02 ||
                                  Math.Abs(_estado.Preenchimento - ultimoPreench) > 0.005 ||
                                  double.IsNaN(ultimoBorda) ||
                                  Math.Abs(_estado.OpacidadeBorda - ultimoBorda) > 0.002;
                int alpha = (int)Math.Round(_estado.Opacidade * 255);
                if (redesenhar)
                {
                    int metade = tamanho / 2;
                    (ultimoOx, double fx) = Subpixel.Dividir(_seguidor.X, metade);
                    (ultimoOy, double fy) = Subpixel.Dividir(_seguidor.Y, metade);
                    Desenhar(g!, metade + fx, metade + fy, escala, diametro, _estado.Preenchimento, corR, corG, corB, _estado.OpacidadeBorda);
                    ultimoX = _seguidor.X;
                    ultimoY = _seguidor.Y;
                    ultimoDiametro = diametro;
                    ultimoPreench = _estado.Preenchimento;
                    ultimoBorda = _estado.OpacidadeBorda;
                }
                if (redesenhar || alpha != ultimoAlpha)
                {
                    var pos = new Win32.POINT { X = ultimoOx, Y = ultimoOy };
                    var tam = new Win32.SIZE { cx = tamanho, cy = tamanho };
                    var zero = new Win32.POINT();
                    var blend = new Win32.BLENDFUNCTION
                    {
                        BlendOp = Win32.AC_SRC_OVER,
                        BlendFlags = 0,
                        SourceConstantAlpha = (byte)alpha,
                        AlphaFormat = Win32.AC_SRC_ALPHA
                    };
                    Win32.UpdateLayeredWindow(_hwnd, hdcTela, ref pos, ref tam, hdcMem, ref zero, 0, ref blend, Win32.ULW_ALPHA);
                    ultimoAlpha = alpha;
                }

                if (!visivel)
                {
                    Win32.ShowWindow(_hwnd, Win32.SW_SHOWNOACTIVATE);
                    visivel = true;
                    ultimoTopmost = Environment.TickCount64;
                }
                else if (Environment.TickCount64 - ultimoTopmost >= 1000)
                {
                    Win32.SetWindowPos(_hwnd, Win32.HWND_TOPMOST, 0, 0, 0, 0,
                        Nativo.SWP_NOSIZE | Nativo.SWP_NOACTIVATE | Win32.SWP_NOMOVE);
                    ultimoTopmost = Environment.TickCount64;
                }

                primeiro = false;
                Esperar();
            }
        }
        finally
        {
            LiberarBitmap(hdcMem, ref dib, ref antigo, ref bmp, ref g);
            Win32.DeleteDC(hdcMem);
            Win32.ReleaseDC(IntPtr.Zero, hdcTela);
        }
    }

    private static void Esperar()
    {
        if (Win32.DwmFlush() != 0)
        {
            Thread.Sleep(1);
        }
    }

    private static void Bombear()
    {
        while (Win32.PeekMessage(out Win32.MSG msg, IntPtr.Zero, 0, 0, Win32.PM_REMOVE))
        {
            Win32.TranslateMessage(ref msg);
            Win32.DispatchMessage(ref msg);
        }
    }

    private static void Desenhar(Graphics g, double cx, double cy, double escala, double diametro, double preenchimento,
        byte r, byte gc, byte b, double opacidade)
    {
        g.Clear(Color.Transparent);

        // Elipse (diâmetro + 2) com traço 1 (#66000000) e elipse (diâmetro) com traço 2 (#99FFFFFF);
        // o traço fica por dentro das medidas, então o raio da linha central é (diâmetro - traço) / 2.
        using var contorno = new Pen(Color.FromArgb(0x66, 0, 0, 0), (float)(1 * escala));
        double r1 = (diametro + 2 - 1) / 2.0 * escala;
        g.DrawEllipse(contorno, (float)(cx - r1), (float)(cy - r1), (float)(2 * r1), (float)(2 * r1));

        double r2 = (diametro - 2) / 2.0 * escala;
        int alfaPreench = (int)Math.Round(0x1A * Math.Clamp(preenchimento, 0, 1)); // branco a 10%
        if (alfaPreench > 0)
        {
            using var fundo = new SolidBrush(Color.FromArgb(alfaPreench, r, gc, b));
            g.FillEllipse(fundo, (float)(cx - r2), (float)(cy - r2), (float)(2 * r2), (float)(2 * r2));
        }

        using var borda = new Pen(Color.FromArgb((int)Math.Round(Math.Clamp(opacidade, 0, 1) * 255), r, gc, b), (float)(2 * escala));
        g.DrawEllipse(borda, (float)(cx - r2), (float)(cy - r2), (float)(2 * r2), (float)(2 * r2));
    }

    private static IntPtr CriarDib(IntPtr hdc, int tamanho, out IntPtr bits)
    {
        var bi = new Win32.BITMAPINFO
        {
            bmiHeader = new Win32.BITMAPINFOHEADER
            {
                biSize = Marshal.SizeOf<Win32.BITMAPINFOHEADER>(),
                biWidth = tamanho,
                biHeight = -tamanho, // top-down
                biPlanes = 1,
                biBitCount = 32,
                biCompression = 0
            }
        };
        return Win32.CreateDIBSection(hdc, ref bi, 0, out bits, IntPtr.Zero, 0);
    }

    private static void LiberarBitmap(IntPtr hdcMem, ref IntPtr dib, ref IntPtr antigo, ref Bitmap? bmp, ref Graphics? g)
    {
        g?.Dispose();
        g = null;
        bmp?.Dispose();
        bmp = null;
        if (antigo != IntPtr.Zero)
        {
            Win32.SelectObject(hdcMem, antigo);
            antigo = IntPtr.Zero;
        }
        if (dib != IntPtr.Zero)
        {
            Win32.DeleteObject(dib);
            dib = IntPtr.Zero;
        }
    }

    private static double EscalaDoMonitor(Nativo.POINT p)
    {
        IntPtr monitor = Nativo.MonitorFromPoint(p, Nativo.MONITOR_DEFAULTTONEAREST);
        if (monitor != IntPtr.Zero &&
            Nativo.GetDpiForMonitor(monitor, Nativo.MDT_EFFECTIVE_DPI, out uint dpiX, out _) == 0 &&
            dpiX > 0)
        {
            return dpiX / 96.0;
        }
        return 1.0;
    }

    private static class Win32
    {
        public const uint WS_POPUP = 0x80000000;
        public const uint WS_EX_TOPMOST = 0x8;
        public const int SW_HIDE = 0;
        public const int SW_SHOWNOACTIVATE = 4;
        public const uint SWP_NOMOVE = 0x0002;
        public const uint PM_REMOVE = 1;
        public const uint ULW_ALPHA = 2;
        public const byte AC_SRC_OVER = 0;
        public const byte AC_SRC_ALPHA = 1;
        public static readonly IntPtr HWND_TOPMOST = new(-1);

        public delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct WNDCLASSEX
        {
            public int cbSize;
            public uint style;
            public WndProc lpfnWndProc;
            public int cbClsExtra;
            public int cbWndExtra;
            public IntPtr hInstance;
            public IntPtr hIcon;
            public IntPtr hCursor;
            public IntPtr hbrBackground;
            public string? lpszMenuName;
            public string lpszClassName;
            public IntPtr hIconSm;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int X; public int Y; }

        [StructLayout(LayoutKind.Sequential)]
        public struct SIZE { public int cx; public int cy; }

        [StructLayout(LayoutKind.Sequential)]
        public struct BLENDFUNCTION
        {
            public byte BlendOp;
            public byte BlendFlags;
            public byte SourceConstantAlpha;
            public byte AlphaFormat;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MSG
        {
            public IntPtr hwnd;
            public uint message;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public POINT pt;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct BITMAPINFOHEADER
        {
            public int biSize;
            public int biWidth;
            public int biHeight;
            public short biPlanes;
            public short biBitCount;
            public int biCompression;
            public int biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public int biClrUsed;
            public int biClrImportant;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct BITMAPINFO
        {
            public BITMAPINFOHEADER bmiHeader;
            public int bmiColors;
        }

        [DllImport("user32.dll", EntryPoint = "DefWindowProcW")]
        public static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "RegisterClassExW", SetLastError = true)]
        public static extern ushort RegisterClassEx(ref WNDCLASSEX wc);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "UnregisterClassW")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnregisterClass(string className, IntPtr hInstance);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "CreateWindowExW", SetLastError = true)]
        public static extern IntPtr CreateWindowEx(uint exStyle, string className, string windowName, uint style,
            int x, int y, int w, int h, IntPtr parent, IntPtr menu, IntPtr hInstance, IntPtr param);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DestroyWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ShowWindow(IntPtr hWnd, int cmd);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);

        [DllImport("user32.dll", EntryPoint = "PeekMessageW")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool PeekMessage(out MSG msg, IntPtr hWnd, uint min, uint max, uint remove);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool TranslateMessage(ref MSG msg);

        [DllImport("user32.dll", EntryPoint = "DispatchMessageW")]
        public static extern IntPtr DispatchMessage(ref MSG msg);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UpdateLayeredWindow(IntPtr hWnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize,
            IntPtr hdcSrc, ref POINT pptSrc, uint crKey, ref BLENDFUNCTION pblend, uint flags);

        [DllImport("user32.dll")]
        public static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern int ReleaseDC(IntPtr hWnd, IntPtr hdc);

        [DllImport("gdi32.dll")]
        public static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        public static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);

        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteObject(IntPtr obj);

        [DllImport("gdi32.dll", SetLastError = true)]
        public static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFO bmi, uint usage, out IntPtr bits,
            IntPtr section, uint offset);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetModuleHandleW")]
        public static extern IntPtr GetModuleHandle(string? name);

        [DllImport("dwmapi.dll")]
        public static extern int DwmFlush();
    }
}
