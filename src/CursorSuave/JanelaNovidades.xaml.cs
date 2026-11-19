using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace CursorSuave;

/// <summary>Modal com as novidades da versão: ícone, título em negrito e texto curto por item.</summary>
public partial class JanelaNovidades : Window
{
    public JanelaNovidades(Window dono, IReadOnlyList<NotaVersao> notas)
    {
        InitializeComponent();
        Owner = dono;
        bool varias = notas.Count > 1;
        TxtTitulo.Text = notas.Count == 0
            ? "Novidades"
            : varias ? "Novidades das versões " + string.Join(", ", Versoes(notas))
                     : "Novidades da versão " + notas[0].Versao;
        foreach (NotaVersao nota in notas)
        {
            if (varias)
            {
                Lista.Children.Add(new TextBlock
                {
                    Text = "Versão " + nota.Versao, FontWeight = FontWeights.SemiBold, Opacity = 0.7,
                    Margin = new Thickness(0, 6, 0, 6)
                });
            }
            foreach (ItemNovidade item in nota.Itens)
            {
                Lista.Children.Add(Linha(item));
            }
        }
        BtnEntendi.Click += (_, _) => Close();
        SourceInitialized += (_, _) => EstiloJanela.AplicarWindows11(this);
    }

    private static IEnumerable<string> Versoes(IReadOnlyList<NotaVersao> notas)
    {
        foreach (NotaVersao n in notas)
        {
            yield return n.Versao;
        }
    }

    private static Grid Linha(ItemNovidade item)
    {
        var g = new Grid { Margin = new Thickness(0, 0, 0, 14) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        string glifo = int.TryParse(item.Icone, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out int cp)
            ? char.ConvertFromUtf32(cp) : "";
        g.Children.Add(new TextBlock
        {
            Text = glifo, FontFamily = new System.Windows.Media.FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
            FontSize = 20, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 2, 0, 0)
        });
        var texto = new StackPanel();
        texto.Children.Add(new TextBlock { Text = item.Titulo, FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.Wrap });
        texto.Children.Add(new TextBlock { Text = item.Texto, TextWrapping = TextWrapping.Wrap, Opacity = 0.8, Margin = new Thickness(0, 2, 0, 0) });
        Grid.SetColumn(texto, 1);
        g.Children.Add(texto);
        return g;
    }
}
