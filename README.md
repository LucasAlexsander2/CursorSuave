# Cursor Suave

Aplicativo de bandeja para Windows que desenha um anel suave e uma bolinha no lugar do cursor do mouse. Feito em C#/WPF (.NET 10).

## Instalar
1. Baixe o `CursorSuave-win-Setup.exe` na página de [Releases](https://github.com/LucasAlexsander2/CursorSuave/releases).
2. Execute. O Windows SmartScreen pode avisar, pois o instalador não é assinado: clique em "Mais informações" e depois em "Executar assim mesmo".
3. Não precisa de administrador nem do .NET. Instala em `%LOCALAPPDATA%\CursorSuave` e cria o atalho no Menu Iniciar.

O app confere o GitHub a cada 6 horas e avisa na bandeja quando há versão nova; só atualiza depois do seu clique.

## Desinstalar
Configurações do Windows, Aplicativos. O cursor original volta e o início com o Windows é desligado. A configuração em `%APPDATA%\CursorSuave` é mantida.

## Desenvolver
```
dotnet build
dotnet test
```

## Publicar uma versão
Crie e envie uma tag: `git tag v1.2.3` e `git push origin v1.2.3`. O GitHub Actions roda os testes, empacota com o Velopack e publica a release.
