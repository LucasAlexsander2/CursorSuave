using System;
using System.Reflection;
using System.Threading.Tasks;
using Velopack;
using Velopack.Sources;

namespace CursorSuave;

/// <summary>Resultado de uma verificação de atualização.</summary>
public enum ResultadoVerificacao
{
    SemInstalacao,
    SemNovidade,
    NovaVersao,
    Erro
}

/// <summary>
/// Envolve o Velopack: verifica o GitHub Releases, baixa e aplica a atualização.
/// Fora da versão instalada (rodando do bin) tudo fica desligado.
/// </summary>
public sealed class Atualizador
{
    public const string Repositorio = "https://github.com/LucasAlexsander2/CursorSuave";
    public static readonly TimeSpan Intervalo = TimeSpan.FromHours(6);

    private readonly UpdateManager? _gerenciador;
    private UpdateInfo? _pendente;

    public Atualizador()
    {
        try
        {
            _gerenciador = new UpdateManager(new GithubSource(Repositorio, null, false));
        }
        catch (Exception)
        {
            _gerenciador = null; // sem instalação do Velopack
        }
    }

    /// <summary>Lógica pura: verificar se está ligado e nunca verificou ou já passaram 6 h ou mais.</summary>
    public static bool DeveVerificar(DateTime agora, DateTime? ultimaVerificacao, bool ligado) =>
        ligado && (ultimaVerificacao is null || agora - ultimaVerificacao.Value >= Intervalo);

    public bool Instalado => _gerenciador?.IsInstalled == true;

    /// <summary>Versão em execução: a instalada (Velopack) ou a do assembly.</summary>
    public string VersaoAtual
    {
        get
        {
            string? v = _gerenciador?.CurrentVersion?.ToString();
            if (!string.IsNullOrEmpty(v))
            {
                return v;
            }
            string? info = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            int mais = info?.IndexOf('+') ?? -1;
            return string.IsNullOrEmpty(info) ? "1.0.0" : mais >= 0 ? info[..mais] : info;
        }
    }

    /// <summary>Versão nova encontrada na última verificação (ou null).</summary>
    public string? VersaoNova => _pendente?.TargetFullRelease.Version.ToString();

    /// <summary>Mensagem curta do último erro.</summary>
    public string? UltimoErro { get; private set; }

    public async Task<ResultadoVerificacao> VerificarAsync()
    {
        if (_gerenciador is null || !_gerenciador.IsInstalled)
        {
            return ResultadoVerificacao.SemInstalacao;
        }
        try
        {
            UpdateInfo? info = await _gerenciador.CheckForUpdatesAsync().ConfigureAwait(true);
            _pendente = info;
            UltimoErro = null;
            return info is null ? ResultadoVerificacao.SemNovidade : ResultadoVerificacao.NovaVersao;
        }
        catch (Exception ex)
        {
            UltimoErro = ex.Message;
            return ResultadoVerificacao.Erro;
        }
    }

    /// <summary>Baixa a versão encontrada e reinicia o app com ela. Só chamar após o clique do usuário.</summary>
    public async Task<bool> BaixarEAplicarAsync(Action<int> progresso, Action antesDeReiniciar)
    {
        if (_gerenciador is null || _pendente is null)
        {
            return false;
        }
        try
        {
            await _gerenciador.DownloadUpdatesAsync(_pendente, progresso).ConfigureAwait(true);
            antesDeReiniciar();
            _gerenciador.ApplyUpdatesAndRestart(_pendente.TargetFullRelease);
            return true;
        }
        catch (Exception ex)
        {
            UltimoErro = ex.Message;
            return false;
        }
    }
}
