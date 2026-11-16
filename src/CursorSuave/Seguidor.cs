using System;

namespace CursorSuave;

/// <summary>Lógica pura: lerp independente de fps + trava de distância.</summary>
public sealed class Seguidor
{
    public const double SuavidadePadrao = 0.15;

    public double X { get; private set; }
    public double Y { get; private set; }
    /// <summary>Constante de tempo τ do anel, em segundos (0 = colado). Só pela thread do anel.</summary>
    public double AtrasoSegundos { get; set; }
    /// <summary>Com true, o anel é puxado para manter a bolinha dentro dele.</summary>
    public bool ManterDentro { get; set; } = true;

    public Seguidor(double atrasoSegundos = 0.1, double x = 0, double y = 0)
    {
        AtrasoSegundos = atrasoSegundos;
        X = x;
        Y = y;
    }

    public void Reiniciar(double x, double y)
    {
        X = x;
        Y = y;
    }

    /// <param name="mouseX">Posição do mouse.</param>
    /// <param name="mouseY">Posição do mouse.</param>
    /// <param name="dt">Segundos desde o quadro anterior.</param>
    /// <param name="limite">Distância máxima entre o centro do anel e o mouse (mesma unidade das posições).</param>
    public void Atualizar(double mouseX, double mouseY, double dt, double limite)
    {
        double f = AtrasoSegundos <= 0 ? 1 : 1 - Math.Exp(-Math.Max(dt, 0) / AtrasoSegundos);
        X += (mouseX - X) * f;
        Y += (mouseY - Y) * f;

        double dx = X - mouseX;
        double dy = Y - mouseY;
        double d = Math.Sqrt(dx * dx + dy * dy);
        if (ManterDentro && d > limite && d > 0)
        {
            double k = limite / d;
            X = mouseX + dx * k;
            Y = mouseY + dy * k;
        }
    }
}
