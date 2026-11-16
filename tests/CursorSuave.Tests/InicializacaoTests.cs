using CursorSuave;

namespace CursorSuave.Tests;

public class InicializacaoTests
{
    [Fact]
    public void MontarValor_CaminhoSaiEntreAspas()
    {
        string valor = Inicializacao.MontarValor(@"C:\Programas\Cursor Suave\CursorSuave.exe");
        Assert.Equal("\"" + @"C:\Programas\Cursor Suave\CursorSuave.exe" + "\"", valor);
    }
}

public class InicializacaoHookTests
{
    [Fact]
    public void ComValorExistente_ApontaParaOExeAtual()
    {
        string? novo = Inicializacao.NovoValorAposAtualizacao(Inicializacao.MontarValor(@"C:\Antigo\CursorSuave.exe"), @"C:\Novo\CursorSuave.exe");
        Assert.Equal(Inicializacao.MontarValor(@"C:\Novo\CursorSuave.exe"), novo);
    }

    [Fact]
    public void SemValor_DevolveNull() =>
        Assert.Null(Inicializacao.NovoValorAposAtualizacao(null, @"C:\Novo\CursorSuave.exe"));
}
