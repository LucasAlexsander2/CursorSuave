using CursorSuave;

namespace CursorSuave.Tests;

public class NovidadesTests
{
    private const string Json = """
    [
      { "versao": "1.0.6", "itens": [ { "icone": "E8A9", "titulo": "A", "texto": "a" } ] },
      { "versao": "1.0.8", "itens": [ { "icone": "E713", "titulo": "B", "texto": "b" } ] },
      { "versao": "1.0.7", "itens": [] }
    ]
    """;

    [Fact]
    public void Ler_LeOJson()
    {
        var lista = Novidades.Ler(Json);
        Assert.Equal(3, lista.Count);
        Assert.Equal("1.0.6", lista[0].Versao);
        Assert.Equal("E8A9", lista[0].Itens[0].Icone);
        Assert.Equal("A", lista[0].Itens[0].Titulo);
        Assert.Empty(Novidades.Ler("não é json"));
    }

    [Fact]
    public void Pendentes_DaVistaParaAtual_DevolveUmaNota()
    {
        var p = Novidades.Pendentes(Novidades.Ler(Json), "1.0.5", "1.0.6");
        Assert.Single(p);
        Assert.Equal("1.0.6", p[0].Versao);
    }

    [Fact]
    public void Pendentes_VistaIgualAAtual_NaoDevolveNada()
    {
        Assert.Empty(Novidades.Pendentes(Novidades.Ler(Json), "1.0.6", "1.0.6"));
        Assert.Empty(Novidades.Pendentes(Novidades.Ler(Json), "1.0.8", "1.0.6"));
    }

    [Fact]
    public void Pendentes_PuloDeVersoes_JuntaDaMaisNovaParaAMaisVelha()
    {
        var p = Novidades.Pendentes(Novidades.Ler(Json), "1.0.5", "1.0.8");
        Assert.Equal(new[] { "1.0.8", "1.0.7", "1.0.6" }, p.Select(n => n.Versao));
        // Compara como versão, não como texto: 1.0.10 vem depois de 1.0.9.
        var dez = Novidades.Ler("[{\"versao\":\"1.0.9\",\"itens\":[]},{\"versao\":\"1.0.10\",\"itens\":[]}]");
        Assert.Equal("1.0.10", Novidades.Pendentes(dez, "1.0.8", "1.0.10")[0].Versao);
    }

    [Fact]
    public void Pendentes_VersaoInvalida_NaoDevolveNada()
    {
        var lista = Novidades.Ler(Json);
        Assert.Empty(Novidades.Pendentes(lista, "abc", "1.0.6"));
        Assert.Empty(Novidades.Pendentes(lista, "1.0.5", ""));
        Assert.Empty(Novidades.Pendentes(lista, null, null));
    }

    [Fact]
    public void DaVersao_AchaANotaOuNull()
    {
        var lista = Novidades.Ler(Json);
        Assert.Equal("1.0.8", Novidades.DaVersao(lista, "1.0.8")!.Versao);
        Assert.Null(Novidades.DaVersao(lista, "1.0.9"));
        Assert.Equal("1.0.8", Novidades.Ultima(lista)!.Versao);
    }

    [Fact]
    public void RecursoEmbutido_TemEntradaPara106ComQuatroItens()
    {
        var nota = Novidades.DaVersao(Novidades.LerEmbutido(), "1.0.6");
        Assert.NotNull(nota);
        Assert.Equal(4, nota!.Itens.Count);
        Assert.All(nota.Itens, i => Assert.False(string.IsNullOrWhiteSpace(i.Texto)));
    }
}
