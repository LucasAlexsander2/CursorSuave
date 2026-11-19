using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace CursorSuave;

/// <summary>
/// Processo auxiliar (a mesma exe com --guardiao PID). Sem janela, sem bandeja e sem mutex:
/// só espera o app principal terminar e, se ele não fechou limpo, devolve o cursor padrão.
/// </summary>
public static class Guardiao
{
    public const string Argumento = "--guardiao";

    /// <summary>Se os argumentos pedem o modo guardião, devolve o PID observado.</summary>
    public static bool TentarLerArgumentos(string[] args, out int pid)
    {
        pid = 0;
        return args.Length >= 2 && args[0] == Argumento && int.TryParse(args[1], out pid) && pid > 0;
    }

    /// <summary>Abre o guardião para o processo atual. Falhar não atrapalha o app (ainda há a rede de segurança na abertura).</summary>
    public static void Iniciar()
    {
        try
        {
            string? exe = Environment.ProcessPath;
            // Rodando por "dotnet CursorSuave.dll" a exe seria o dotnet.exe e os argumentos não bateriam.
            if (exe is null || Path.GetFileNameWithoutExtension(exe).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            Process.Start(new ProcessStartInfo(exe, $"{Argumento} {Environment.ProcessId}")
            {
                UseShellExecute = false,
                CreateNoWindow = true
            })?.Dispose();
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException)
        {
        }
    }

    /// <summary>Corpo do processo guardião: espera o principal e restaura se a sessão ficou aberta.</summary>
    public static void Executar(int pid)
    {
        try
        {
            using Process principal = Process.GetProcessById(pid);
            principal.WaitForExit(); // bloqueia sem gastar CPU
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or Win32Exception)
        {
            // O principal já tinha terminado: segue para a checagem do arquivo.
        }

        string caminho = Sessao.CaminhoPadrao;
        // Arquivo apagado = o principal restaurou sozinho (Sair, pausa, atualização): não faz nada.
        if (!Sessao.Existe(caminho))
        {
            return;
        }
        Sessao? s = Sessao.Ler(caminho);
        // CUIDADO: arquivo de outro PID é de uma sessão mais nova; não mexer.
        if (s is not null && s.Pid != pid)
        {
            return;
        }
        RestaurarSistema(s?.VanishOriginal);
        Sessao.Apagar(caminho);
    }

    /// <summary>Devolve os cursores do Windows e o "ocultar ponteiro ao digitar" original (se conhecido).</summary>
    public static void RestaurarSistema(bool? vanishOriginal)
    {
        if (vanishOriginal is bool original)
        {
            Nativo.SystemParametersInfo(Nativo.SPI_SETMOUSEVANISH, 0, original ? (IntPtr)1 : IntPtr.Zero, 0);
        }
        Nativo.SystemParametersInfo(Nativo.SPI_SETCURSORS, 0, IntPtr.Zero, 0);
    }
}
