using System;

namespace CursorSuave;

/// <summary>
/// Lógica pura da animação do anel: diâmetro (link), escala do clique, preenchimento e opacidade.
/// Usa o mesmo lerp independente de fps do <see cref="Seguidor"/>.
/// </summary>
public sealed class EstadoAnel
{
    public const double DiametroPadrao = 26;
    public const double EscalaCliquePressionado = 0.8;
    public const double SegundosParadoPadrao = 3;
    public const double SegundosSumir = 0.3;
    public const double SegundosVoltar = 0.12;
    /// <summary>Opacidade padrão do anel "apagado" quando o mouse fica parado (não some por completo).</summary>
    public const double OpacidadeParadoPadrao = 0.3;

    private double _diametroBase;
    private double _segundosParado;
    private double _opacidadeParado = OpacidadeParadoPadrao;
    private bool _sumirParado = true;
    private double _parado;
    private double _opacidadeBorda = 0.6;
    private double _opacidadeEmLinks = 1.0;

    /// <summary>Diâmetro lógico (sem a escala do clique).</summary>
    public double Diametro { get; private set; }
    /// <summary>1 em repouso; converge para 0,8 com o botão pressionado.</summary>
    public double EscalaClique { get; private set; } = 1;
    /// <summary>0 a 1: quanto do preenchimento branco (10%) de link está ativo.</summary>
    public double Preenchimento { get; private set; }
    public double Opacidade { get; private set; } = 1;
    /// <summary>Segundos seguidos sem o mouse mexer nem clicar.</summary>
    public double SegundosSemMexer => _parado;
    /// <summary>0 a 1: quanto do realce de opacidade da borda (sobre clicável) está ativo.</summary>
    public double RealceLink { get; private set; }
    /// <summary>Alfa da borda: de <c>Opacidade</c> configurada até <c>OpacidadeEmLinks</c>.</summary>
    public double OpacidadeBorda => _opacidadeBorda + (_opacidadeEmLinks - _opacidadeBorda) * RealceLink;

    public const double SegundosEntrarCarregando = 0.12;
    public const double SegundosSairCarregando = 0.2;
    public const double GrausPorSegundoCarregando = 360;

    /// <summary>0 a 1: intensidade da marcação de carregando (sobe em 120 ms, desce em 200 ms, linear).</summary>
    public double Carregamento { get; private set; }
    /// <summary>Ângulo do arco de carregando, em graus (0 a 360, horário).</summary>
    public double AnguloCarregamento { get; private set; }

    public EstadoAnel(double diametroBase = DiametroPadrao,
        double segundosParado = SegundosParadoPadrao)
    {
        _diametroBase = diametroBase;
        _segundosParado = segundosParado;
        Diametro = diametroBase;
    }

    /// <summary>Troca os parâmetros (só pela thread do anel); os valores animados convergem aos poucos.</summary>
    public void Configurar(double diametroBase, double segundosParado, double opacidadeParado, bool sumirParado,
        double opacidadeBorda = 0.6, double opacidadeEmLinks = 1.0)
    {
        _opacidadeBorda = opacidadeBorda;
        _opacidadeEmLinks = opacidadeEmLinks;
        _diametroBase = diametroBase;
        _segundosParado = segundosParado;
        _opacidadeParado = opacidadeParado;
        _sumirParado = sumirParado;
    }

    public void Reiniciar()
    {
        Diametro = _diametroBase;
        EscalaClique = 1;
        Preenchimento = 0;
        RealceLink = 0;
        Opacidade = 1;
        _parado = 0;
        Carregamento = 0;
        AnguloCarregamento = 0;
    }

    public (double DiametroLogico, double Opacidade) Atualizar(double dt, bool mexeu, bool pressionado,
        bool sobreLink, bool cursorVisivel, bool fundoEmLinks = true,
        bool carregando = false)
    {
        dt = Math.Max(dt, 0);

        // Linear, e independente do mouse parado: quem espera um carregamento deixa o mouse quieto.
        Carregamento = carregando
            ? Math.Min(1, Carregamento + dt / SegundosEntrarCarregando)
            : Math.Max(0, Carregamento - dt / SegundosSairCarregando);
        if (Carregamento > 0)
        {
            AnguloCarregamento = (AnguloCarregamento + GrausPorSegundoCarregando * dt) % 360;
        }
        double f = 1 - Math.Pow(1 - Seguidor.SuavidadePadrao, dt * 60);

        // Sobre link o anel não cresce: só o preenchimento muda.
        double alvoDiametro = _diametroBase;
        Diametro += (alvoDiametro - Diametro) * f;
        EscalaClique += ((pressionado ? EscalaCliquePressionado : 1) - EscalaClique) * f;
        Preenchimento += ((sobreLink && fundoEmLinks ? 1 : 0) - Preenchimento) * f;
        RealceLink += ((sobreLink ? 1 : 0) - RealceLink) * f;

        if (mexeu || pressionado)
        {
            _parado = 0;
        }
        else
        {
            _parado += dt;
        }

        // cursorVisivel não esconde mais o anel (o cursor some ao digitar, o anel fica).
        if (_sumirParado && _parado >= _segundosParado)
        {
            Opacidade = Math.Max(_opacidadeParado, Opacidade - dt / SegundosSumir);
        }
        else
        {
            Opacidade = Math.Min(1, Opacidade + dt / SegundosVoltar);
        }

        return (Diametro, Opacidade);
    }
}
