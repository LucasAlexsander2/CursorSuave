using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace CursorSuave;

/// <summary>
/// Janela de configurações com prévia ao vivo. "Salvar" grava; fechar de qualquer outro modo
/// (Cancelar, X, Esc) devolve a configuração original.
/// </summary>
public partial class JanelaConfiguracoes : Window
{
    private static readonly string[] CoresProntas =
        { "#FFFFFF", "#FFD60A", "#30D158", "#0A84FF", "#FF453A", "#BF5AF2" };

    private readonly Configuracao _original;
    private readonly Action<Configuracao> _previa;
    private readonly Action<Configuracao, bool> _salvar;
    private readonly List<string> _programas;
    private readonly List<(StackPanel Painel, TextBox Campo)> _amostras = new();
    private string _cor;
    private string _corBolinha;
    private bool _carregando = true;
    private bool _salvou;

    private readonly Slider SlAnel, SlBolinha, SlOpac, SlOpacLinks, SlSuav, SlParado, SlOpacParado,
        SlAlturaBarra, SlLarguraBarra, SlOpacBolinha, SlOpacBolinhaLinks, SlOpacBolinhaParado;
    private readonly CheckBox ChkDentro, ChkFundo, ChkClique;
    private readonly TextBox TxtCor, TxtCorBolinha;
    private readonly FrameworkElement _linhaAltura, _linhaLargura;

    public JanelaConfiguracoes(Configuracao original, bool iniciarComWindows,
        Action<Configuracao> previa, Action<Configuracao, bool> salvar, string versao = "")
    {
        InitializeComponent();
        _original = original;
        _previa = previa;
        _salvar = salvar;
        _programas = original.ProgramasPausados.ToList();
        _cor = original.Cor;
        _corBolinha = original.CorBolinha;

        // Altura limitada à tela de trabalho (o ScrollViewer cuida do resto).
        MaxHeight = SystemParameters.WorkArea.Height * 0.95;
        if (Height > MaxHeight)
        {
            Height = MaxHeight;
        }

        // Cartão Anel
        SlAnel = LinhaSlider(CartaoAnel, "Tamanho do anel", 16, 60, 1, "px", false, out _);
        TxtCor = LinhaCor(CartaoAnel, out StackPanel amostrasAnel);
        SlOpac = LinhaSlider(CartaoAnel, "Opacidade", 0.2, 1, 0.01, "%", true, out _);
        SlOpacLinks = LinhaSlider(CartaoAnel, "Opacidade em clicáveis", 0.2, 1, 0.01, "%", true, out _);
        ChkFundo = Check(CartaoAnel, "Fundo leve ao passar sobre clicáveis");
        SlSuav = LinhaSlider(CartaoAnel, "Atraso do anel", 0, 500, 10, "ms", false, out _);
        ChkDentro = Check(CartaoAnel, "Manter a bolinha dentro do anel");
        ChkClique = Check(CartaoAnel, "Encolher ao clicar");

        // Cartão Bolinha (o tamanho fica fora do painel que "mesmo visual" desabilita)
        SlBolinha = LinhaSlider(PainelBolinha, "Tamanho da bolinha", 4, 12, 1, "px", false, out FrameworkElement linhaTamanho);
        PainelBolinha.Children.Remove(linhaTamanho);
        Panel cartaoBolinha = (Panel)PainelBolinha.Parent;
        cartaoBolinha.Children.Insert(cartaoBolinha.Children.IndexOf(PainelBolinha), linhaTamanho);
        TxtCorBolinha = LinhaCor(PainelBolinha, out StackPanel amostrasBolinha);
        SlOpacBolinha = LinhaSlider(PainelBolinha, "Opacidade", 0.1, 1, 0.01, "%", true, out _);
        SlOpacBolinhaLinks = LinhaSlider(PainelBolinha, "Opacidade em clicáveis", 0.1, 1, 0.01, "%", true, out _);
        SlOpacBolinhaParado = LinhaSlider(PainelBolinha, "Opacidade parada", 0.1, 1, 0.01, "%", true, out _);

        // Cartão Barra de texto
        SlAlturaBarra = LinhaSlider(CartaoBarra, "Altura da barra", 8, 28, 1, "px", false, out _linhaAltura);
        SlLarguraBarra = LinhaSlider(CartaoBarra, "Largura da barra", 1, 6, 1, "px", false, out _linhaLargura);

        // Cartão Mouse parado
        SlParado = LinhaSlider(CartaoParado, "Segundos até apagar", 1, 30, 1, "s", false, out _);
        SlOpacParado = LinhaSlider(CartaoParado, "Opacidade parado do anel", 0, 0.9, 0.01, "%", true, out _);

        AdicionarAmostras(amostrasAnel, TxtCor);
        AdicionarAmostras(amostrasBolinha, TxtCorBolinha);

        LstProgramas.ItemsSource = _programas;
        CmbProgramas.ItemsSource = ProcessosComJanela();
        ChkIniciar.IsChecked = iniciarComWindows;
        ChkAtualizar.IsChecked = original.VerificarAtualizacoes;
        TxtVersao.Text = string.IsNullOrWhiteSpace(versao) ? string.Empty : "Versão atual: " + versao;
        Aplicar(original);

        foreach (Slider s in new[] { SlAnel, SlBolinha, SlOpac, SlOpacLinks, SlSuav, SlParado, SlOpacParado, SlAlturaBarra, SlLarguraBarra, SlOpacBolinha, SlOpacBolinhaLinks, SlOpacBolinhaParado })
        {
            s.ValueChanged += (_, _) => Mudou();
        }
        foreach (CheckBox c in new[] { ChkDentro, ChkFundo, ChkBarra, ChkClique, ChkParado, ChkTelaCheia, ChkMesmoVisual })
        {
            c.Click += (_, _) =>
            {
                AtualizarBarra();
                PainelBolinha.IsEnabled = ChkMesmoVisual.IsChecked != true;
                Mudou();
            };
        }
        TxtCor.TextChanged += (_, _) =>
        {
            if (Configuracao.TentarLerCor(TxtCor.Text, out _, out _, out _))
            {
                _cor = TxtCor.Text;
                Mudou();
            }
        };
        TxtCorBolinha.TextChanged += (_, _) =>
        {
            if (Configuracao.TentarLerCor(TxtCorBolinha.Text, out _, out _, out _))
            {
                _corBolinha = TxtCorBolinha.Text;
                Mudou();
            }
        };
        BtnAdicionar.Click += (_, _) => Adicionar();
        BtnRemover.Click += (_, _) =>
        {
            if (LstProgramas.SelectedItem is string nome)
            {
                _programas.Remove(nome);
                AtualizarLista();
                Mudou();
            }
        };
        BtnRestaurar.Click += (_, _) => Restaurar();
        BtnSalvar.Click += (_, _) =>
        {
            _salvou = true;
            _salvar(Montar(), ChkIniciar.IsChecked == true);
            Close();
        };
        BtnCancelar.Click += (_, _) => Close();
        Closed += (_, _) =>
        {
            if (!_salvou)
            {
                _previa(_original);
            }
        };
        SourceInitialized += (_, _) => AplicarEstiloWindows11();

        _carregando = false;
    }

    /// <summary>Cantos arredondados e fundo Mica; no Windows 10 as chamadas falham e são ignoradas.</summary>
    private void AplicarEstiloWindows11()
    {
        try
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            int cantos = 2;   // DWMWCP_ROUND
            int mica = 2;     // DWMSBT_MAINWINDOW
            _ = Nativo.DwmSetWindowAttribute(hwnd, Nativo.DWMWA_WINDOW_CORNER_PREFERENCE, ref cantos, sizeof(int));
            _ = Nativo.DwmSetWindowAttribute(hwnd, Nativo.DWMWA_SYSTEMBACKDROP_TYPE, ref mica, sizeof(int));
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            // Sem suporte do sistema: segue com o visual do tema.
        }
    }

    /// <summary>Coloca os valores da configuração nos controles (sem prévia). Não mexe em programas, iniciar nem atualizar.</summary>
    private void Aplicar(Configuracao c)
    {
        bool antes = _carregando;
        _carregando = true;
        SlAnel.Value = c.TamanhoAnel;
        SlBolinha.Value = c.TamanhoBolinha;
        SlOpac.Value = c.Opacidade;
        SlOpacLinks.Value = c.OpacidadeEmLinks;
        ChkBarra.IsChecked = c.BarraEmTexto;
        SlAlturaBarra.Value = c.AlturaBarra;
        SlLarguraBarra.Value = c.LarguraBarra;
        SlSuav.Value = c.AtrasoMs;
        ChkDentro.IsChecked = c.ManterBolinhaDentro;
        SlParado.Value = c.SegundosParado;
        SlOpacParado.Value = c.OpacidadeParado;
        ChkFundo.IsChecked = c.FundoEmLinks;
        ChkClique.IsChecked = c.EfeitoClique;
        ChkParado.IsChecked = c.SumirParado;
        ChkTelaCheia.IsChecked = c.PausarTelaCheia;
        _cor = c.Cor;
        _corBolinha = c.CorBolinha;
        TxtCor.Text = c.Cor;
        TxtCorBolinha.Text = c.CorBolinha;
        SlOpacBolinha.Value = c.OpacidadeBolinha;
        SlOpacBolinhaLinks.Value = c.OpacidadeBolinhaEmLinks;
        SlOpacBolinhaParado.Value = c.OpacidadeBolinhaParado;
        ChkMesmoVisual.IsChecked = c.MesmoVisualBolinhaAnel;
        PainelBolinha.IsEnabled = !c.MesmoVisualBolinhaAnel;
        AtualizarBarra();
        _carregando = antes;
    }

    private void Restaurar()
    {
        MessageBoxResult r = MessageBox.Show(this,
            "Voltar todos os ajustes de aparência e comportamento ao padrão?",
            "Restaurar padrão", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (r != MessageBoxResult.Yes)
        {
            return;
        }
        // Programas pausados, "Iniciar com o Windows" e "Verificar atualizações" ficam como estão.
        Aplicar(Montar().ComPadroesVisuais());
        Mudou();
    }

    private Configuracao Montar() => new Configuracao
    {
        TamanhoAnel = (int)Math.Round(SlAnel.Value),
        TamanhoBolinha = (int)Math.Round(SlBolinha.Value),
        Cor = _cor,
        Opacidade = Math.Round(SlOpac.Value, 2),
        OpacidadeEmLinks = Math.Round(SlOpacLinks.Value, 2),
        BarraEmTexto = ChkBarra.IsChecked == true,
        AlturaBarra = (int)Math.Round(SlAlturaBarra.Value),
        LarguraBarra = (int)Math.Round(SlLarguraBarra.Value),
        AtrasoMs = (int)Math.Round(SlSuav.Value),
        ManterBolinhaDentro = ChkDentro.IsChecked == true,
        FundoEmLinks = ChkFundo.IsChecked == true,
        EfeitoClique = ChkClique.IsChecked == true,
        SumirParado = ChkParado.IsChecked == true,
        PausarTelaCheia = ChkTelaCheia.IsChecked == true,
        VerificarAtualizacoes = ChkAtualizar.IsChecked == true,
        SegundosParado = Math.Round(SlParado.Value),
        OpacidadeParado = Math.Round(SlOpacParado.Value, 2),
        OpacidadeBolinha = Math.Round(SlOpacBolinha.Value, 2),
        OpacidadeBolinhaEmLinks = Math.Round(SlOpacBolinhaLinks.Value, 2),
        OpacidadeBolinhaParado = Math.Round(SlOpacBolinhaParado.Value, 2),
        CorBolinha = _corBolinha,
        MesmoVisualBolinhaAnel = ChkMesmoVisual.IsChecked == true,
        ProgramasPausados = _programas.ToArray()
    }.Validar();

    // ---------- Construção das linhas (rótulo à esquerda, controle à direita) ----------

    /// <summary>
    /// Linha com slider + campo digitável + unidade. Opacidades usam % (0 a 100) no texto e 0 a 1 no slider.
    /// O slider arredonda pelo passo (TickFrequency), então nunca grava 0,5983.
    /// </summary>
    private Slider LinhaSlider(Panel destino, string rotulo, double min, double max, double passo,
        string unidade, bool porcento, out FrameworkElement linha)
    {
        var slider = new Slider
        {
            Minimum = min,
            Maximum = max,
            TickFrequency = passo,
            IsSnapToTickEnabled = true,
            Width = 190,
            VerticalAlignment = VerticalAlignment.Center
        };
        var campo = new TextBox
        {
            Width = 64,
            Margin = new Thickness(10, 0, 4, 0),
            HorizontalContentAlignment = HorizontalAlignment.Right,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        var textoUnidade = new TextBlock { Text = unidade, Width = 24, VerticalAlignment = VerticalAlignment.Center, Opacity = 0.7 };

        double Exibido(double v) => porcento ? CampoNumerico.OpacidadeParaPorcento(v) : Math.Round(v);
        void Atualizar() => campo.Text = Exibido(slider.Value).ToString("0", CultureInfo.InvariantCulture);

        slider.ValueChanged += (_, _) => Atualizar();

        void Confirmar()
        {
            double? v = CampoNumerico.Interpretar(campo.Text, Exibido(min), Exibido(max), 0);
            if (v is double valor)
            {
                slider.Value = porcento ? CampoNumerico.PorcentoParaOpacidade(valor) : valor;
            }
            Atualizar(); // texto inválido volta ao valor atual; fora da faixa mostra o valor limitado
        }
        campo.LostFocus += (_, _) => Confirmar();
        campo.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                Confirmar();
                campo.SelectAll();
                e.Handled = true;
            }
            else if (e.Key is Key.Up or Key.Down)
            {
                Confirmar();
                double sinal = e.Key == Key.Up ? 1 : -1;
                slider.Value = Math.Clamp(slider.Value + sinal * passo, min, max);
                campo.SelectAll();
                e.Handled = true;
            }
        };

        var direita = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        direita.Children.Add(slider);
        direita.Children.Add(campo);
        direita.Children.Add(textoUnidade);
        linha = Linha(rotulo, direita);
        destino.Children.Add(linha);
        Atualizar();
        return slider;
    }

    private TextBox LinhaCor(Panel destino, out StackPanel amostras)
    {
        amostras = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var campo = new TextBox
        {
            Width = 84,
            Margin = new Thickness(6, 0, 0, 0),
            MaxLength = 7,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        var direita = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        direita.Children.Add(amostras);
        direita.Children.Add(campo);
        destino.Children.Add(Linha("Cor", direita));
        _amostras.Add((amostras, campo));
        return campo;
    }

    private static CheckBox Check(Panel destino, string texto)
    {
        var c = new CheckBox { Content = texto, Margin = new Thickness(0, 4, 0, 4) };
        destino.Children.Add(c);
        return c;
    }

    private static Grid Linha(string rotulo, UIElement controle)
    {
        var g = new Grid { Margin = new Thickness(0, 4, 0, 4) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.Children.Add(new TextBlock { Text = rotulo, VerticalAlignment = VerticalAlignment.Center });
        Grid.SetColumn(controle, 1);
        g.Children.Add(controle);
        return g;
    }

    /// <summary>Seis botões redondos; o da cor escolhida ganha um anel de seleção.</summary>
    private void AdicionarAmostras(StackPanel destino, TextBox campo)
    {
        var botoes = new List<Button>();
        foreach (string hex in CoresProntas)
        {
            Configuracao.TentarLerCor(hex, out byte r, out byte g, out byte b);
            var botao = new Button
            {
                Style = (Style)FindResource("Amostra"),
                Background = new SolidColorBrush(Color.FromRgb(r, g, b)),
                ToolTip = hex
            };
            botao.Click += (_, _) => campo.Text = hex;
            botoes.Add(botao);
            destino.Children.Add(botao);
        }
        void Marcar()
        {
            foreach (Button botao in botoes)
            {
                botao.Tag = string.Equals(campo.Text.Trim(), (string)botao.ToolTip, StringComparison.OrdinalIgnoreCase) ? "sel" : null;
            }
        }
        campo.TextChanged += (_, _) => Marcar();
        Marcar();
    }

    private void AtualizarBarra()
    {
        bool ligada = ChkBarra.IsChecked == true;
        _linhaAltura.IsEnabled = ligada;
        _linhaLargura.IsEnabled = ligada;
    }

    private void Mudou()
    {
        if (!_carregando)
        {
            _previa(Montar());
        }
    }

    private void Adicionar()
    {
        string nome = (CmbProgramas.Text ?? string.Empty).Trim();
        if (nome.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            nome = nome[..^4];
        }
        if (nome.Length == 0 || _programas.Contains(nome, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }
        _programas.Add(nome);
        CmbProgramas.Text = string.Empty;
        AtualizarLista();
        Mudou();
    }

    private void AtualizarLista()
    {
        LstProgramas.ItemsSource = null;
        LstProgramas.ItemsSource = _programas;
    }

    private static List<string> ProcessosComJanela()
    {
        var nomes = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Process p in Process.GetProcesses())
        {
            try
            {
                if (p.MainWindowHandle != IntPtr.Zero)
                {
                    nomes.Add(p.ProcessName);
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                // Processo encerrou ou sem acesso: ignora.
            }
            finally
            {
                p.Dispose();
            }
        }
        return nomes.ToList();
    }
}
