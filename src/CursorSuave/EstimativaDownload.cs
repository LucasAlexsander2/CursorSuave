using System;
using System.Globalization;

namespace CursorSuave;

/// <summary>
/// Estima o tempo restante de um download a partir só da porcentagem (o Velopack não informa bytes).
/// </summary>
public sealed class EstimativaDownload
{
    private const double Alfa = 0.3;
    private double? _suavizado;

    /// <summary>Segundos restantes, ou null enquanto faltam dados (menos de 3% ou menos de 1 s).</summary>
    public double? Registrar(int percentual, TimeSpan decorrido)
    {
        if (percentual < 3 || decorrido < TimeSpan.FromSeconds(1))
        {
            return null;
        }
        int p = Math.Min(percentual, 100);
        double bruto = decorrido.TotalSeconds * (100 - p) / p;
        // POR QUÊ: a taxa média oscila; a média móvel exponencial evita o texto pular a cada 1%.
        _suavizado = _suavizado is double anterior ? Alfa * bruto + (1 - Alfa) * anterior : bruto;
        return _suavizado;
    }

    /// <summary>"cerca de 8 s", "cerca de 1 min 05 s" ou "menos de 1 s".</summary>
    public static string Formatar(double segundos)
    {
        if (segundos < 1)
        {
            return "menos de 1 s";
        }
        int total = (int)Math.Round(segundos);
        if (total < 60)
        {
            return $"cerca de {total} s";
        }
        return string.Create(CultureInfo.InvariantCulture, $"cerca de {total / 60} min {total % 60:00} s");
    }
}
