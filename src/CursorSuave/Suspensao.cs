using System;
using System.Collections.Generic;

namespace CursorSuave;

public enum MotivoSuspensao
{
    Nenhum,
    TelaCheia,
    ProgramaPausado
}

/// <summary>Lógica pura: decide se o anel e a bolinha devem ser suspensos.</summary>
public static class Suspensao
{
    public static MotivoSuspensao Decidir(string? classe, string? processo, bool telaCheia,
        bool pausarTelaCheia, IReadOnlyCollection<string> programasPausados)
    {
        string proc = Normalizar(processo);
        if (proc.Length > 0)
        {
            foreach (string p in programasPausados)
            {
                if (string.Equals(Normalizar(p), proc, StringComparison.OrdinalIgnoreCase))
                {
                    return MotivoSuspensao.ProgramaPausado;
                }
            }
        }

        return pausarTelaCheia && telaCheia ? MotivoSuspensao.TelaCheia : MotivoSuspensao.Nenhum;
    }

    private static string Normalizar(string? nome)
    {
        string n = (nome ?? string.Empty).Trim();
        return n.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? n[..^4] : n;
    }
}
