namespace CursorSuave;

/// <summary>Cada opção da aba Mouse que o modo Simples/Avançado pode esconder.</summary>
public enum CampoTela
{
    IniciarComWindows, OcultarEmCapturas,
    TamanhoAnel, CorAnel, OpacidadeAnel, AtrasoAnel,
    OpacidadeEmClicaveis, FundoEmClicaveis, ManterBolinhaDentro, EfeitoClique,
    TamanhoBolinha, MesmoVisualBolinha, CorBolinha, OpacidadesBolinha,
    MostrarBarra, AlturaBarra, LarguraBarra,
    TrocarAmpulheta, CorMarcacao, PreviaMarcacao, TamanhoMarcacao, OpacidadeMarcacao,
    ApagarParado, SegundosParado, OpacidadeParada,
    PausarTelaCheia, ListaProgramas
}

/// <summary>Tabela pura de visibilidade por modo (testável sem janela). Esconder não altera valores.</summary>
public static class ModoTela
{
    public static bool Visivel(CampoTela campo, bool avancado, bool mesmoVisualBolinha = false)
    {
        if (avancado)
        {
            return true;
        }
        return campo switch
        {
            CampoTela.OpacidadeEmClicaveis or CampoTela.FundoEmClicaveis
                or CampoTela.ManterBolinhaDentro or CampoTela.EfeitoClique => false,
            CampoTela.OpacidadesBolinha => false,
            CampoTela.AlturaBarra or CampoTela.LarguraBarra => false,
            CampoTela.TamanhoMarcacao or CampoTela.OpacidadeMarcacao => false,
            CampoTela.SegundosParado or CampoTela.OpacidadeParada => false,
            CampoTela.ListaProgramas => false,
            // A cor própria da bolinha só faz sentido quando ela não copia o anel.
            CampoTela.CorBolinha => !mesmoVisualBolinha,
            _ => true
        };
    }
}
