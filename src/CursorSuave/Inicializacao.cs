using Microsoft.Win32;

namespace CursorSuave;

/// <summary>Iniciar com o Windows: o estado real é o valor no registro (HKCU\...\Run).</summary>
public static class Inicializacao
{
    private const string Chave = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Nome = "CursorSuave";

    /// <summary>Valor do registro: o caminho do exe entre aspas.</summary>
    public static string MontarValor(string caminhoExe) => "\"" + caminhoExe + "\"";

    /// <summary>
    /// Valor novo do registro após uma instalação/atualização: se já existe valor
    /// (<paramref name="valorAtual"/> não nulo), aponta para o exe atual; se não existe, devolve null (nada a fazer).
    /// </summary>
    public static string? NovoValorAposAtualizacao(string? valorAtual, string caminhoExe) =>
        valorAtual is null ? null : MontarValor(caminhoExe);

    /// <summary>Regrava o valor Run com o caminho do exe atual, se o valor existir.</summary>
    public static void CorrigirCaminhoAtual()
    {
        try
        {
            string? exe = System.Environment.ProcessPath;
            if (exe is null)
            {
                return;
            }
            using RegistryKey? k = Registry.CurrentUser.OpenSubKey(Chave, true);
            string? novo = NovoValorAposAtualizacao(k?.GetValue(Nome) as string, exe);
            if (k is not null && novo is not null)
            {
                k.SetValue(Nome, novo);
            }
        }
        catch (System.Exception ex) when (ex is System.Security.SecurityException or System.UnauthorizedAccessException or System.IO.IOException)
        {
            // Sem acesso ao registro: o usuário pode religar a opção nas configurações.
        }
    }

    public static bool Ativo()
    {
        using RegistryKey? k = Registry.CurrentUser.OpenSubKey(Chave);
        return k?.GetValue(Nome) is string;
    }

    public static void Definir(bool ativo, string caminhoExe)
    {
        using RegistryKey k = Registry.CurrentUser.CreateSubKey(Chave);
        if (ativo)
        {
            k.SetValue(Nome, MontarValor(caminhoExe));
        }
        else
        {
            k.DeleteValue(Nome, false);
        }
    }
}
