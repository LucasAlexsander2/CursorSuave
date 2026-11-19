using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace CursorSuave;

/// <summary>Lógica pura dos campos digitáveis da janela de configurações.</summary>
public static class CampoNumerico
{
    private static readonly Regex Numero = new(@"^\s*([+-]?\d+(?:[.,]\d+)?)\s*[A-Za-z%]*\s*$", RegexOptions.Compiled);

    /// <summary>
    /// Lê um número do texto (vírgula ou ponto; a unidade digitada, como "ms" ou "%", é ignorada),
    /// limita à faixa e arredonda em <paramref name="casas"/> casas. Texto inválido devolve null.
    /// </summary>
    public static double? Interpretar(string? texto, double min, double max, int casas)
    {
        Match m = Numero.Match(texto ?? string.Empty);
        if (!m.Success ||
            !double.TryParse(m.Groups[1].Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ||
            double.IsNaN(v) || double.IsInfinity(v))
        {
            return null;
        }
        return Math.Round(Math.Clamp(v, min, max), casas);
    }

    /// <summary>Opacidade interna (0 a 1) para o texto em % (0 a 100).</summary>
    public static double OpacidadeParaPorcento(double opacidade) => Math.Round(opacidade * 100);

    /// <summary>Porcentagem digitada (0 a 100) para opacidade interna (0 a 1).</summary>
    public static double PorcentoParaOpacidade(double porcento) => Math.Round(porcento) / 100.0;
}
