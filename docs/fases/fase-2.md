# Fase 2 — Correções e melhorias

A fase é dividida em duas partes, com um agente para cada uma:
- **2a:** correções e efeitos do anel. Mexe em `AnelNativo`, `App`, na suspensão e na lógica pura.
- **2b:** configurações, inicialização com o Windows e publicação.

A 2b começa só depois que a 2a estiver pronta e conferida.

---

## Parte 2a — Correções e efeitos

### Problemas relatados (Lucas)
1. **Menu Iniciar, pesquisa e Visão de Tarefas:** o anel fica escondido e a bolinha aparece sozinha. Esses menus ficam numa camada (z-band) acima de qualquer janela comum, até das que estão sempre no topo.
2. **Ao digitar:** a bolinha some e o anel continua na tela. A opção do Windows "Ocultar ponteiro ao digitar" (`SPI_GETMOUSEVANISH`) está ligada.

### Decisões
- **Menus do shell → suspender**, do mesmo jeito que na tela cheia: o anel some e o cursor original volta.
  - **Quando conta como menu do shell:** o processo da janela em primeiro plano é `StartMenuExperienceHost`, `SearchHost`, `SearchApp` ou `ShellExperienceHost`, OU a classe da janela é `MultitaskingViewFrame`, `XamlExplorerHostIslandWindow` ou `ForegroundStaging`.
  - **Polling:** o intervalo cai de 500 ms para 100 ms, para a resposta ser rápida (a checagem é barata).
  - **Lógica pura:** `Suspensao.Decidir(classe, processo, telaCheia, pausarTelaCheia, programasPausados)` devolve um `MotivoSuspensao` (Nenhum, TelaCheia, MenuDoShell, ProgramaPausado). O último item já prepara a 2b (lista vazia por enquanto).
  - **Nome do processo:** obtido com `GetWindowThreadProcessId` + `Process.GetProcessById(...).ProcessName`, guardado em cache por PID para não alocar a cada 100 ms.
- **Cursor oculto → o anel some junto.** A cada quadro, `GetCursorInfo`: se `flags & CURSOR_SHOWING == 0`, o anel fica com opacidade 0 (sem parar o laço). O anel volta quando o cursor reaparece.
- **Anel cresce sobre links:**
  - **Detecção:** depois de aplicar a bolinha, guardar `LoadCursor(IntPtr.Zero, IDC_HAND)`, que já é o handle trocado; a cada quadro, compara-se o `hCursor` do `GetCursorInfo` com ele.
  - **Efeito:** o diâmetro vai de 26 para 44 px lógicos, com preenchimento branco de 10% (igual à demo).
  - **Se a comparação falhar:** também comparar com o handle que o `LoadCursorFromFile` usou. Se nada funcionar, registrar nos Resultados (risco principal da fase).
- **Efeito de clique:** `GetAsyncKeyState(VK_LBUTTON | VK_RBUTTON)` lido a cada quadro, sem hook global. Botão pressionado → o anel vai para 80% do tamanho; solto → volta.
  - **Por que sem hook:** um hook `WH_MOUSE_LL` travaria o mouse do sistema inteiro se a thread demorasse.
- **Some quando parado:** depois de 3 s sem o mouse se mexer e sem clique, a opacidade do anel cai até 0 em 300 ms. Ao mexer, ela volta em 120 ms.
  - **Como:** a opacidade é aplicada no `SourceConstantAlpha` do `UpdateLayeredWindow`, sem redesenhar a imagem.
  - **A bolinha não some**, porque é o cursor real do Windows.
- **Animação:** diâmetro, escala do clique e opacidade usam o mesmo lerp independente de fps do `Seguidor`. Fica numa classe pura `EstadoAnel.Atualizar(dt, mexeu, pressionado, sobreLink, cursorVisivel)`, que devolve `(diametroLogico, opacidade)`.
- **Desenho e trava:**
  - **Trava:** usa o raio atual: `diametro * escalaClique / 2 - 3 - 2`.
  - **Tamanho da janela:** passa a ser `ceil(56 * escala)` lógico, para caber o anel de 44 px com folga e antialias.
  - **Quando redesenhar:** só quando a posição, o diâmetro ou o preenchimento mudarem mais que um limiar. Mudança só de opacidade chama `UpdateLayeredWindow` sem redesenhar.

### Testes unitários (2a)
- **`Suspensao`:**
  1. Cada processo do shell da lista, com o nome em qualquer caixa.
  2. Cada classe da lista.
  3. A tela cheia com a opção desligada não suspende.
  4. Um programa da lista de pausados.
  5. Uma janela comum.
- **`EstadoAnel`:**
  1. Sobre link, o diâmetro converge para 44.
  2. Botão pressionado, a escala converge para 0,8.
  3. Parado por 3 s ou mais, a opacidade converge para 0.
  4. Mexer devolve a opacidade a 1 em cerca de 120 ms.
  5. Cursor invisível dá opacidade 0 na hora.
  6. Um `dt` grande não ultrapassa o valor-alvo.
- **`Seguidor`:** a trava com um limite variável.

### Roteiro na tela (Lucas)
| # | Caso | Resultado esperado |
|---|---|---|
| 1 | Tecla Windows e passar o mouse no menu | A seta normal aparece no menu; ao fechar o menu, a bolinha e o anel voltam |
| 2 | Digitar num campo de texto | A bolinha e o anel somem juntos e voltam ao mexer o mouse |
| 3 | Passar sobre um link no navegador | O anel cresce suave e volta ao sair do link |
| 4 | Clicar e segurar | O anel encolhe e volta ao soltar |
| 5 | Deixar o mouse parado por 3 s | O anel desaparece aos poucos e reaparece ao mexer |
| 6 | Sem regressão: movimento rápido | Continua fluido e a bolinha não sai do anel |

---

## Parte 2b — Configurações, inicialização com o Windows e publicação

### Decisões
- **Arquivo `%APPDATA%\CursorSuave\config.json`** (System.Text.Json).
  - **Campos:**
    - `TamanhoAnel` (26, de 16 a 60) e `TamanhoBolinha` (6, de 4 a 12).
    - `Cor` ("#FFFFFF") e `Opacidade` (0,6, de 0,2 a 1).
    - `Suavidade` (0,15, de 0,05 a 0,5).
    - `CrescerEmLinks`, `EfeitoClique`, `SumirParado` e `PausarTelaCheia`, todos `true`.
    - `SegundosParado` (3, de 1 a 30) e `ProgramasPausados` (lista de nomes de processo sem ".exe").
  - **Validação:** valores fora da faixa são limitados à faixa.
  - **JSON inválido:** o arquivo vira `config.json.bak` e o app usa o padrão.
  - **Lógica pura:** `Configuracao.Carregar(texto)`, `Validar()` e `Salvar()`.
- **Os valores fixos de hoje viram leitura da configuração:**
  - O tamanho do anel sobre link fica proporcional: `TamanhoAnel * 44/26`.
  - A trava usa `TamanhoBolinha`.
  - Mudar o tamanho da bolinha gera o `.cur` de novo e reaplica o cursor. `CursorSistema.GerarCur(diametro)` recebe o tamanho.
- **Janela "Configurações…" (WPF)**, aberta pela bandeja.
  - **Controles:**
    - Sliders com o valor ao lado.
    - Cor: 6 amostras prontas mais um campo hex.
    - Caixas de marcar para os efeitos.
    - Lista de programas pausados: adicionar pelo nome ou escolhendo num combo com os processos que têm janela aberta; botão remover.
  - **Prévia ao vivo:** as mudanças valem na hora.
  - **Botões:** "Salvar" grava; "Cancelar" volta ao que estava.
  - **Só uma janela:** se já estiver aberta, ela é trazida à frente.
- **Iniciar com o Windows:** item marcável na bandeja e caixa de marcar na janela de Configurações. O estado real é o próprio registro, não o JSON.
  - **Registro:** grava `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, valor `CursorSuave` = `"<caminho do exe>"`.
  - **Testável:** a montagem do valor é uma função pura.
- **Publicação:** script `tools/publicar.ps1`.
  - **Comando:** `dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true`, com saída em `%LOCALAPPDATA%\Programs\CursorSuave\`.
  - **Se o app estiver rodando:** o script aborta com uma mensagem, sem matar o processo.
  - **Atalho:** cria um atalho no Menu Iniciar.
  - **Caminho no registro:** se "Iniciar com o Windows" estiver marcado, o valor aponta para o exe publicado.

### Testes unitários (2b)
- **`Configuracao`:**
  1. JSON vazio dá o padrão.
  2. Valores fora da faixa são limitados.
  3. Uma cor hex inválida volta ao padrão.
  4. Ida e volta (salvar e carregar) mantém os valores.
  5. Um JSON corrompido é sinalizado para backup.
- **`GerarCur(diametro)`:**
  1. O cabeçalho continua válido para 4 e 12 px.
  2. O hotspot fica no centro.
- **`Inicializacao.MontarValor(caminho)`:** o caminho sai entre aspas.

### Roteiro na tela (Lucas)
| # | Caso | Resultado esperado |
|---|---|---|
| 1 | Configurações: mudar tamanho, cor e suavidade | Muda ao vivo; "Cancelar" volta ao que estava; "Salvar" mantém depois de reabrir o app |
| 2 | Pôr um programa na lista de pausados e focá-lo | O efeito pausa só nele |
| 3 | Rodar `tools/publicar.ps1`, marcar "Iniciar com o Windows" e reiniciar o PC (ou sair e entrar de novo) | O app abre sozinho a partir da pasta publicada |
| 4 | Sem regressão: efeitos da 2a | O link, o clique e o mouse parado continuam funcionando |

## Resultados
### 2a
- **Build:** 0 erros e 0 avisos. Com o CursorSuave.exe rodando (PID 20356) e travando o bin, compilado com `-p:OutDir=` para uma pasta temporária, sem matar o processo.
- **Testes:** 42 aprovados, 0 falhas (novos: `SuspensaoTests`, `EstadoAnelTests` e a trava com limite variável em `SeguidorTests`).
- **Arquivos:** novos `Suspensao.cs`, `EstadoAnel.cs`, `JanelaAtiva.cs`; alterados `AnelNativo.cs`, `App.xaml.cs`, `CursorSistema.cs`, `Nativo.cs`.
- **Não verificado na tela:** o roteiro de tela é do Lucas. Risco principal (detecção de link por `hCursor`): `CursorSistema.EhMao` compara com `LoadCursor(IDC_HAND)` e com o handle do `LoadCursorFromFile`; se não funcionar na tela, registrar aqui.
- **Desvio:** `EstadoAnel.Atualizar` devolve o diâmetro sem a escala do clique; `EscalaClique` e `Preenchimento` ficam em propriedades, e o `AnelNativo` multiplica.
- **Desvio:** o preenchimento de link entra e sai com o mesmo lerp do diâmetro (`Preenchimento` de 0 a 1).
- **Desvio:** o cache de nomes de processo não guarda nomes vazios (falhas são tentadas de novo) e é limpo ao passar de 128 entradas.
- **Desvio:** `_programasPausados` no `App` é um array vazio fixo; a 2b liga à configuração. A tela cheia só é consultada quando "Pausar em tela cheia" está marcado.

### 2b
- **Build:** 0 erros e 0 avisos (`dotnet build`, sem precisar de `-p:OutDir=`).
- **Testes:** 48 aprovados, 0 falhas (novos: `ConfiguracaoTests` com 7, `InicializacaoTests` com 1 e `GerarCur(diametro)` com 3).
- **Arquivos:** novos `Configuracao.cs`, `Inicializacao.cs`, `JanelaConfiguracoes.xaml(.cs)`, `tools/publicar.ps1`; alterados `AnelNativo.cs`, `App.xaml.cs`, `CursorSistema.cs`, `EstadoAnel.cs`, `Seguidor.cs` e os testes.
- **Não verificado:** janela, publicação e registro não foram executados (roteiro do Lucas); `publicar.ps1` não foi rodado.
- **Desvio:** `Configuracao` é um `record` imutável entregue ao anel por `AnelNativo.Aplicar` (referência volátil); `Carregar(texto, out corrompido)` sinaliza o backup e `CarregarDoDisco` move para `config.json.bak`.
- **Desvio:** `EstadoAnel.OpacidadeParado` virou `OpacidadeParadoPadrao`; `EstadoAnel.Configurar(...)` e `Seguidor.Suavidade` (propriedade) trocam os parâmetros na thread do anel.
- **Desvio:** o tamanho da janela do anel é `TamanhoAnel + 30` lógicos (56 no padrão) e a trava é `max(0, diametro/2 - bolinha/2 - 2)`.
- **Desvio:** "Opacidade" da configuração é o alfa da borda do anel (0,6 = 0x99 de antes); o item "Pausar em tela cheia" da bandeja grava na configuração.
- **Desvio:** "Iniciar com o Windows" na janela só é aplicado ao Salvar; Cancelar/fechar desfaz a prévia.

## Ajustes da 2a após o teste do Lucas (16/11/2026)
1. **Menu Iniciar e outros menus do shell:** o cursor NÃO volta para a seta; a bolinha continua.
   - Remover a suspensão por `MenuDoShell` (o enum, a lista de processos e classes e os testes).
   - O anel fica atrás do menu, porque o Windows não deixa janelas comuns ficarem por cima dele. Essa limitação é aceita.
2. **Ao digitar, a bolinha e o anel continuam visíveis.**
   - **Ao aplicar a bolinha:** ler `SPI_GETMOUSEVANISH`, guardar o valor original e desligar com `SPI_SETMOUSEVANISH` usando `fWinIni = 0` (só vale na sessão; não grava no perfil, então reiniciar o Windows já desfaz).
   - **Ao restaurar** (sair, pausar, exceção): devolver o valor original.
   - **Anel:** a regra "cursor oculto → anel some" deixa de ser usada. O `cursorVisivel` continua passando pelo `EstadoAnel`, mas o laço não usa mais esse dado para esconder o anel.
3. **Sobre links e botões, o anel NÃO cresce.** Ele mantém 26 px e ganha só um leve fundo branco (10%) com transição suave. O diâmetro-alvo fica sempre no tamanho base.

**Testes:** atualizar `SuspensaoTests` e `EstadoAnelTests`.

**Roteiro (Lucas):**
- Repetir os casos 1, 2 e 3 da 2a com os novos resultados esperados:
  1. Menu Iniciar: a bolinha continua (o anel fica atrás do menu).
  2. Ao digitar: os dois continuam visíveis.
  3. Sobre um link: só o fundo clareia.
- **Caso extra:** sair do app e conferir em Configurações → Mouse que "Ocultar ponteiro ao digitar" voltou a estar ligado.

**Resultado:** build 0 erros e 0 avisos; 37 testes aprovados, 0 falhas. Desvios: o `DiametroLink` e o parâmetro do construtor foram removidos do `EstadoAnel`; o valor original de `SPI_GETMOUSEVANISH` fica em `CursorSistema` (guardado uma vez por ciclo Aplicar/Restaurar e devolvido em `Restaurar`, que já cobre sair, pausar e exceção); `Nativo` ganhou `SystemParametersInfoLerInt` para o GET. Não executado na tela (roteiro do Lucas).

## Mudanças na 2b por causa dos ajustes da 2a
Estas regras valem por cima do texto original da Parte 2b.
- **`CrescerEmLinks` passa a se chamar `FundoEmLinks`** (padrão `true`). Sobre links, o anel ganha só o fundo branco leve; não existe mais a regra `TamanhoAnel * 44/26`.
- **Novo campo `OpacidadeParado`** (padrão 0,3, faixa de 0 a 0,9). Ele substitui a constante `EstadoAnel.OpacidadeParado` e aparece como slider na janela de Configurações.
- **A cor escolhida vale para os dois:** a borda do anel e o fundo de link (10% dessa cor).
- **Lista de programas pausados:** usa a `Suspensao.Decidir` que já existe (motivo `ProgramaPausado`). Menus do shell nunca suspendem.
- **O desligamento de "Ocultar ponteiro ao digitar" continua** igual ao da 2a; não vira opção nas Configurações.
