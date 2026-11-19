using System.Threading.Tasks;
using CursorSuave;

namespace CursorSuave.Tests;

public class ControleAtualizacaoTests
{
    private sealed class Falso : IFonteAtualizacao
    {
        public bool Instalado { get; set; } = true;
        public string? VersaoNova { get; set; } = "1.0.7";
        public string? UltimoErro { get; set; }
        public bool BaixaOk { get; set; } = true;
        public bool ReinicioOk { get; set; } = true;
        public int Reinicios, AoSair;
        public Task<ResultadoVerificacao> VerificarAsync() => Task.FromResult(ResultadoVerificacao.NovaVersao);
        public Task<bool> BaixarAsync(Action<int> progresso)
        {
            progresso(50);
            return Task.FromResult(BaixaOk);
        }
        public bool AplicarEReiniciar(Action antesDeReiniciar)
        {
            Reinicios++;
            return ReinicioOk;
        }
        public void AplicarAoSair() => AoSair++;
    }

    private static async Task<(ControleAtualizacao, Falso)> ComNovaVersao()
    {
        var f = new Falso();
        var c = new ControleAtualizacao(f, () => { });
        await c.VerificarAsync(false);
        return (c, f);
    }

    [Fact]
    public async Task DepoisDoDownload_VaiParaPronto_SemReiniciar()
    {
        var (c, f) = await ComNovaVersao();
        await c.BaixarAsync();
        Assert.Equal(EstadoAtualizacao.Pronto, c.Situacao.Estado);
        Assert.Equal("1.0.7", c.Situacao.VersaoNova);
        Assert.Equal(0, f.Reinicios);
    }

    [Fact]
    public async Task FalhaNoDownload_VoltaParaNovaVersaoComErro()
    {
        var (c, f) = await ComNovaVersao();
        f.BaixaOk = false;
        f.UltimoErro = "sem rede";
        await c.BaixarAsync();
        Assert.Equal(EstadoAtualizacao.NovaVersao, c.Situacao.Estado);
        Assert.Equal("sem rede", c.Situacao.Erro);
    }

    [Fact]
    public async Task Reiniciar_SoComPronto_EFalhaMantemPronto()
    {
        var (c, f) = await ComNovaVersao();
        c.Reiniciar();
        Assert.Equal(0, f.Reinicios); // ainda NovaVersao: nada a reiniciar
        await c.BaixarAsync();
        f.ReinicioOk = false;
        f.UltimoErro = "falhou";
        c.Reiniciar();
        Assert.Equal(1, f.Reinicios);
        Assert.Equal(EstadoAtualizacao.Pronto, c.Situacao.Estado);
        Assert.Equal("falhou", c.Situacao.Erro);
    }

    [Fact]
    public async Task Pronto_NaoEhApagadoPorNovaVerificacao_EAplicaAoSair()
    {
        var (c, f) = await ComNovaVersao();
        c.AplicarAoSair();
        Assert.Equal(0, f.AoSair); // sem download, nada a aplicar
        await c.BaixarAsync();
        await c.VerificarAsync(true);
        Assert.Equal(EstadoAtualizacao.Pronto, c.Situacao.Estado);
        c.AplicarAoSair();
        Assert.Equal(1, f.AoSair);
    }
}
