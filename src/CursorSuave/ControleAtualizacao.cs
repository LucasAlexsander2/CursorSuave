using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace CursorSuave;

public enum EstadoAtualizacao
{
    Inicial,
    Verificando,
    SemNovidade,
    NovaVersao,
    Baixando,
    Pronto,
    Erro,
    NaoInstalado
}

/// <summary>Retrato do estado da atualização, lido pela janela e pelo menu.</summary>
public sealed record SituacaoAtualizacao(
    EstadoAtualizacao Estado, string? VersaoNova, int Percentual, double? SegundosRestantes, string? Erro);

/// <summary>
/// Guarda o estado da atualização num só lugar (um download por vez). A janela de Configurações
/// só assina <see cref="Mudou"/> e lê <see cref="Situacao"/>; fechar a janela não interrompe o download.
/// </summary>
public sealed class ControleAtualizacao
{
    private readonly IFonteAtualizacao _atualizador;
    private readonly Action _antesDeReiniciar;
    private EstimativaDownload _estimativa = new EstimativaDownload();
    private readonly Stopwatch _relogio = new Stopwatch();
    private SituacaoAtualizacao _situacao;

    public ControleAtualizacao(IFonteAtualizacao atualizador, Action antesDeReiniciar)
    {
        _atualizador = atualizador;
        _antesDeReiniciar = antesDeReiniciar;
        _situacao = new SituacaoAtualizacao(
            atualizador.Instalado ? EstadoAtualizacao.Inicial : EstadoAtualizacao.NaoInstalado, null, 0, null, null);
    }

    public SituacaoAtualizacao Situacao => _situacao;
    public DateTime? UltimaVerificacao { get; private set; }
    public bool Ocupado => _situacao.Estado is EstadoAtualizacao.Verificando or EstadoAtualizacao.Baixando;

    /// <summary>Qualquer mudança de estado ou de progresso (sempre na thread da interface).</summary>
    public event Action? Mudou;

    /// <summary>Fim de uma verificação (resultado e se foi pedida pelo usuário), para os balões.</summary>
    public event Action<ResultadoVerificacao, bool>? Verificado;

    private void Definir(SituacaoAtualizacao nova)
    {
        _situacao = nova;
        Mudou?.Invoke();
    }

    public async Task VerificarAsync(bool manual)
    {
        // Pronto: já há versão baixada esperando o reinício; verificar de novo só a apagaria da tela.
        if (Ocupado || !_atualizador.Instalado || _situacao.Estado == EstadoAtualizacao.Pronto)
        {
            return;
        }
        Definir(_situacao with { Estado = EstadoAtualizacao.Verificando, Erro = null });
        ResultadoVerificacao r = await _atualizador.VerificarAsync();
        UltimaVerificacao = DateTime.UtcNow;
        _situacao = r switch
        {
            ResultadoVerificacao.NovaVersao => new SituacaoAtualizacao(
                EstadoAtualizacao.NovaVersao, _atualizador.VersaoNova, 0, null, null),
            ResultadoVerificacao.Erro => new SituacaoAtualizacao(
                EstadoAtualizacao.Erro, null, 0, null, Resumir(_atualizador.UltimoErro)),
            _ => new SituacaoAtualizacao(EstadoAtualizacao.SemNovidade, null, 0, null, null)
        };
        Mudou?.Invoke();
        Verificado?.Invoke(r, manual);
    }

    public async Task BaixarAsync()
    {
        if (_situacao.Estado != EstadoAtualizacao.NovaVersao || _atualizador.VersaoNova is null)
        {
            return;
        }
        _estimativa = new EstimativaDownload();
        _relogio.Restart();
        Definir(_situacao with { Estado = EstadoAtualizacao.Baixando, Percentual = 0, SegundosRestantes = null, Erro = null });
        Dispatcher? ui = Application.Current?.Dispatcher;
        bool ok = await _atualizador.BaixarAsync(
            p =>
            {
                void Aplicar()
                {
                    // CUIDADO: um progresso atrasado (BeginInvoke) pode chegar depois do fim do download.
                    if (_situacao.Estado != EstadoAtualizacao.Baixando)
                    {
                        return;
                    }
                    double? restante = _estimativa.Registrar(p, _relogio.Elapsed);
                    Definir(_situacao with { Percentual = p, SegundosRestantes = restante });
                }
                // CUIDADO: o Velopack pode chamar o progresso fora da thread da interface.
                if (ui is null || ui.CheckAccess())
                {
                    Aplicar();
                }
                else
                {
                    ui.BeginInvoke(Aplicar);
                }
            });
        if (ok)
        {
            // Baixou: fica esperando o usuário reiniciar (ou a aplicação ao sair / na próxima abertura).
            Definir(_situacao with { Estado = EstadoAtualizacao.Pronto, Percentual = 100, SegundosRestantes = null, Erro = null });
        }
        else
        {
            // Falhou: o app continua rodando e volta a "versão nova" com a mensagem.
            Definir(_situacao with
            {
                Estado = EstadoAtualizacao.NovaVersao, Percentual = 0, SegundosRestantes = null,
                Erro = Resumir(_atualizador.UltimoErro)
            });
        }
    }

    /// <summary>Aplica a versão baixada e reinicia. Se falhar, continua em Pronto com a mensagem.</summary>
    public void Reiniciar()
    {
        if (_situacao.Estado != EstadoAtualizacao.Pronto)
        {
            return;
        }
        if (!_atualizador.AplicarEReiniciar(_antesDeReiniciar))
        {
            Definir(_situacao with { Erro = Resumir(_atualizador.UltimoErro) });
        }
    }

    /// <summary>Chamado no "Sair": com versão baixada, agenda a aplicação para o fim do processo.</summary>
    public void AplicarAoSair()
    {
        if (_situacao.Estado == EstadoAtualizacao.Pronto)
        {
            _atualizador.AplicarAoSair();
        }
    }

    public static string Resumir(string? mensagem)
    {
        string m = (mensagem ?? "erro desconhecido").Replace('\r', ' ').Replace('\n', ' ').Trim();
        return m.Length <= 80 ? m : m[..77] + "...";
    }
}
