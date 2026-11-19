using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace CursorSuave;

/// <summary>
/// Configuração imutável. A thread do anel recebe uma nova instância por referência,
/// nunca campos compartilhados mutáveis.
/// </summary>
public readonly record struct VisualDaBolinha(string Cor, double Opacidade, double OpacidadeEmLinks, double OpacidadeParado);

public sealed record Configuracao
{
    public const string CorPadrao = "#FFFFFF";

    public int TamanhoAnel { get; init; } = 25;
    public int TamanhoBolinha { get; init; } = 5;
    public string Cor { get; init; } = CorPadrao;
    public double Opacidade { get; init; } = 0.6;
    public double OpacidadeEmLinks { get; init; } = 1.0;
    public bool BarraEmTexto { get; init; } = true;
    public int AlturaBarra { get; init; } = 14;
    public int LarguraBarra { get; init; } = 1;
    public int AtrasoMs { get; init; } = 100;
    public bool ManterBolinhaDentro { get; init; } = true;
    public bool FundoEmLinks { get; init; } = true;
    public bool EfeitoClique { get; init; } = true;
    public bool SumirParado { get; init; } = false;
    public bool PausarTelaCheia { get; init; } = false;
    public bool VerificarAtualizacoes { get; init; } = true;
    public double SegundosParado { get; init; } = 3;
    public double OpacidadeParado { get; init; } = 0.3;
    public double OpacidadeBolinha { get; init; } = 1.0;
    public double OpacidadeBolinhaEmLinks { get; init; } = 1.0;
    public double OpacidadeBolinhaParado { get; init; } = 1.0;
    public string CorBolinha { get; init; } = CorPadrao;
    public bool MesmoVisualBolinhaAnel { get; init; } = false;
    public IReadOnlyList<string> ProgramasPausados { get; init; } = Array.Empty<string>();

    private static readonly JsonSerializerOptions Opcoes = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    /// <summary>Limita os valores às faixas válidas e corrige a cor e a lista.</summary>
    public Configuracao Validar()
    {
        string[] programas = (ProgramasPausados ?? Array.Empty<string>())
            .Select(p => (p ?? string.Empty).Trim())
            .Select(p => p.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? p[..^4] : p)
            .Where(p => p.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return this with
        {
            TamanhoAnel = Math.Clamp(TamanhoAnel, 16, 60),
            TamanhoBolinha = Math.Clamp(TamanhoBolinha, 4, 12),
            AlturaBarra = Math.Clamp(AlturaBarra, 8, 28),
            LarguraBarra = Math.Clamp(LarguraBarra, 1, 6),
            Cor = TentarLerCor(Cor, out _, out _, out _) ? NormalizarCor(Cor) : CorPadrao,
            Opacidade = Limitar(Opacidade, 0.2, 1, 0.6),
            OpacidadeEmLinks = Limitar(OpacidadeEmLinks, 0.2, 1, 1.0),
            AtrasoMs = Math.Clamp(AtrasoMs, 0, 500),
            SegundosParado = Limitar(SegundosParado, 1, 30, 3),
            OpacidadeParado = Limitar(OpacidadeParado, 0, 0.9, 0.3),
            OpacidadeBolinha = Limitar(OpacidadeBolinha, MinOpacidadeBolinha, 1, 1.0),
            OpacidadeBolinhaEmLinks = Limitar(OpacidadeBolinhaEmLinks, MinOpacidadeBolinha, 1, 1.0),
            OpacidadeBolinhaParado = Limitar(OpacidadeBolinhaParado, MinOpacidadeBolinha, 1, 1.0),
            CorBolinha = TentarLerCor(CorBolinha, out _, out _, out _) ? NormalizarCor(CorBolinha) : CorPadrao,
            ProgramasPausados = programas
        };
    }

    /// <summary>Padrão em todos os campos de aparência e comportamento; mantém a lista de programas pausados e VerificarAtualizacoes.</summary>
    public Configuracao ComPadroesVisuais() => new Configuracao
    {
        ProgramasPausados = ProgramasPausados,
        VerificarAtualizacoes = VerificarAtualizacoes
    };

    public const double MinOpacidadeBolinha = 0.1;

    /// <summary>Visual efetivo da bolinha (e da barra): do anel se MesmoVisualBolinhaAnel, senão os campos próprios.</summary>
    public VisualDaBolinha VisualBolinha()
    {
        if (MesmoVisualBolinhaAnel)
        {
            string cor = TentarLerCor(Cor, out _, out _, out _) ? NormalizarCor(Cor) : CorPadrao;
            return new VisualDaBolinha(cor,
                Limitar(Opacidade, MinOpacidadeBolinha, 1, 1.0),
                Limitar(OpacidadeEmLinks, MinOpacidadeBolinha, 1, 1.0),
                Limitar(OpacidadeParado, MinOpacidadeBolinha, 1, 1.0));
        }
        string c = TentarLerCor(CorBolinha, out _, out _, out _) ? NormalizarCor(CorBolinha) : CorPadrao;
        return new VisualDaBolinha(c,
            Limitar(OpacidadeBolinha, MinOpacidadeBolinha, 1, 1.0),
            Limitar(OpacidadeBolinhaEmLinks, MinOpacidadeBolinha, 1, 1.0),
            Limitar(OpacidadeBolinhaParado, MinOpacidadeBolinha, 1, 1.0));
    }

    private static double Limitar(double v, double min, double max, double padrao) =>
        double.IsNaN(v) || double.IsInfinity(v) ? padrao : Math.Clamp(v, min, max);

    /// <summary>
    /// Lê a configuração do texto JSON. Texto vazio dá o padrão; JSON inválido dá o padrão
    /// com <paramref name="corrompido"/> = true (quem chama faz o backup).
    /// </summary>
    public static Configuracao Carregar(string? texto, out bool corrompido)
    {
        corrompido = false;
        if (string.IsNullOrWhiteSpace(texto))
        {
            return new Configuracao();
        }
        try
        {
            Configuracao? c = JsonSerializer.Deserialize<Configuracao>(texto, Opcoes);
            if (c is null)
            {
                corrompido = true;
                return new Configuracao();
            }
            return MigrarSuavidade(texto, c).Validar();
        }
        catch (JsonException)
        {
            corrompido = true;
            return new Configuracao();
        }
    }

    /// <summary>JSON antigo com "Suavidade" e sem "AtrasoMs": converte para milissegundos.</summary>
    private static Configuracao MigrarSuavidade(string texto, Configuracao c)
    {
        using JsonDocument doc = JsonDocument.Parse(texto, new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        });
        if (doc.RootElement.ValueKind != JsonValueKind.Object)
        {
            return c;
        }
        bool temAtraso = false;
        double? suavidade = null;
        foreach (JsonProperty p in doc.RootElement.EnumerateObject())
        {
            if (p.Name.Equals("AtrasoMs", StringComparison.OrdinalIgnoreCase))
            {
                temAtraso = true;
            }
            else if (p.Name.Equals("Suavidade", StringComparison.OrdinalIgnoreCase) &&
                     p.Value.ValueKind == JsonValueKind.Number)
            {
                suavidade = p.Value.GetDouble();
            }
        }
        if (temAtraso || suavidade is not double s)
        {
            return c;
        }
        s = Math.Clamp(s, 0.05, 0.5);
        int ms = (int)Math.Round(-1000 / (60 * Math.Log(1 - s)));
        return c with { AtrasoMs = Math.Clamp(ms, 0, 500) };
    }

    /// <summary>Texto JSON (já validado) para gravar no arquivo.</summary>
    public string Salvar() => JsonSerializer.Serialize(Validar(), Opcoes);

    public static string Caminho => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CursorSuave", "config.json");

    /// <summary>Lê o arquivo; se estiver corrompido, vira config.json.bak e volta o padrão.</summary>
    public static Configuracao CarregarDoDisco()
    {
        try
        {
            string caminho = Caminho;
            if (!File.Exists(caminho))
            {
                return new Configuracao();
            }
            Configuracao c = Carregar(File.ReadAllText(caminho), out bool corrompido);
            if (corrompido)
            {
                File.Move(caminho, caminho + ".bak", true);
            }
            return c;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new Configuracao();
        }
    }

    public void SalvarNoDisco()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Caminho)!);
            File.WriteAllText(Caminho, Salvar());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Sem permissão de escrita: a configuração vale só nesta sessão.
        }
    }

    /// <summary>Aceita "#RGB" ou "#RRGGBB" (o # é opcional).</summary>
    public static bool TentarLerCor(string? hex, out byte r, out byte g, out byte b)
    {
        r = g = b = 255;
        string h = (hex ?? string.Empty).Trim().TrimStart('#');
        if (h.Length == 3)
        {
            h = string.Concat(h.Select(c => new string(c, 2)));
        }
        if (h.Length != 6 || !int.TryParse(h, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out int v))
        {
            return false;
        }
        r = (byte)(v >> 16);
        g = (byte)(v >> 8);
        b = (byte)v;
        return true;
    }

    private static string NormalizarCor(string hex)
    {
        TentarLerCor(hex, out byte r, out byte g, out byte b);
        return $"#{r:X2}{g:X2}{b:X2}";
    }
}
