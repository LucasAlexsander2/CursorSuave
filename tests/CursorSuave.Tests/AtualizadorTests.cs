using CursorSuave;

namespace CursorSuave.Tests;

public class AtualizadorTests
{
    private static readonly DateTime Agora = new(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void NuncaVerificou_Verifica() =>
        Assert.True(Atualizador.DeveVerificar(Agora, null, true));

    [Fact]
    public void HaMenosDeSeisHoras_NaoVerifica() =>
        Assert.False(Atualizador.DeveVerificar(Agora, Agora.AddHours(-5).AddMinutes(-59), true));

    [Fact]
    public void HaSeisHorasOuMais_Verifica()
    {
        Assert.True(Atualizador.DeveVerificar(Agora, Agora.AddHours(-6), true));
        Assert.True(Atualizador.DeveVerificar(Agora, Agora.AddHours(-20), true));
    }

    [Fact]
    public void Desligado_NaoVerifica()
    {
        Assert.False(Atualizador.DeveVerificar(Agora, null, false));
        Assert.False(Atualizador.DeveVerificar(Agora, Agora.AddDays(-2), false));
    }
}
