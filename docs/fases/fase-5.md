# Fase 5 — Menu da bandeja moderno, marcação de carregando no anel e atualizar pela tela

A fase tem três partes independentes. Cada parte vai para um agente, em sequência, porque elas mexem em alguns arquivos em comum (`App.xaml.cs`, `JanelaConfiguracoes`).

## 5a — Menu da bandeja moderno

**Hoje:** o menu é um `ContextMenuStrip` do WinForms (`App.xaml.cs:123`), com visual antigo e itens pequenos.

**Decisões:**
- **Janela nova:** o menu vira uma janela WPF sem borda, `MenuBandeja.xaml`.
  - **Visual:** mesmo tema da tela de Configurações (Fluent `ThemeMode="System"`, cantos arredondados e Mica pelo DWM, como na fase 4).
  - **Janela:** `WindowStyle=None`, `ResizeMode=NoResize`, `ShowInTaskbar=false`, `Topmost`, largura de cerca de 300 px.
- **Conteúdo, de cima para baixo:**
  1. **Cabeçalho:** ícone, "Cursor Suave" e a versão.
  2. **Faixa de atualização:** aparece só quando existe versão nova. Tem o texto "Versão X disponível" e o botão "Atualizar".
  3. **Interruptores** (`ToggleButton` no estilo ToggleSwitch do Fluent, com linhas de 40 px e o clique valendo em toda a linha): Ativo, Pausar em tela cheia, Iniciar com o Windows.
  4. **Botões largos** (40 px de altura, ícone Segoe Fluent Icons e texto): Configurações, Verificar atualizações, Restaurar cursor padrão.
  5. **Rodapé:** "Sair", separado dos outros e em texto vermelho discreto.
- **Abrir:** o clique direito no ícone abre o menu. O clique esquerdo também abre, porque hoje não faz nada.
  - **Posição:** o canto do menu fica junto ao ponteiro e dentro da área de trabalho do monitor do ponteiro. A conta fica em `PosicaoMenu.Calcular(ponteiro, tamanho, areaDeTrabalho)`, lógica pura com testes.
  - **Foco:** a janela chama `Activate()` mais `SetForegroundWindow` para receber o foco.
- **Fechar:** ao perder o foco (`Deactivated`), com Esc e depois de qualquer clique em botão. Os interruptores aplicam o efeito na hora e o menu continua aberto.
- **Lógica mantida:** os itens usam os mesmos métodos que já existem. O texto "Verificando…" e a faixa de atualização vêm dos mesmos pontos que hoje mexem em `_itemVerificar` e `_itemAtualizar`.
- **Remoção:** o `ContextMenuStrip` sai. O `NotifyIcon` do WinForms continua, só para o ícone e os balões.
- **Ícone da janela de Configurações** (pedido junto com a fase): é o mesmo ícone da bandeja, que hoje é desenhado em código por `App.CriarIcone()`.
  - **Como:** convertido uma vez para `ImageSource` com `Imaging.CreateBitmapSourceFromHIcon` e atribuído a `Window.Icon` nas Configurações. Aparece na barra de título e no botão da barra de tarefas.
  - **Cabeçalho do menu:** o menu novo usa o mesmo ícone.

## 5b — Marcação de carregando no anel

**Hoje:** quando o Windows mostra a ampulheta ou o círculo de espera, o cursor do sistema aparece, porque só o normal, a mão e a I-beam são trocados.

**Decisões:**
- **Trocar o cursor de espera:** `OCR_WAIT` (32514, a ampulheta) e `OCR_APPSTARTING` (32650, a seta com o círculo) passam a usar o mesmo arquivo da bolinha normal, no degrau atual. Os dois entram em `NivelBolinha.CursoresDoDegrau` e em `IdDoSistema`.
  - **Opção desligada:** os cursores de espera não são trocados, e o Windows mostra a ampulheta de sempre.
- **Detectar o carregamento:** `CursorSistema.EhCarregando(hCursor)` segue o padrão do `EhMao`. Guarda os handles de `LoadCursor(IDC_WAIT / IDC_APPSTARTING)` e os dos arquivos depois de cada troca, e o anel consulta isso a cada quadro, junto com o `EhMao`.
  - **Limite aceito:** um programa que usa cursor de espera próprio, e não o do sistema, não é detectado.
- **A marcação:** um arco desenhado em cima da borda do anel, no mesmo raio, girando no sentido horário a 1 volta por segundo.
  - **Traço:** a espessura é a da borda mais 1 px, com pontas arredondadas (`DrawArc`, `LineCap.Round`).
  - **Entrada e saída:** aparece em 120 ms e some em 200 ms depois que o carregamento acaba. Isso evita piscar quando o cursor de espera dura só um instante.
  - **Mouse parado:** a marcação não apaga com o mouse parado (quem espera um carregamento costuma deixar o mouse quieto). O anel continua apagando como hoje.
- **Estado puro:** `EstadoAnel` ganha `Carregamento` (de 0 a 1, com a entrada e a saída) e `AnguloCarregamento` (em graus, soma `360 × dt` e volta a 0 depois de 360). Os dois têm testes.
- **Redesenho:** hoje o anel só redesenha quando a posição muda. Com `Carregamento > 0`, precisa redesenhar todo quadro. **Risco:** sem isso, a marcação fica parada.
- **Configurações** (cartão novo, "Carregando", entre "Barra de texto" e "Mouse parado"):

  | Campo | Controle | Faixa | Padrão |
  |---|---|---|---|
  | `MarcacaoCarregando` | caixa "Trocar a ampulheta pela marcação no anel" | — | true |
  | `TamanhoMarcacao` | slider e campo, em graus (comprimento do arco) | 30 a 270, passo 10 | 90 |
  | `CorMarcacao` | amostras redondas e campo hex (como o anel) | — | `#4CC2FF` (azul do Windows) |
  | `OpacidadeMarcacao` | slider e campo, em % | 10 a 100 | 100 |

  - **Botão "Prévia":** liga a marcação por 3 s para ver o efeito sem esperar um carregamento real. É um sinal de teste na thread do anel, com `volatile` e prazo.
  - **Restaurar padrão:** `ComPadroesVisuais()` passa a incluir os 4 campos.
  - **Arquivo de configuração:** quem já tem `config.json` recebe os padrões nos campos novos.

## 5c — Atualizar pela tela de Configurações

**Hoje:** o cartão "Sistema" só mostra a versão. Para atualizar, é preciso usar o menu da bandeja, que mostra o progresso só no texto do item.

**Decisões:**
- **Cartão "Sistema":** abaixo da versão, uma linha de atualização com estes estados:
  1. **Inicial:** botão "Verificar atualizações".
  2. **Verificando:** botão desabilitado, com o texto "Verificando…".
  3. **Sem novidade:** "Você está na versão mais recente."
  4. **Versão nova:** "Versão X disponível" e o botão em destaque "Atualizar agora".
  5. **Baixando:** `ProgressBar` e o texto "42% · cerca de 12 s restantes". Nos primeiros instantes aparece "calculando o tempo…".
  6. **Erro:** a mensagem curta e o botão "Tentar de novo".
  7. **Não instalado** (rodando pelo `dotnet run` ou pelo `bin`): botão desabilitado e "Atualizações só na versão instalada."
- **Confirmação antes de baixar:** "O Cursor Suave vai reiniciar para atualizar. Ajustes não salvos nesta tela serão descartados. Continuar?" (Sim/Não).
- **Estimativa de tempo:** o Velopack informa só a porcentagem.
  - **Classe pura `EstimativaDownload`:** `Registrar(percentual, decorrido)` devolve os segundos restantes, ou null enquanto não há dado bastante (menos de 3% ou menos de 1 s).
  - **Conta:** taxa média desde o início, `restante = decorrido × (100 − p) / p`, suavizada com média móvel exponencial (α = 0,3) para não pular.
  - **Texto:** `Formatar(segundos)` dá "cerca de 8 s", "cerca de 1 min 05 s" ou "menos de 1 s".
  - **Testes:** a estimativa e a formatação têm testes unitários.
- **Um download por vez:** o download fica no `App`, num só lugar.
  - **A janela:** só se inscreve no progresso (evento `ProgressoAtualizacao(int percentual, double? segundosRestantes)`) e se reinscreve se for fechada e reaberta durante o download.
  - **O menu da bandeja:** "Atualizar" abre as Configurações já no cartão "Sistema" e começa o download ali.
- **Se o download falhar:** mostra o erro, volta ao estado "Versão nova" e o app continua rodando.

## 5d — Cursor volta ao padrão se o app fechar à força

**Hoje:** a restauração só roda no "Sair" e no fechamento normal. Se o processo é encerrado à força (Gerenciador de Tarefas, travamento, erro não tratado), o anel some, porque a janela morre junto. A bolinha fica, porque o `SetSystemCursor` vale para a sessão inteira.

**Decisões:**
- **Processo guardião:** ao iniciar, o app abre uma cópia de si mesmo com `--guardiao <pid>`.
  - **Início:** o `Program.Main` trata esse argumento logo depois do `VelopackApp.Build().Run()`, sem mutex, sem janela e sem bandeja.
  - **Espera:** o guardião espera o processo principal terminar (`Process.WaitForExit`, sem gastar CPU).
  - **Ao terminar:** se o principal não fechou limpo, o guardião restaura os cursores (`SPI_SETCURSORS`) e o "ocultar ponteiro ao digitar" original, e encerra.
- **Arquivo de sessão** `%LOCALAPPDATA%\CursorSuave\sessao.json`, com `{ pid, vanishOriginal }`:
  - **Gravação:** gravado quando o app aplica os cursores.
  - **Remoção:** apagado no `CursorSistema.Restaurar()` (Sair, pausa, Restaurar cursor padrão). Arquivo apagado significa "fechou limpo", e aí o guardião não faz nada.
- **Rede de segurança na abertura:** se o app abre e encontra `sessao.json` de um PID que não está mais rodando, restaura antes de aplicar. Isso cobre o caso de o guardião também ter sido encerrado.
- **Erros não tratados:** `DispatcherUnhandledException`, `AppDomain.UnhandledException` e `SessionEnding` chamam `Restaurar()` antes de o app cair.
- **Atualização (Velopack):** o `ApplyUpdatesAndRestart` já passa pelo `antesDeReiniciar`, que restaura e apaga o arquivo. O guardião vê o fechamento como limpo.
- **Lógica pura testável:** `Sessao.PrecisaRestaurar(arquivoExiste, pidAindaRodando)` e a leitura e gravação do JSON, com caminho injetável nos testes.
- **Limite aceito:** se o guardião for encerrado junto (por exemplo, "Finalizar árvore de processos"), o cursor volta só na próxima abertura do app ou ao reiniciar o Windows. O `SetSystemCursor` não fica gravado no perfil.

## 5e — Anel fora dos prints e gravações

**Hoje:** o anel é uma janela comum, então entra em qualquer captura de tela. A bolinha, por ser o cursor do sistema, já fica de fora na maioria dos prints, porque a Ferramenta de Captura não inclui o cursor por padrão.

**Decisões:**
- **Como esconder:** `SetWindowDisplayAffinity(hwnd, WDA_EXCLUDEFROMCAPTURE = 0x11)` na janela do anel, logo depois de criá-la, na thread do anel.
  - **Resultado:** ela continua visível na tela, mas some de prints (Win+Shift+S, PrintScreen), gravações e compartilhamento de tela.
  - **Compatibilidade:** precisa do Windows 10 2004 ou mais novo. Se a chamada falhar, nada quebra e o anel aparece no print como hoje.
- **Configurável:** nova caixa "Esconder o anel em prints e gravações" (`OcultarEmCapturas`, padrão true) no cartão "Sistema".
  - **Por que é opção:** quem grava tutoriais ou compartilha a tela numa reunião pode querer que o anel apareça.
  - **Ao vivo:** mudar a opção reaplica na janela do anel (`WDA_NONE = 0` ou `0x11`), com um sinal `volatile` lido pela thread do anel.
  - **Restaurar padrão:** não mexe nesta opção, como acontece com as outras do cartão Sistema.
- **Marcação de carregando (5b):** faz parte da mesma janela, então também fica de fora.

## Testes unitários
- **`PosicaoMenu.Calcular`:**
  1. Ponteiro no canto inferior direito: o menu abre para cima e para a esquerda.
  2. O menu nunca sai da área de trabalho.
  3. Ponteiro em monitor com coordenadas negativas.
- **`EstadoAnel`:**
  1. `Carregamento` sobe até 1 em cerca de 120 ms e desce até 0 em cerca de 200 ms.
  2. `AnguloCarregamento` avança 360 por segundo e volta a 0 depois de 360.
  3. Sem carregar, o ângulo não importa e `Carregamento` fica em 0.
- **`NivelBolinha.CursoresDoDegrau`:** com a marcação ligada, inclui os cursores de espera; com ela desligada, não inclui.
- **`Configuracao`:**
  1. Padrões dos 4 campos novos.
  2. `ComPadroesVisuais` inclui os 4 campos.
  3. JSON antigo, sem os campos, recebe os padrões (inclusive `OcultarEmCapturas` = true).
  4. `ComPadroesVisuais` mantém `OcultarEmCapturas`.
- **`EstimativaDownload`:**
  1. Dá null antes de 3% ou de 1 s.
  2. Taxa constante dá a estimativa certa.
  3. A suavização não deixa a estimativa pular.
  4. Formatação de 0,5 s, 8 s e 65 s.
- **`Sessao`:**
  1. Arquivo ausente: não restaura.
  2. Arquivo presente com o PID morto: restaura.
  3. Arquivo presente com o PID vivo: não restaura.
  4. Gravar e ler mantém o `vanishOriginal`.

## Roteiro na tela (Lucas)
| # | Caso | Por que na tela | Resultado esperado |
|---|---|---|---|
| 1 | Clique direito no ícone da bandeja, no tema claro e no escuro | Visual e posição | Menu arredondado no estilo Windows 11, junto ao ponteiro e dentro da tela, com botões grandes |
| 2 | No menu, desligar "Ativo" e ligar de novo; clicar fora | Interruptor e fechar ao perder o foco | O efeito some e volta na hora; clicar fora fecha o menu |
| 3 | Abrir um programa pesado (por exemplo, o Visual Studio) e passar o mouse em algo que mostra espera | Cursor real do sistema | Sem ampulheta; a marcação gira no anel e some ao terminar |
| 4 | Configurações → Carregando: mudar tamanho, cor e opacidade e clicar em "Prévia" | Desenho ao vivo | A marcação gira com os valores escolhidos por 3 s |
| 5 | Configurações → Sistema → Verificar atualizações | Estados da tela | Na 1.0.5, mostra "Você está na versão mais recente." |
| 6 | Abrir as Configurações | Ícone na barra de tarefas | A barra de título e o botão na barra de tarefas mostram o ícone do Cursor Suave |
| 7 | Gerenciador de Tarefas → Detalhes → finalizar o `CursorSuave.exe` principal (o de maior memória) | Encerramento à força real | Em até 1 s, o cursor volta à seta padrão do Windows |
| 8 | Abrir de novo o app e depois usar "Sair" | Fechamento limpo | O cursor volta ao padrão, e o guardião encerra junto (só um `CursorSuave.exe` some e nenhum fica sobrando) |
| 10 | Win+Shift+S com o anel na tela; depois desligar "Esconder o anel em prints" e repetir | Captura real | Primeiro print sem o anel; segundo com o anel |
| 11 | Sem regressão: link (mão), campo de texto (barra) e mouse parado | Cursores trocados | Continuam como na 1.0.4 |

**Limite do roteiro:** a barra de progresso com o tempo restante só pode ser vista na prática quando houver uma versão mais nova que a 1.0.5. Ela fica testada nos testes unitários e na próxima atualização real (1.0.5 → 1.0.6).

## Resultados
(5a, 5b e 5c: preenchidos pelos outros agentes)

### 5b
- **Build:** 0 erros e 0 avisos. **Testes:** 143 aprovados, 0 falhas (novos: 3 em `EstadoAnelTests`, 1 em `NivelBolinhaTests`, 3 em `ConfiguracaoTests`).
- **Desvio:** `CursorDaBolinha` ganhou `Espera` e `EsperaSegundoPlano`; ambos usam o arquivo da bolinha normal (um `LoadCursorFromFile` por cursor).
- **Desvio:** com a marcação ativa a janela fica opaca (alfa 255) e o anel apaga pelo desenho (fator `Opacidade`), para a marcação não apagar com o mouse parado.
- **Desvio:** a prévia vale mesmo com a caixa "Trocar a ampulheta" desligada; desligar a caixa recarrega os cursores originais (`SPI_SETCURSORS`) antes de reaplicar.
- **Não verificado na tela:** casos 3 e 4 do roteiro (arco girando, prévia).

### 5d/5e
- **Build:** 0 erros e 0 avisos. **Testes:** 136 aprovados, 0 falhas (novos: `SessaoTests`, 4 em `ConfiguracaoTests`).
- **Arquivos novos:** `Sessao.cs` (JSON `{Pid, VanishOriginal}` com caminho injetável, `PrecisaRestaurar`, `PidRodando`) e `Guardiao.cs` (spawn e corpo do guardião).
- **Desvio:** `VanishOriginal` é `bool?` (null se a leitura do valor falhou; aí o guardião só restaura os cursores).
- **Desvio:** o `Restaurar()` incondicional na abertura foi mantido depois da rede de segurança (barato, cobre versões sem `sessao.json`).
- **Desvio:** o guardião não é aberto quando a exe é `dotnet.exe` (`dotnet run`/dll); lá vale só a rede de segurança.
- **Desvio:** o "sinal volatile" de 5e é o próprio `Configuracao` volátil do anel: `DefinirAfinidade` roda na criação da janela e a cada config nova.
- **Não verificado na tela:** guardião real, Win+Shift+S (casos 7, 8 e 10 do roteiro). Risco a observar: o guardião ainda vivo por instantes durante o `ApplyUpdatesAndRestart` do Velopack.

### 5a/5c
- **Build:** 0 erros e 0 avisos. **Testes:** 156 aprovados, 0 falhas (novos: `PosicaoMenuTests`, `EstimativaDownloadTests`).
- **Arquivos novos:** `MenuBandeja.xaml(.cs)`, `PosicaoMenu.cs`, `EstimativaDownload.cs`, `ControleAtualizacao.cs` (estado e download únicos no App, evento `Mudou`), `EstiloJanela.cs` (DWM compartilhado com as Configurações).
- **Desvio:** o evento de progresso é o `ControleAtualizacao.Mudou` (a janela lê `Situacao` com percentual e segundos), em vez de `ProgressoAtualizacao(int, double?)`.
- **Desvio:** o ToggleSwitch é um template próprio de `ToggleButton` (o WPF Fluent não traz estilo de interruptor); hover em cinza translúcido.
- **Desvio:** sem botão Cancelar (fora do escopo); falha no download volta a "Versão nova" com a mensagem. Verificar pela janela não gera balão; o balão de versão nova só sai com as Configurações fechadas.
- **Desvio:** clique no balão de atualização também abre as Configurações e pede a confirmação.
- **Não verificado na tela:** casos 1, 2, 5 e 6 do roteiro (posição/DPI do menu, foco e Deactivated, estados do cartão Sistema).
