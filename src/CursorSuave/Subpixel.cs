using System;

namespace CursorSuave;

/// <summary>Lógica pura: separa a posição em origem inteira da janela e deslocamento fracionário.</summary>
public static class Subpixel
{
    /// <param name="pos">Posição do centro (px físicos, fracionária).</param>
    /// <param name="metade">Metade do tamanho da janela, em px.</param>
    /// <returns>origem = floor(pos) - metade; frac em [0, 1).</returns>
    public static (int origem, double frac) Dividir(double pos, int metade)
    {
        double chao = Math.Floor(pos);
        return ((int)chao - metade, pos - chao);
    }
}
