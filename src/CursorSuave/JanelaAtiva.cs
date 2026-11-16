using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace CursorSuave;

/// <summary>Classe e nome do processo da janela em primeiro plano (com cache por PID).</summary>
public static class JanelaAtiva
{
    private static readonly Dictionary<uint, string> CacheProcessos = new();
    private const int LimiteCache = 128;

    public static (string Classe, string Processo) Ler()
    {
        IntPtr hwnd = Nativo.GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
        {
            return (string.Empty, string.Empty);
        }

        var sb = new StringBuilder(256);
        Nativo.GetClassName(hwnd, sb, sb.Capacity);
        Nativo.GetWindowThreadProcessId(hwnd, out uint pid);
        return (sb.ToString(), NomeDoProcesso(pid));
    }

    private static string NomeDoProcesso(uint pid)
    {
        if (pid == 0)
        {
            return string.Empty;
        }
        if (CacheProcessos.TryGetValue(pid, out string? nome))
        {
            return nome;
        }

        try
        {
            using Process p = Process.GetProcessById((int)pid);
            nome = p.ProcessName;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            nome = string.Empty;
        }

        if (CacheProcessos.Count >= LimiteCache)
        {
            CacheProcessos.Clear();
        }
        if (nome.Length > 0)
        {
            CacheProcessos[pid] = nome;
        }
        return nome;
    }
}
