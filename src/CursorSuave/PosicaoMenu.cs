using System;

namespace CursorSuave;

/// <summary>Retângulo em pixels físicos (como o rcWork do Windows).</summary>
public readonly record struct AreaPx(int Esquerda, int Topo, int Direita, int Base);

/// <summary>Conta pura da posição do menu da bandeja, tudo em pixels físicos.</summary>
public static class PosicaoMenu
{
    /// <summary>
    /// O canto do menu fica no ponteiro; se não couber à direita ou embaixo, abre para a esquerda
    /// ou para cima. Depois um limite final garante que nunca saia da área de trabalho.
    /// </summary>
    public static (int X, int Y) Calcular(int ponteiroX, int ponteiroY, int largura, int altura, AreaPx area)
    {
        int x = ponteiroX + largura > area.Direita ? ponteiroX - largura : ponteiroX;
        int y = ponteiroY + altura > area.Base ? ponteiroY - altura : ponteiroY;
        // POR QUÊ: o limite final cobre monitor pequeno e menu maior que o espaço dos dois lados.
        x = Math.Max(area.Esquerda, Math.Min(x, area.Direita - largura));
        y = Math.Max(area.Topo, Math.Min(y, area.Base - altura));
        return (x, y);
    }
}
