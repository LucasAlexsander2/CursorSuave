using System;
using System.Collections.Generic;

namespace CursorSuave;

public enum CursorDaBolinha { Normal, Mao, Barra }

/// <summary>Lógica pura dos degraus de apagar da bolinha (e da barra) quando o mouse fica parado.</summary>
public static class NivelBolinha
{
    public const int Degraus = 4;
    public const double SegundosPorDegrau = 0.075; // 4 degraus em 300 ms

    /// <summary>Degrau (0 a 4) e opacidade-alvo (fator sobre a opacidade cheia) para o tempo parado.</summary>
    public static (int Degrau, double Opacidade) Calcular(double segundosDesdeParado, double segundosParado, double opacidadeParado)
    {
        if (double.IsNaN(opacidadeParado) || opacidadeParado >= 1)
        {
            return (0, 1);
        }
        double t = segundosDesdeParado - segundosParado;
        if (double.IsNaN(t) || t <= 0)
        {
            return (0, 1);
        }
        int d = (int)Math.Min(Degraus, Math.Floor(t / SegundosPorDegrau + 1e-9));
        return (d, FatorDoDegrau(d, opacidadeParado));
    }

    /// <summary>Fator de opacidade do degrau: 1 no degrau 0 até <paramref name="opacidadeParado"/> no degrau 4.</summary>
    public static double FatorDoDegrau(int degrau, double opacidadeParado)
    {
        degrau = Math.Clamp(degrau, 0, Degraus);
        double o = Math.Clamp(opacidadeParado, 0, 1);
        return 1 + (o - 1) * degrau / Degraus;
    }

    /// <summary>Cursores trocados em cada degrau: normal e mão sempre; a barra só se ligada.</summary>
    public static IReadOnlyList<CursorDaBolinha> CursoresDoDegrau(bool barraEmTexto) =>
        barraEmTexto
            ? new[] { CursorDaBolinha.Normal, CursorDaBolinha.Mao, CursorDaBolinha.Barra }
            : new[] { CursorDaBolinha.Normal, CursorDaBolinha.Mao };
}
