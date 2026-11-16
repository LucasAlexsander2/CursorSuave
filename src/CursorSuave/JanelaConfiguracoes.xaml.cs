using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
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
    private string _cor;
    private bool _carregando = true;
    private bool _salvou;

    public JanelaConfiguracoes(Configuracao original, bool iniciarComWindows,
        Action<Configuracao> previa, Action<Configuracao, bool> salvar)
    {
        InitializeComponent();
        _original = original;
        _previa = previa;
        _salvar = salvar;
        _programas = original.ProgramasPausados.ToList();
        _cor = original.Cor;

        SlAnel.Value = original.TamanhoAnel;
        SlBolinha.Value = original.TamanhoBolinha;
        SlOpac.Value = original.Opacidade;
        SlOpacLinks.Value = original.OpacidadeEmLinks;
        ChkBarra.IsChecked = original.BarraEmTexto;
        SlSuav.Value = original.AtrasoMs;
        ChkDentro.IsChecked = original.ManterBolinhaDentro;
        SlParado.Value = original.SegundosParado;
        SlOpacParado.Value = original.OpacidadeParado;
        ChkFundo.IsChecked = original.FundoEmLinks;
        ChkClique.IsChecked = original.EfeitoClique;
        ChkParado.IsChecked = original.SumirParado;
        ChkTelaCheia.IsChecked = original.PausarTelaCheia;
        ChkIniciar.IsChecked = iniciarComWindows;
        ChkAtualizar.IsChecked = original.VerificarAtualizacoes;
        TxtCor.Text = original.Cor;
        LstProgramas.ItemsSource = _programas;
        CmbProgramas.ItemsSource = ProcessosComJanela();

        foreach (string hex in CoresProntas)
        {
            Configuracao.TentarLerCor(hex, out byte r, out byte g, out byte b);
            var botao = new Button
            {
                Width = 22,
                Height = 22,
                Margin = new Thickness(0, 0, 4, 0),
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(r, g, b)),
                ToolTip = hex
            };
            botao.Click += (_, _) => TxtCor.Text = hex;
            Amostras.Children.Add(botao);
        }

        foreach (Slider s in new[] { SlAnel, SlBolinha, SlOpac, SlOpacLinks, SlSuav, SlParado, SlOpacParado })
        {
            s.ValueChanged += (_, _) => Mudou();
        }
        foreach (CheckBox c in new[] { ChkDentro, ChkFundo, ChkBarra, ChkClique, ChkParado, ChkTelaCheia })
        {
            c.Click += (_, _) => Mudou();
        }
        TxtCor.TextChanged += (_, _) =>
        {
            if (Configuracao.TentarLerCor(TxtCor.Text, out _, out _, out _))
            {
                _cor = TxtCor.Text;
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

        _carregando = false;
    }

    private Configuracao Montar() => new Configuracao
    {
        TamanhoAnel = (int)Math.Round(SlAnel.Value),
        TamanhoBolinha = (int)Math.Round(SlBolinha.Value),
        Cor = _cor,
        Opacidade = SlOpac.Value,
        OpacidadeEmLinks = SlOpacLinks.Value,
        BarraEmTexto = ChkBarra.IsChecked == true,
        AtrasoMs = (int)Math.Round(SlSuav.Value),
        ManterBolinhaDentro = ChkDentro.IsChecked == true,
        FundoEmLinks = ChkFundo.IsChecked == true,
        EfeitoClique = ChkClique.IsChecked == true,
        SumirParado = ChkParado.IsChecked == true,
        PausarTelaCheia = ChkTelaCheia.IsChecked == true,
        VerificarAtualizacoes = ChkAtualizar.IsChecked == true,
        SegundosParado = SlParado.Value,
        OpacidadeParado = SlOpacParado.Value,
        ProgramasPausados = _programas.ToArray()
    }.Validar();

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
