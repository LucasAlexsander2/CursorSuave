using CursorSuave;

namespace CursorSuave.Tests;

public class SuspensaoTests
{
    private static readonly string[] Vazia = Array.Empty<string>();

    [Theory]
    [InlineData("StartMenuExperienceHost", "Qualquer")]
    [InlineData("SearchHost", "Qualquer")]
    [InlineData("explorer", "MultitaskingViewFrame")]
    [InlineData("ShellExperienceHost", "ForegroundStaging")]
    public void MenuDoShell_NaoSuspende(string processo, string classe) =>
        Assert.Equal(MotivoSuspensao.Nenhum, Suspensao.Decidir(classe, processo, false, true, Vazia));

    [Fact]
    public void TelaCheia_ComOpcaoLigada_Suspende() =>
        Assert.Equal(MotivoSuspensao.TelaCheia, Suspensao.Decidir("Jogo", "jogo", true, true, Vazia));

    [Fact]
    public void TelaCheia_ComOpcaoDesligada_NaoSuspende() =>
        Assert.Equal(MotivoSuspensao.Nenhum, Suspensao.Decidir("Jogo", "jogo", true, false, Vazia));

    [Theory]
    [InlineData("Photoshop")]
    [InlineData("photoshop.exe")]
    public void ProgramaDaListaDePausados_Suspende(string processo) =>
        Assert.Equal(MotivoSuspensao.ProgramaPausado,
            Suspensao.Decidir("Photoshop", processo, false, true, new[] { "Photoshop" }));

    [Fact]
    public void JanelaComum_NaoSuspende() =>
        Assert.Equal(MotivoSuspensao.Nenhum, Suspensao.Decidir("Chrome_WidgetWin_1", "chrome", false, true, new[] { "Photoshop" }));
}
