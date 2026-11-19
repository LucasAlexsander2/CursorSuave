using CursorSuave;

namespace CursorSuave.Tests;

public class SessaoTests
{
    [Fact]
    public void ArquivoAusente_NaoRestaura() => Assert.False(Sessao.PrecisaRestaurar(false, false));

    [Fact]
    public void ArquivoComPidMorto_Restaura() => Assert.True(Sessao.PrecisaRestaurar(true, false));

    [Fact]
    public void ArquivoComPidVivo_NaoRestaura() => Assert.False(Sessao.PrecisaRestaurar(true, true));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [InlineData(null)]
    public void GravarELer_MantemVanishOriginal(bool? vanish)
    {
        string pasta = Path.Combine(Path.GetTempPath(), "cs_sessao_" + Guid.NewGuid().ToString("N"));
        string caminho = Path.Combine(pasta, "sessao.json");
        try
        {
            Sessao.Gravar(caminho, new Sessao(1234, vanish));
            Assert.True(Sessao.Existe(caminho));
            Sessao? lida = Sessao.Ler(caminho);
            Assert.Equal(new Sessao(1234, vanish), lida);
            Sessao.Apagar(caminho);
            Assert.False(Sessao.Existe(caminho));
            Assert.Null(Sessao.Ler(caminho));
        }
        finally
        {
            if (Directory.Exists(pasta))
            {
                Directory.Delete(pasta, true);
            }
        }
    }

    [Fact]
    public void ArquivoCorrompido_LeNullMasContinuaExistindo()
    {
        string caminho = Path.Combine(Path.GetTempPath(), "cs_sessao_ruim_" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            File.WriteAllText(caminho, "{ isto não é json");
            Assert.True(Sessao.Existe(caminho));
            Assert.Null(Sessao.Ler(caminho));
        }
        finally
        {
            File.Delete(caminho);
        }
    }

    [Fact]
    public void PidRodando_ReconheceOProcessoAtualEUmPidInexistente()
    {
        Assert.True(Sessao.PidRodando(Environment.ProcessId));
        Assert.False(Sessao.PidRodando(int.MaxValue));
    }

    [Fact]
    public void Guardiao_LeArgumentos()
    {
        Assert.True(Guardiao.TentarLerArgumentos(new[] { "--guardiao", "42" }, out int pid));
        Assert.Equal(42, pid);
        Assert.False(Guardiao.TentarLerArgumentos(new[] { "--guardiao" }, out _));
        Assert.False(Guardiao.TentarLerArgumentos(new[] { "--guardiao", "x" }, out _));
        Assert.False(Guardiao.TentarLerArgumentos(Array.Empty<string>(), out _));
    }
}
