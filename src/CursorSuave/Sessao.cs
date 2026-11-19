using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace CursorSuave;

/// <summary>
/// Registro de que o app trocou os cursores do sistema. Existir o arquivo significa "cursores trocados";
/// apagá-lo (Restaurar) significa "fechou limpo". Quem lê o arquivo depois de o processo morrer sabe que
/// o fechamento foi à força e precisa devolver o cursor padrão.
/// </summary>
public sealed record Sessao(int Pid, bool? VanishOriginal)
{
    private static readonly JsonSerializerOptions Opcoes = new()
    {
        WriteIndented = false,
        PropertyNameCaseInsensitive = true
    };

    public static string CaminhoPadrao => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CursorSuave", "sessao.json");

    /// <summary>Restaura se o arquivo existe e o processo dono dele não está mais rodando.</summary>
    public static bool PrecisaRestaurar(bool arquivoExiste, bool pidAindaRodando) => arquivoExiste && !pidAindaRodando;

    /// <summary>Grava a sessão; falha de disco é ignorada (a proteção é só um extra).</summary>
    public static void Gravar(string caminho, Sessao sessao)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(caminho)!);
            File.WriteAllText(caminho, JsonSerializer.Serialize(sessao, Opcoes));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    /// <summary>Lê a sessão; null se o arquivo não existe, está ilegível ou inválido.</summary>
    public static Sessao? Ler(string caminho)
    {
        try
        {
            if (!File.Exists(caminho))
            {
                return null;
            }
            return JsonSerializer.Deserialize<Sessao>(File.ReadAllText(caminho), Opcoes);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    /// <summary>O arquivo existe? (separado de Ler: um arquivo corrompido ainda indica sessão aberta.)</summary>
    public static bool Existe(string caminho) => File.Exists(caminho);

    public static void Apagar(string caminho)
    {
        try
        {
            File.Delete(caminho);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    public static bool PidRodando(int pid)
    {
        try
        {
            using Process p = Process.GetProcessById(pid);
            return !p.HasExited;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return false;
        }
    }
}
