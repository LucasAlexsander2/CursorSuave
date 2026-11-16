# Fase 1 — Cursor suave no Windows (app de bandeja em C#/WPF)

## Objetivo
Levar a demo web (`cursor-suave.html`) para o Windows inteiro: a bolinha é o cursor real e um anel segue o mouse com atraso suave, sem deixar a bolinha sair dele.

## Comportamento esperado
- **Bolinha:** cursor do sistema trocado por um ponto branco de 6px com contorno escuro de 1px. O ponto quente fica no centro.
- **Anel:** 26px de diâmetro (em pixels lógicos, escalado pelo DPI do monitor), borda de 2px branca com 60% de opacidade e um contorno escuro fino, para aparecer em fundo claro e escuro.
- **Seguimento:** interpolação linear (lerp) com `SUAVIDADE = 0.15` a cada quadro de 60 fps, independente da taxa de quadros. Fórmula: `f = 1 - pow(1 - 0.15, dt * 60)`.
- **Trava:** a distância entre o centro do anel e o mouse nunca passa de `raio do anel - raio da bolinha - borda` (26/2 - 6/2 - 2 = 8px lógicos). Se passar, o anel é puxado até a borda desse limite.
- **Bandeja:** ícone com o menu "Ativo" (marcável), "Pausar em tela cheia" (marcável, ligado por padrão), "Restaurar cursor padrão" e "Sair".
- **Tela cheia:** quando a janela em primeiro plano cobre o monitor inteiro e não é a área de trabalho, o anel some e o cursor original volta. Quando o jogo sai de tela cheia, tudo volta. A checagem roda a cada 500 ms.

## Decisões
- **Projeto:** .NET 10 (SDK 10.0.402 instalado), `net10.0-windows`, com `UseWPF` e `UseWindowsForms` (este só para o `NotifyIcon`).
- **Overlay:** uma janela pequena e sem bordas (64×64 px físicos) que acompanha o anel. Não é uma janela do tamanho da tela. Assim evita problemas de DPI entre monitores e gasta menos.
  - **Configuração da janela:** `AllowsTransparency=True`, `WindowStyle=None`, `Topmost`, `ShowInTaskbar=False`.
  - **Estilos estendidos:** `WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE`. Ela não recebe cliques, não rouba o foco e não aparece no Alt+Tab.
- **DPI:** manifesto com `PerMonitorV2`. Posição lida com `GetCursorPos` (pixels físicos) e janela movida com `SetWindowPos` em pixels físicos (`SWP_NOACTIVATE | SWP_NOZORDER | SWP_NOSIZE` depois de criada). O tamanho do anel usa o DPI do monitor do cursor (`MonitorFromPoint` + `GetDpiForMonitor`).
- **Laço:** `CompositionTarget.Rendering`, que fica sincronizado com a tela. Calcula `dt` com `Stopwatch`.
- **Cursor de bolinha:** gerado em código. Um arquivo `.cur` de 32×32 é escrito em `%LOCALAPPDATA%\CursorSuave\ponto.cur` e aplicado com `LoadCursorFromFile` e `SetSystemCursor`.
  - **Cursores trocados:** só `OCR_NORMAL` (32512) e `OCR_HAND` (32649). O de texto (I-beam), os de redimensionar e o de espera continuam originais, para manter a precisão.
  - **Um handle por cursor:** `SetSystemCursor` destrói o handle que recebe, então é preciso carregar um handle novo para cada cursor trocado.
- **Restaurar cursor:** `SystemParametersInfo(SPI_SETCURSORS, 0, IntPtr.Zero, 0)`.
  - **Quando restaurar:** no "Sair", no `ProcessExit`, em `DispatcherUnhandledException` e `AppDomain.UnhandledException`, ao pausar, e também na inicialização (cobre um travamento anterior).
- **Instância única:** um `Mutex` nomeado. Se o app abrir de novo, a segunda instância sai.
- **Fora desta fase:**
  - O efeito de hover (anel crescer sobre links), que não tem como ser detectado no Windows todo.
  - Iniciar com o Windows.
  - Tela de configurações.

## Arquivos
```
CursorSuave.sln
src/CursorSuave/
  CursorSuave.csproj        net10.0-windows, WPF+WinForms, app.manifest PerMonitorV2, WinExe
  app.manifest
  App.xaml / App.xaml.cs    mutex, bandeja, tratamento de exceções, restaurar ao sair
  AnelWindow.xaml(.cs)      overlay 64x64 com Ellipse; laço de render
  Seguidor.cs               lógica pura: lerp independente de fps + trava (testável)
  TelaCheia.cs              lógica pura: "retângulo da janela cobre o monitor?" + chamada Win32
  CursorSistema.cs          gera bytes .cur (função pura) e aplica/restaura cursores
  Nativo.cs                 P/Invoke (GetCursorPos, SetWindowPos, Get/SetWindowLong, SetSystemCursor,
                            LoadCursorFromFile, SystemParametersInfo, GetForegroundWindow, GetWindowRect,
                            MonitorFromWindow/Point, GetMonitorInfo, GetDpiForMonitor, GetShellWindow, GetClassName)
tests/CursorSuave.Tests/    xUnit, net10.0-windows
```
O repositório é iniciado com `git init -b main`, sem commit (o commit depende de autorização).

## Testes unitários
- **Seguidor (lerp e trava):**
  1. Mouse parado: o anel converge para o mouse.
  2. Um quadro com `dt = 1/60`: o anel anda 15% da distância.
  3. Com `dt = 1/30`, o anel anda o equivalente a 2 quadros de 60 fps (`1 - 0.85²`).
  4. Salto grande do mouse (500px): a distância final é igual ao limite de trava.
  5. Distância dentro do limite: a trava não altera nada.
  6. Distância zero: não há divisão por zero.
- **TelaCheia:**
  1. Janela igual ao monitor conta como tela cheia.
  2. Janela 1px menor não conta.
  3. Janela maior que o monitor (comum em jogos sem borda) conta.
  4. Classes `Progman` e `WorkerW` nunca contam.
- **Cursor `.cur`:**
  1. Bytes 0 a 5: `00 00 02 00 01 00`.
  2. Hotspot no centro (16,16).
  3. Tamanho declarado no cabeçalho igual ao tamanho real dos dados.

## Roteiro na tela (o Lucas verifica)
Testes na tela são feitos pelo Lucas, porque o efeito é visual e depende do mouse físico. O agente não envia teclas nem mexe no mouse.

| # | Caso | Passos | Resultado esperado |
|---|---|---|---|
| 1 | Efeito básico | Abrir o app e mexer o mouse devagar e depois rápido | O anel segue suave e a bolinha nunca sai dele; os cliques funcionam normalmente |
| 2 | Fundo claro e escuro | Passar sobre uma página branca e sobre uma escura | Os dois círculos ficam visíveis nos dois fundos |
| 3 | Tela cheia | Abrir um vídeo em tela cheia (F11 ou YouTube) | O anel some e volta o cursor normal; ao sair da tela cheia, o efeito volta |
| 4 | Sair e restaurar | Bandeja → Sair | O cursor original do Windows volta |
| 5 | Sem regressão | Usar o Alt+Tab e digitar num campo de texto | O anel não aparece no Alt+Tab e não rouba o foco; o I-beam continua normal |

## Critérios de pronto
- `dotnet build` sem erros e sem avisos.
- Todos os testes unitários passando.
- Resultados registrados abaixo.

## Resultados
- **Build:** `dotnet build` com 0 erros e 0 avisos (`TreatWarningsAsErrors` ligado nos dois projetos).
- **Testes:** 15 testes unitários (6 Seguidor, 6 TelaCheia, 3 Cursor `.cur`), 15 aprovados, 0 falhas.
- **Não verificado:** o app não foi executado; o roteiro na tela é do Lucas.
- **Desvios:**
  - `NoWarn WFO0003` no csproj: o analisador do WinForms reclama de DPI no `app.manifest`, mas o manifesto `PerMonitorV2` é exigido pelo documento (o WPF não usa `ApplicationHighDpiMode`).
  - `GetWindowLongPtr`/`SetWindowLongPtr` no lugar de `Get/SetWindowLong` (versão correta para 64 bits).
  - Os testes usam o tipo `Retangulo` próprio (sem `RECT` do Win32) para manter a lógica de `TelaCheia` pura.

## Correção 1b — anel travando (16/11/2026)
**Sintoma (Lucas):** o anel não fica suave, dá pequenos trancos.
**Causa provável:**
- O monitor é de 120 Hz e o cursor do Windows anda a 120 Hz, mas o `CompositionTarget.Rendering` do WPF fica limitado a 60 fps.
- A janela WPF com `AllowsTransparency` passa por um caminho de desenho lento a cada `SetWindowPos`.
- A posição arredondada para pixel inteiro cria degraus quando o lerp desacelera.

**Decisões:**
- Trocar a `AnelWindow` (WPF) por `AnelNativo`: uma janela Win32 criada com `CreateWindowEx` numa thread própria, atualizada com `UpdateLayeredWindow`. A posição e a imagem mudam na mesma chamada, de forma atômica.
- **Laço:** `DwmFlush()` a cada volta, sincronizado com o refresh real (120 Hz aqui). Se o DWM falhar, usa `Thread.Sleep(1)` como alternativa.
- **Subpixel:** a janela fica na parte inteira da posição, e o anel é desenhado com antialias deslocado pela parte fracionária, para não haver degraus.
- **Imagem:** DIB 32bpp pré-multiplicado, desenhado com System.Drawing (`Format32bppPArgb`, AntiAlias) e com tamanho em pixels físicos escalado pelo DPI do monitor do cursor.
- Só redesenha quando a posição muda mais de 0,01 px, e reafirma `HWND_TOPMOST` a cada 1 s.
- **Nova lógica pura testável:** `Subpixel.Dividir(pos, metade)`, que devolve a origem inteira e o deslocamento fracionário.

**Roteiro na tela (Lucas):** repetir os casos 1 e 3; o anel deve ficar tão fluido quanto o cursor.

**Resultado:** build com 0 erros e 0 avisos; testes 18/18 aprovados (3 novos de `Subpixel`). App não executado.
**Desvios:**
- O `CursorSuave.exe` (PID 7664) estava rodando e travou o `dotnet build` normal; build e testes foram feitos com `-p:OutDir` numa pasta temporária. É preciso fechar o app antes de gerar o `bin` de novo.
- `NoWarn WFO0003` mantido: ainda é necessário por causa do manifesto PerMonitorV2.
- Ao reaparecer (Mostrar), o anel é reiniciado na posição do mouse (sem lerp), como na versão WPF.
