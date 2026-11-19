using System;
using System.Windows;
using System.Windows.Interop;

namespace CursorSuave;

/// <summary>Cantos arredondados e fundo do sistema (DWM), iguais nas Configurações e no menu da bandeja.</summary>
internal static class EstiloJanela
{
    /// <summary>Chamar com o handle já criado. No Windows 10 as chamadas falham e são ignoradas.</summary>
    public static void AplicarWindows11(Window janela)
    {
        try
        {
            IntPtr hwnd = new WindowInteropHelper(janela).Handle;
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
}
