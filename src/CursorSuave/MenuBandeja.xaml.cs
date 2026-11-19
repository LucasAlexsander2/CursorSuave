using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace CursorSuave;

/// <summary>Estado de abertura e ações do menu da bandeja. A janela é criada a cada abertura.</summary>
public sealed record DadosMenu(
    ImageSource Icone,
    string Versao,
    bool Ativo,
    bool PausarTelaCheia,
    bool IniciarComWindows,
    SituacaoAtualizacao Atualizacao,
    Action<bool> AoAtivo,
    Action<bool> AoPausar,
    Action<bool> AoIniciar,
    Action AoConfiguracoes,
    Action AoVerificar,
    Action AoAtualizar,
    Action AoRestaurar,
    Action AoSair);

/// <summary>
/// Menu da bandeja: janela WPF sem borda, aberta junto ao ponteiro. Fecha ao perder o foco,
/// com Esc ou depois de qualquer botão; os interruptores aplicam na hora e deixam o menu aberto.
/// </summary>
public partial class MenuBandeja : Window
{
    private const double LarguraDip = 300;
    private bool _fechando;

    public MenuBandeja(DadosMenu d)
    {
        InitializeComponent();
        ImgIcone.Source = d.Icone;
        TxtVersao.Text = "Versão " + d.Versao;
        Icon = d.Icone;

        TglAtivo.IsChecked = d.Ativo;
        TglPausar.IsChecked = d.PausarTelaCheia;
        TglIniciar.IsChecked = d.IniciarComWindows;
        TglAtivo.Click += (_, _) => d.AoAtivo(TglAtivo.IsChecked == true);
        TglPausar.Click += (_, _) => d.AoPausar(TglPausar.IsChecked == true);
        TglIniciar.Click += (_, _) => d.AoIniciar(TglIniciar.IsChecked == true);

        SituacaoAtualizacao s = d.Atualizacao;
        bool temNova = s.VersaoNova is not null && s.Estado is EstadoAtualizacao.NovaVersao or EstadoAtualizacao.Baixando;
        FaixaAtualizacao.Visibility = temNova ? Visibility.Visible : Visibility.Collapsed;
        if (temNova)
        {
            bool baixando = s.Estado == EstadoAtualizacao.Baixando;
            TxtAtualizacao.Text = baixando ? $"Baixando… {s.Percentual}%" : $"Versão {s.VersaoNova} disponível";
            BtnAtualizar.IsEnabled = !baixando;
        }
        switch (s.Estado)
        {
            case EstadoAtualizacao.NaoInstalado:
                TxtVerificar.Text = "Atualizações só na versão instalada";
                BtnVerificar.IsEnabled = false;
                break;
            case EstadoAtualizacao.Verificando:
                TxtVerificar.Text = "Verificando…";
                BtnVerificar.IsEnabled = false;
                break;
            case EstadoAtualizacao.Baixando:
                BtnVerificar.IsEnabled = false;
                break;
        }

        BtnAtualizar.Click += (_, _) => Acao(d.AoAtualizar);
        BtnConfig.Click += (_, _) => Acao(d.AoConfiguracoes);
        BtnVerificar.Click += (_, _) => Acao(d.AoVerificar);
        BtnRestaurar.Click += (_, _) => Acao(d.AoRestaurar);
        BtnSair.Click += (_, _) => Acao(d.AoSair);

        // POR QUÊ: sem foco o Deactivated nunca dispara e o menu ficaria preso na tela.
        Deactivated += (_, _) => Fechar();
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                Fechar();
            }
        };
    }

    private void Acao(Action acao)
    {
        Fechar();
        acao();
    }

    private void Fechar()
    {
        if (_fechando)
        {
            return;
        }
        _fechando = true;
        Close();
    }

    /// <summary>
    /// Abre o menu com o canto junto ao ponteiro. Tudo em pixels físicos; a escala é a do monitor do ponteiro.
    /// </summary>
    public void MostrarEm(int ponteiroX, int ponteiroY, AreaPx area, double escala)
    {
        IntPtr hwnd = new WindowInteropHelper(this).EnsureHandle();
        EstiloJanela.AplicarWindows11(this);

        // Mede o conteúdo antes de mostrar, para saber se cabe embaixo ou à direita.
        ((FrameworkElement)Content).Measure(new Size(LarguraDip, double.PositiveInfinity));
        int largura = (int)Math.Ceiling(LarguraDip * escala);
        int altura = (int)Math.Ceiling(((FrameworkElement)Content).DesiredSize.Height * escala);
        (int x, int y) = PosicaoMenu.Calcular(ponteiroX, ponteiroY, largura, altura, area);

        const uint flags = Nativo.SWP_NOSIZE | Nativo.SWP_NOZORDER | Nativo.SWP_NOACTIVATE;
        _ = Nativo.SetWindowPos(hwnd, IntPtr.Zero, x, y, 0, 0, flags);
        Show();
        // CUIDADO: ir a outro monitor muda o DPI e o WPF reposiciona a janela; corrige de novo.
        _ = Nativo.SetWindowPos(hwnd, IntPtr.Zero, x, y, 0, 0, flags);
        Activate();
        _ = Nativo.SetForegroundWindow(hwnd);
    }
}
