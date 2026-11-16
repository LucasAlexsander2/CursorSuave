using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
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
    private ToolStripMenuItem? _itemAtivo;
    private ToolStripMenuItem? _itemPausar;
    private bool _mostrando;
    private MotivoSuspensao _motivo = MotivoSuspensao.Nenhum;
    private ToolStripMenuItem? _itemIniciar;
    private JanelaConfiguracoes? _janelaConfig;
    private Configuracao _config = new Configuracao(); // só a thread da interface
    private Configuracao _cursorAplicado = new Configuracao(); // última config usada em CursorSistema.Aplicar
    private bool _sincronizando;
    private readonly Atualizador _atualizador = new Atualizador();
    private DispatcherTimer? _timerAtualizacao;
    private ToolStripMenuItem? _itemVerificar;
    private ToolStripMenuItem? _itemAtualizar;
    private DateTime? _ultimaVerificacao;
    private bool _verificando;
    private bool _baixando;
    private bool _balaoEhAtualizacao;

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
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            if (!_segundaInstancia)
            {
                CursorSistema.Restaurar();
            }
        };

        // Cobre um travamento anterior que tenha deixado a bolinha no sistema.
        CursorSistema.Restaurar();

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
        var itemAtivo = new ToolStripMenuItem("Ativo") { CheckOnClick = true, Checked = true };
        itemAtivo.CheckedChanged += (_, _) => Atualizar();
        _itemAtivo = itemAtivo;

        var itemPausar = new ToolStripMenuItem("Pausar em tela cheia") { CheckOnClick = true, Checked = _config.PausarTelaCheia };
        itemPausar.CheckedChanged += (_, _) =>
        {
            if (!_sincronizando)
            {
                AplicarConfiguracao(_config with { PausarTelaCheia = itemPausar.Checked });
                _config.SalvarNoDisco();
            }
        };
        _itemPausar = itemPausar;

        var itemIniciar = new ToolStripMenuItem("Iniciar com o Windows") { CheckOnClick = true };
        itemIniciar.Click += (_, _) => DefinirInicializacao(itemIniciar.Checked);
        _itemIniciar = itemIniciar;

        var configuracoes = new ToolStripMenuItem("Configurações…");
        configuracoes.Click += (_, _) => AbrirConfiguracoes();

        var restaurar = new ToolStripMenuItem("Restaurar cursor padrão");
        restaurar.Click += (_, _) =>
        {
            // Desmarcar "Ativo" restaura o cursor via Atualizar().
            if (itemAtivo.Checked)
            {
                itemAtivo.Checked = false;
            }
            else
            {
                CursorSistema.Restaurar();
            }
        };

        var sair = new ToolStripMenuItem("Sair");
        sair.Click += (_, _) => Encerrar();

        var menu = new ContextMenuStrip();
        menu.Opening += (_, _) => itemIniciar.Checked = LerInicializacao();
        var versao = new ToolStripMenuItem("Cursor Suave " + _atualizador.VersaoAtual) { Enabled = false };

        var verificar = new ToolStripMenuItem(_atualizador.Instalado
            ? "Verificar atualizações"
            : "Atualizações só na versão instalada") { Enabled = _atualizador.Instalado };
        verificar.Click += async (_, _) => await VerificarAsync(true);
        _itemVerificar = verificar;

        var atualizar = new ToolStripMenuItem("Atualizar") { Visible = false };
        atualizar.Click += async (_, _) => await AtualizarAgoraAsync();
        _itemAtualizar = atualizar;

        menu.Items.AddRange(new ToolStripItem[]
        {
            versao, atualizar, verificar, new ToolStripSeparator(),
            itemAtivo, itemPausar, itemIniciar, configuracoes, restaurar, new ToolStripSeparator(), sair
        });

        _icone = CriarIcone();
        _bandeja = new NotifyIcon
        {
            Icon = _icone,
            Text = "Cursor Suave",
            ContextMenuStrip = menu,
            Visible = true
        };
        _bandeja.BalloonTipClicked += async (_, _) =>
        {
            if (_balaoEhAtualizacao)
            {
                await AtualizarAgoraAsync();
            }
        };
    }

    private void IniciarAtualizacoes()
    {
        if (!_atualizador.Instalado)
        {
            return;
        }
        // Primeira verificação 30 s depois de abrir; depois a cada 6 h.
        _timerAtualizacao = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _timerAtualizacao.Tick += async (_, _) =>
        {
            _timerAtualizacao.Interval = Atualizador.Intervalo;
            if (Atualizador.DeveVerificar(DateTime.UtcNow, _ultimaVerificacao, _config.VerificarAtualizacoes))
            {
                await VerificarAsync(false);
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

    private async System.Threading.Tasks.Task VerificarAsync(bool manual)
    {
        if (_verificando || _baixando)
        {
            return;
        }
        _verificando = true;
        try
        {
            ResultadoVerificacao r = await _atualizador.VerificarAsync();
            _ultimaVerificacao = DateTime.UtcNow;
            switch (r)
            {
                case ResultadoVerificacao.NovaVersao:
                    string nova = _atualizador.VersaoNova ?? string.Empty;
                    if (_itemAtualizar is not null)
                    {
                        _itemAtualizar.Text = "Atualizar para " + nova;
                        _itemAtualizar.Visible = true;
                    }
                    Balao($"Cursor Suave {nova} disponível. Clique para atualizar.", true);
                    break;
                case ResultadoVerificacao.SemNovidade when manual:
                    Balao("Você já está na versão mais recente.", false);
                    break;
                case ResultadoVerificacao.Erro when manual:
                    Balao("Não foi possível verificar: " + Resumir(_atualizador.UltimoErro), false);
                    break;
            }
        }
        finally
        {
            _verificando = false;
        }
    }

    private async System.Threading.Tasks.Task AtualizarAgoraAsync()
    {
        if (_baixando || _atualizador.VersaoNova is null)
        {
            return;
        }
        _baixando = true;
        string textoOriginal = _bandeja?.Text ?? "Cursor Suave";
        bool ok = await _atualizador.BaixarEAplicarAsync(
            p => { if (_bandeja is not null) { _bandeja.Text = $"Baixando atualização… {p}%"; } },
            () =>
            {
                _anel?.Ocultar();
                CursorSistema.Restaurar();
            });
        if (!ok)
        {
            // Se chegou aqui, não reiniciou: houve erro.
            _baixando = false;
            if (_bandeja is not null)
            {
                _bandeja.Text = textoOriginal;
            }
            Balao("Não foi possível atualizar: " + Resumir(_atualizador.UltimoErro), false);
        }
    }

    private static string Resumir(string? mensagem)
    {
        string m = (mensagem ?? "erro desconhecido").Replace('\r', ' ').Replace('\n', ' ').Trim();
        return m.Length <= 80 ? m : m[..77] + "...";
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
        if (_anel is null || _itemAtivo is null || _itemPausar is null)
        {
            return;
        }

        bool deve = _itemAtivo.Checked && _motivo == MotivoSuspensao.Nenhum;
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

        if (_itemPausar is not null && _itemPausar.Checked != nova.PausarTelaCheia)
        {
            _sincronizando = true;
            _itemPausar.Checked = nova.PausarTelaCheia;
            _sincronizando = false;
        }
    }

    /// <summary>Algo que muda os arquivos dos cursores (tamanho, barra, cor ou opacidades efetivas)?</summary>
    private static bool CursorMudou(Configuracao a, Configuracao b) =>
        a.TamanhoBolinha != b.TamanhoBolinha || a.BarraEmTexto != b.BarraEmTexto
        || a.AlturaBarra != b.AlturaBarra || a.LarguraBarra != b.LarguraBarra
        || a.VisualBolinha() != b.VisualBolinha();

    private void AbrirConfiguracoes()
    {
        if (_janelaConfig is not null)
        {
            if (_janelaConfig.WindowState == System.Windows.WindowState.Minimized)
            {
                _janelaConfig.WindowState = System.Windows.WindowState.Normal;
            }
            _janelaConfig.Activate();
            return;
        }

        var janela = new JanelaConfiguracoes(_config, LerInicializacao(),
            AplicarConfiguracao,
            (cfg, iniciar) =>
            {
                AplicarConfiguracao(cfg);
                cfg.SalvarNoDisco();
                DefinirInicializacao(iniciar);
            });
        janela.Closed += (_, _) => _janelaConfig = null;
        _janelaConfig = janela;
        janela.Show();
        janela.Activate();
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
        if (_itemIniciar is not null)
        {
            _itemIniciar.Checked = LerInicializacao();
        }
    }

    private void Encerrar()
    {
        _timer?.Stop();
        _timerAtualizacao?.Stop();
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
