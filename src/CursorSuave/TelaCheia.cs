using System;
using System.Runtime.InteropServices;
using System.Text;

namespace CursorSuave;

/// <summary>Retângulo em pixels físicos (independe de tipos Win32 para facilitar os testes).</summary>
public readonly record struct Retangulo(int Esquerda, int Topo, int Direita, int Base);

public static class TelaCheia
{
    /// <summary>A janela cobre (ou excede) o monitor inteiro?</summary>
    public static bool CobreMonitor(Retangulo janela, Retangulo monitor) =>
        janela.Esquerda <= monitor.Esquerda &&
        janela.Topo <= monitor.Topo &&
        janela.Direita >= monitor.Direita &&
        janela.Base >= monitor.Base;

    /// <summary>Janelas da área de trabalho nunca contam como tela cheia.</summary>
    public static bool EhAreaDeTrabalho(string? classe) =>
        string.Equals(classe, "Progman", StringComparison.Ordinal) ||
        string.Equals(classe, "WorkerW", StringComparison.Ordinal);

    public static bool JanelaEmPrimeiroPlanoEhTelaCheia()
    {
        IntPtr hwnd = Nativo.GetForegroundWindow();
        if (hwnd == IntPtr.Zero || hwnd == Nativo.GetShellWindow())
        {
            return false;
        }

        var sb = new StringBuilder(256);
        Nativo.GetClassName(hwnd, sb, sb.Capacity);
        if (EhAreaDeTrabalho(sb.ToString()))
        {
            return false;
        }

        if (!Nativo.GetWindowRect(hwnd, out Nativo.RECT r))
        {
            return false;
        }

        IntPtr monitor = Nativo.MonitorFromWindow(hwnd, Nativo.MONITOR_DEFAULTTONEAREST);
        var info = new Nativo.MONITORINFO { cbSize = Marshal.SizeOf<Nativo.MONITORINFO>() };
        if (monitor == IntPtr.Zero || !Nativo.GetMonitorInfo(monitor, ref info))
        {
            return false;
        }

        return CobreMonitor(
            new Retangulo(r.Left, r.Top, r.Right, r.Bottom),
            new Retangulo(info.rcMonitor.Left, info.rcMonitor.Top, info.rcMonitor.Right, info.rcMonitor.Bottom));
    }
}
