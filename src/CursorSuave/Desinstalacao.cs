using System;
using System.IO;
using System.Security;
using Microsoft.Win32;

namespace CursorSuave;

/// <summary>Limpeza feita pelo hook de desinstalação. A configuração em %APPDATA% é mantida.</summary>
public static class Desinstalacao
{
    public static void Preparar()
    {
        try
        {
            // Também devolve "Ocultar ponteiro ao digitar" se este processo o tiver guardado.
            CursorSistema.Restaurar();
            Inicializacao.Definir(false, string.Empty);
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException)
        {
            // Hook rápido: nunca deve travar a desinstalação.
        }
    }
}
