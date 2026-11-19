using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using System.Windows;
using ImageSource = System.Windows.Media.ImageSource;
using System.Windows.Media.Imaging;
using Imaging = System.Windows.Interop.Imaging;
using System.Windows.Threading;

namespace CursorSuave;

public partial class App : System.Windows.Application
{
    private Mutex? _mutex;
    private bool _segundaInstancia;
    private AnelNativo? _anel;
    private NotifyIcon? _bandeja;
    private Icon? _icone;
    private DispatcherTimer? _timer;
    private bool _ativo = true;
    private MenuBandeja? _menu;
    private long _menuFechadoEm;
    private ImageSource? _iconeWpf;
    private bool _mostrando;
    private MotivoSuspensao _motivo = MotivoSuspensao.Nenhum;
    private JanelaConfiguracoes? _janelaConfig;
    private Configuracao _config = new Configuracao(); // só a thread da interface
    private Configuracao _cursorAplicado = new Configuracao(); // última config usada em CursorSistema.Aplicar
    private readonly Atualizador _atualizador = new Atualizador();
    private DispatcherTimer? _timerAtualizacao;
    private readonly ControleAtualizacao _controle;
    private bool _balaoEhAtualizacao;

    public App()
    {
        _controle = new ControleAtualizacao(_atualizador, () =>
        {
            // Restaura o cursor (e apaga sessao.json) antes de reiniciar, para o guardião ver fechamento limpo.
            _anel?.Ocultar();
            CursorSistema.Restaurar();
        });
    }

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);

        _mutex = new Mutex(true, @"Local\CursorSuave.Instancia", out bool primeira);
        if (!primeira)
        {
            _segundaInstancia = true;
            Shutdown();
            return;
        }

        DispatcherUnhandledException += (_, _) => CursorSistema.Restaurar();
        AppDomain.CurrentDomain.UnhandledException += (_, _) => CursorSistema.Restaurar();
        SessionEnding += (_, _) => CursorSistema.Restaurar();
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            if (!_segundaInstancia)
            {
                CursorSistema.Restaurar();
            }
        };

        // Rede de segurança: sessão de um processo que morreu à força (e o guardião também não rodou).
        string caminhoSessao = Sessao.CaminhoPadrao;
        Sessao? anterior = Sessao.Ler(caminhoSessao);
        bool existe = Sessao.Existe(caminhoSessao);
        if (Sessao.PrecisaRestaurar(existe, anterior is not null && Sessao.PidRodando(anterior.Pid)))
        {
            Guardiao.RestaurarSistema(anterior?.VanishOriginal);
        }
        // Cobre também um travamento de versões sem sessao.json.
        CursorSistema.Restaurar();
        Guardiao.Iniciar();

        _config = Configuracao.CarregarDoDisco();
        _anel = new AnelNativo();
        _anel.Aplicar(_config);

        CriarBandeja();
        IniciarAtualizacoes();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _timer.Tick += (_, _) =>
        {
            (string classe, string processo) = JanelaAtiva.Ler();
            Configuracao cfg = _config;
            bool pausar = cfg.PausarTelaCheia;
            bool telaCheia = pausar && TelaCheia.JanelaEmPrimeiroPlanoEhTelaCheia();
            _motivo = Suspensao.Decidir(classe, processo, telaCheia, pausar, cfg.ProgramasPausados);
            Atualizar();
        };
        _timer.Start();

        Atualizar();
    }

    private void CriarBandeja()
    {
        _icone = CriarIcone();
        // Convertido uma vez: serve de ícone das Configurações e do cabeçalho do menu.
        BitmapSource fonte = Imaging.CreateBitmapSourceFromHIcon(_icone.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
        fonte.Freeze();
        _iconeWpf = fonte;

        // O NotifyIcon continua só para o ícone e os balões; o menu é a janela MenuBandeja.
        _bandeja = new NotifyIcon
        {
            Icon = _icone,
            Text = "Cursor Suave",
            Visible = true
        };
        _bandeja.MouseUp += (_, e) =>
        {
            if (e.Button is MouseButtons.Left or MouseButtons.Right)
            {
                AbrirMenu();
            }
        };
        _bandeja.BalloonTipClicked += (_, _) =>
        {
            if (_balaoEhAtualizacao)
            {
                AbrirConfiguracoes(iniciarAtualizacao: true);
            }
        };
    }

    private void AbrirMenu()
    {
        // POR QUÊ: clicar no ícone com o menu aberto tira o foco (fecha o menu) e o MouseUp reabriria;
        // ignorar aberturas logo após um fechamento faz o segundo clique funcionar como "fechar".
        if (_menu is not null || Environment.TickCount64 - _menuFechadoEm < 300 || _iconeWpf is null)
        {
            return;
        }
        var dados = new DadosMenu(
            _iconeWpf, _atualizador.VersaoAtual, _ativo, _config.PausarTelaCheia, LerInicializacao(),
            _controle.Situacao,
            ativo => { _ativo = ativo; Atualizar(); },
            pausar =>
            {
                AplicarConfiguracao(_config with { PausarTelaCheia = pausar });
                _config.SalvarNoDisco();
            },
            DefinirInicializacao,
            () => AbrirConfiguracoes(),
            async () => await _controle.VerificarAsync(true),
            () => AbrirConfiguracoes(iniciarAtualizacao: true),
            () =>
            {
                // Desligar "Ativo" restaura o cursor via Atualizar().
                if (_ativo)
                {
                    _ativo = false;
                    Atualizar();
                }
                else
                {
                    CursorSistema.Restaurar();
                }
            },
            Encerrar);
        var menu = new MenuBandeja(dados);
        menu.Closed += (_, _) =>
        {
            _menu = null;
            _menuFechadoEm = Environment.TickCount64;
        };
        _menu = menu;

        _ = Nativo.GetCursorPos(out Nativo.POINT p);
        IntPtr monitor = Nativo.MonitorFromPoint(p, Nativo.MONITOR_DEFAULTTONEAREST);
        var info = new Nativo.MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<Nativo.MONITORINFO>() };
        AreaPx area = Nativo.GetMonitorInfo(monitor, ref info)
            ? new AreaPx(info.rcWork.Left, info.rcWork.Top, info.rcWork.Right, info.rcWork.Bottom)
            : new AreaPx(p.X - 2000, p.Y - 2000, p.X + 2000, p.Y + 2000);
        double escala = Nativo.GetDpiForMonitor(monitor, Nativo.MDT_EFFECTIVE_DPI, out uint dpiX, out _) == 0 ? dpiX / 96.0 : 1.0;
        menu.MostrarEm(p.X, p.Y, area, escala);
    }

    private void IniciarAtualizacoes()
    {
        _controle.Verificado += (r, manual) =>
        {
            switch (r)
            {
                case ResultadoVerificacao.NovaVersao:
                    // Com as Configurações abertas o cartão Sistema já mostra a versão nova.
                    if (_janelaConfig is null)
                    {
                        Balao($"Cursor Suave {_controle.Situacao.VersaoNova} disponível. Clique para atualizar.", true);
                    }
                    break;
                case ResultadoVerificacao.SemNovidade when manual:
                    Balao("Você já está na versão mais recente.", false);
                    break;
                case ResultadoVerificacao.Erro when manual:
                    Balao("Não foi possível verificar: " + _controle.Situacao.Erro, false);
                    break;
            }
        };
        if (!_atualizador.Instalado)
        {
            return;
        }
        // Primeira verificação 30 s depois de abrir; depois a cada 6 h.
        _timerAtualizacao = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _timerAtualizacao.Tick += async (_, _) =>
        {
            _timerAtualizacao.Interval = Atualizador.Intervalo;
            if (Atualizador.DeveVerificar(DateTime.UtcNow, _controle.UltimaVerificacao, _config.VerificarAtualizacoes))
            {
                await _controle.VerificarAsync(false);
            }
        };
        _timerAtualizacao.Start();
    }

    private void Balao(string texto, bool ehAtualizacao)
    {
        if (_bandeja is null)
        {
            return;
        }
        _balaoEhAtualizacao = ehAtualizacao;
        _bandeja.ShowBalloonTip(5000, "Cursor Suave", texto, ToolTipIcon.Info);
    }

    private static Icon CriarIcone()
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var contorno = new Pen(Color.FromArgb(160, 0, 0, 0), 1);
            g.DrawEllipse(contorno, 2, 2, 27, 27);
            using var pena = new Pen(Color.White, 3);
            g.DrawEllipse(pena, 4, 4, 23, 23);
            g.FillEllipse(Brushes.White, 13, 13, 6, 6);
        }
        IntPtr h = bmp.GetHicon();
        try
        {
            using Icon temporario = Icon.FromHandle(h);
            return (Icon)temporario.Clone();
        }
        finally
        {
            Nativo.DestroyIcon(h);
        }
    }

    private void Atualizar()
    {
        if (_anel is null)
        {
            return;
        }

        bool deve = _ativo && _motivo == MotivoSuspensao.Nenhum;
        if (deve == _mostrando)
        {
            return;
        }

        _mostrando = deve;
        if (deve)
        {
            CursorSistema.Aplicar(_config);
            _cursorAplicado = _config;
            _anel.Mostrar();
        }
        else
        {
            _anel.Ocultar();
            CursorSistema.Restaurar();
        }
    }


    /// <summary>Passa a configuração (imutável) ao anel e reaplica a bolinha se o tamanho mudou.</summary>
    private void AplicarConfiguracao(Configuracao nova)
    {
        _config = nova;
        _anel?.Aplicar(nova);

        if (_mostrando && CursorMudou(_cursorAplicado, nova))
        {
            if (nova.BarraEmTexto != _cursorAplicado.BarraEmTexto && !nova.BarraEmTexto)
            {
                // SPI_SETCURSORS devolve a I-beam original; depois reaplica só o que está ligado.
                CursorSistema.Restaurar();
            }
            CursorSistema.Aplicar(nova);
            _cursorAplicado = nova;
        }
    }

    /// <summary>Algo que muda os arquivos dos cursores (tamanho, barra, cor ou opacidades efetivas)?</summary>
    private static bool CursorMudou(Configuracao a, Configuracao b) =>
        a.TamanhoBolinha != b.TamanhoBolinha || a.BarraEmTexto != b.BarraEmTexto
        || a.MarcacaoCarregando != b.MarcacaoCarregando
        || a.AlturaBarra != b.AlturaBarra || a.LarguraBarra != b.LarguraBarra
        || a.VisualBolinha() != b.VisualBolinha();

    private void AbrirConfiguracoes(bool iniciarAtualizacao = false)
    {
        if (_janelaConfig is not null)
        {
            if (_janelaConfig.WindowState == System.Windows.WindowState.Minimized)
            {
                _janelaConfig.WindowState = System.Windows.WindowState.Normal;
            }
            _janelaConfig.Activate();
            if (iniciarAtualizacao)
            {
                _janelaConfig.IniciarAtualizacao();
            }
            return;
        }

        var janela = new JanelaConfiguracoes(_config, LerInicializacao(),
            AplicarConfiguracao,
            (cfg, iniciar) =>
            {
                AplicarConfiguracao(cfg);
                cfg.SalvarNoDisco();
                DefinirInicializacao(iniciar);
            }, _atualizador.VersaoAtual,
            () => _anel?.MostrarPreviaCarregando(), _controle, _iconeWpf);
        janela.Closed += (_, _) => _janelaConfig = null;
        _janelaConfig = janela;
        janela.Show();
        janela.Activate();
        if (iniciarAtualizacao)
        {
            // Depois do Show: a caixa de confirmação precisa da janela já aberta como dona.
            janela.IniciarAtualizacao();
        }
    }

    private static bool LerInicializacao()
    {
        try
        {
            return Inicializacao.Ativo();
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or System.IO.IOException)
        {
            return false;
        }
    }

    private void DefinirInicializacao(bool ativo)
    {
        try
        {
            string? exe = Environment.ProcessPath;
            if (exe is not null)
            {
                Inicializacao.Definir(ativo, exe);
            }
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or System.IO.IOException)
        {
            // Sem acesso ao registro: o estado real continua o que está lá.
        }
    }

    private void Encerrar()
    {
        _timer?.Stop();
        _timerAtualizacao?.Stop();
        _menu?.Close();
        _janelaConfig?.Close();
        _anel?.Ocultar();
        _anel?.Dispose();
        _anel = null;
        CursorSistema.Restaurar();
        if (_bandeja is not null)
        {
            _bandeja.Visible = false;
            _bandeja.Dispose();
            _bandeja = null;
        }
        _icone?.Dispose();
        Shutdown();
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        if (!_segundaInstancia)
        {
            CursorSistema.Restaurar();
            try { _mutex?.ReleaseMutex(); } catch (ApplicationException) { }
        }
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
