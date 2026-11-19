using System;
using Velopack;

namespace CursorSuave;

/// <summary>Ponto de entrada: o Velopack trata os hooks de instalação antes de qualquer interface.</summary>
public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Os hooks rodam rápido e sem interface; depois de executá-los o Velopack encerra o processo.
        VelopackApp.Build()
            .OnFirstRun(_ => Inicializacao.CorrigirCaminhoAtual())
            .OnAfterUpdateFastCallback(_ => Inicializacao.CorrigirCaminhoAtual())
            .OnBeforeUninstallFastCallback(_ => Desinstalacao.Preparar())
            .Run();

        // Modo guardião: sem mutex, janela ou bandeja; só vigia o processo principal.
        if (Guardiao.TentarLerArgumentos(args, out int pidPrincipal))
        {
            Guardiao.Executar(pidPrincipal);
            return;
        }

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
