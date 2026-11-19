using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace CursorSuave;

public sealed record ItemNovidade(string Icone, string Titulo, string Texto);

public sealed record NotaVersao(string Versao, IReadOnlyList<ItemNovidade> Itens);

/// <summary>Lógica pura das notas de versão (o arquivo Novidades.json vai embutido no executável).</summary>
public static class Novidades
{
    private static readonly JsonSerializerOptions Opcoes = new() { PropertyNameCaseInsensitive = true };

    /// <summary>Lê o JSON; texto vazio ou inválido dá lista vazia (as novidades nunca derrubam o app).</summary>
    public static IReadOnlyList<NotaVersao> Ler(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<NotaVersao>();
        }
        try
        {
            return JsonSerializer.Deserialize<List<NotaVersao>>(json, Opcoes)?
                .Where(n => n is not null && n.Itens is not null).ToList() ?? new List<NotaVersao>();
        }
        catch (JsonException)
        {
            return Array.Empty<NotaVersao>();
        }
    }

    /// <summary>Lê o Novidades.json embutido no assembly.</summary>
    public static IReadOnlyList<NotaVersao> LerEmbutido()
    {
        using Stream? s = typeof(Novidades).Assembly.GetManifestResourceStream("Novidades.json");
        if (s is null)
        {
            return Array.Empty<NotaVersao>();
        }
        using var leitor = new StreamReader(s);
        return Ler(leitor.ReadToEnd());
    }

    /// <summary>Notas das versões em (vista, atual], da mais nova para a mais velha. Versão inválida: nenhuma.</summary>
    public static IReadOnlyList<NotaVersao> Pendentes(IReadOnlyList<NotaVersao> lista, string? vista, string? atual)
    {
        if (!TentarVersao(vista, out Version? v) || !TentarVersao(atual, out Version? a))
        {
            return Array.Empty<NotaVersao>();
        }
        return lista
            .Select(n => (Nota: n, Ok: TentarVersao(n.Versao, out Version? nv), Versao: nv))
            .Where(x => x.Ok && x.Versao! > v! && x.Versao! <= a!)
            .OrderByDescending(x => x.Versao)
            .Select(x => x.Nota)
            .ToList();
    }

    /// <summary>Nota de uma versão específica (ou null).</summary>
    public static NotaVersao? DaVersao(IReadOnlyList<NotaVersao> lista, string? versao) =>
        TentarVersao(versao, out Version? alvo)
            ? lista.FirstOrDefault(n => TentarVersao(n.Versao, out Version? nv) && nv == alvo)
            : null;

    /// <summary>Nota mais nova do arquivo (ou null).</summary>
    public static NotaVersao? Ultima(IReadOnlyList<NotaVersao> lista) =>
        lista.Where(n => TentarVersao(n.Versao, out _))
            .OrderByDescending(n => { TentarVersao(n.Versao, out Version? v); return v; })
            .FirstOrDefault();

    /// <summary>Aceita "1.0.6" e "1.0.6-beta+x" (ignora o sufixo); normaliza para 4 partes para o == funcionar.</summary>
    public static bool TentarVersao(string? texto, out Version? versao)
    {
        versao = null;
        string t = (texto ?? string.Empty).Trim().TrimStart('v', 'V');
        int corte = t.IndexOfAny(new[] { '-', '+' });
        if (corte >= 0)
        {
            t = t[..corte];
        }
        if (!Version.TryParse(t, out Version? bruta))
        {
            return false;
        }
        versao = new Version(bruta.Major, bruta.Minor, Math.Max(bruta.Build, 0), Math.Max(bruta.Revision, 0));
        return true;
    }
}
