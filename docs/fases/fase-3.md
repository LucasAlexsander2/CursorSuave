# Fase 3 — Instalador, GitHub e atualização automática

## Objetivo
- Instalar o Cursor Suave em qualquer PC com um `Setup.exe` baixado do GitHub, sem precisar instalar o .NET.
- O app instalado verifica se há versões novas no GitHub Releases, avisa e só atualiza se o usuário quiser.

## Decisões do Lucas (16/11/2026)
- Repositório **público** `LucasAlexsander2/CursorSuave`. Assim o app baixa as atualizações sem token embutido.
- Versão nova encontrada: **avisar e perguntar**. O app só baixa e reinicia depois do clique.
- Publicação: **GitHub Actions disparado por tag** (`v1.2.3`).

## Decisões técnicas
- **Velopack** (sucessor do Squirrel) faz o instalador e a atualização.
  - **O que ele cuida:** gera o `Setup.exe`, instala em `%LOCALAPPDATA%\CursorSuave\` sem pedir administrador, cria atalhos e baixa atualizações do GitHub Releases, inclusive as delta (só o que mudou).
  - **Pacotes:** o NuGet `Velopack` no app e a ferramenta `vpk` no workflow, sempre **na mesma versão**, fixada nos dois.
- **Publicação self-contained** `win-x64`: o PC de destino não precisa do .NET. O pacote cheio fica maior, mas as atualizações delta compensam.
- **Versão:** `<Version>` no csproj (começa em `1.0.0`). O workflow pega a versão da tag e passa `-p:Version=` no publish e `-v` no `vpk pack`.
- **Ponto de entrada:**
  - Criar `Program.cs` com um `Main` (`[STAThread]`) que chama `VelopackApp.Build()...Run()` antes de qualquer coisa e depois `new App()` + `InitializeComponent()` + `Run()`.
  - `App.xaml` deixa de gerar o `Main` (`StartupObject` apontando para `Program`).
- **Hooks do Velopack:**
  - `OnFirstRun` e `OnAfterUpdateFastCallback`: se o valor `Run\CursorSuave` existir, regravar com o caminho do exe atual.
  - `OnBeforeUninstallFastCallback`:
    - restaurar o cursor (`SPI_SETCURSORS`);
    - devolver "Ocultar ponteiro ao digitar", se estiver guardado;
    - apagar o valor `Run`;
    - manter a configuração em `%APPDATA%`.
  - Os hooks rodam rápido e sem interface.
- **Classe `Atualizador`:** envolve o `UpdateManager(new GithubSource("https://github.com/LucasAlexsander2/CursorSuave", null, false))`.
  - **Sem instalação:** se `!IsInstalled` (rodando do `bin`), tudo fica desligado e o item do menu mostra "Atualizações só na versão instalada".
  - **Quando verifica:** 30 s depois de abrir e a cada 6 h, se `VerificarAtualizacoes` (novo campo da configuração, padrão `true`) estiver ligado. Também no item "Verificar atualizações" da bandeja.
  - **Versão encontrada:**
    - aparece um balão na bandeja: "Cursor Suave X disponível. Clique para atualizar.";
    - o item "Atualizar para X" aparece no menu.
  - **Ao clicar para atualizar:**
    - o download começa, com o progresso no texto do ícone;
    - o cursor original é restaurado;
    - `ApplyUpdatesAndRestart`.
  - **Erros** (sem internet, limite da API): na verificação automática, ficam em silêncio. Na verificação manual, aparece um balão com a mensagem curta.
  - **Lógica pura testável:** `Atualizador.DeveVerificar(agora, ultimaVerificacao, ligado)`, com intervalo de 6 h.
- **Bandeja:** um item desabilitado no topo com "Cursor Suave 1.0.0", mais os itens "Verificar atualizações" e "Atualizar para X" (este só aparece quando há versão nova).
- **Configurações:** caixa de marcar "Verificar atualizações automaticamente".
- **Workflow** `.github/workflows/release.yml`:
  - **Gatilho:** `push` de tags `v*`, rodando em `windows-latest`, com `permissions: contents: write`.
  - **Passos:**
    1. `setup-dotnet` 10.x;
    2. `dotnet test`;
    3. `dotnet publish` self-contained em `publish/`;
    4. `dotnet tool install -g vpk --version <fixa>`;
    5. `vpk download github` (traz a versão anterior para gerar a delta; não falha se não houver);
    6. `vpk pack -u CursorSuave -v <versão> -p publish -e CursorSuave.exe --packTitle "Cursor Suave"`;
    7. `vpk upload github --repoUrl ... --publish --releaseName "Cursor Suave <versão>" --tag v<versão> --token ${{ secrets.GITHUB_TOKEN }}`.
- **Sai:** o `tools/publicar.ps1` (substituído pelo instalador). Remover o script e citar no README.
- **README** curto: o que é, um GIF ou imagem (opcional, fica para depois), como instalar (baixar o `Setup.exe` em Releases), o aviso do SmartScreen e como publicar uma versão (criar a tag).
- **Antes de ficar público:** conferir que o repositório não tem dados pessoais (e-mail, caminhos com nome de usuário, tokens). Os documentos `docs/fases` vão junto.

## Partes
- **3a (agente Sonnet):** Velopack no app (`Program.cs`, hooks, `Atualizador`, bandeja, configuração), workflow, README e remoção do `publicar.ps1`.
  - Build sem avisos e testes passando.
  - Sem acesso ao GitHub.
- **3b (sessão principal, com autorização do Lucas a cada passo):**
  1. Commit inicial: fases 1 a 3.
  2. Criar o repositório público com `gh repo create`.
  3. Push da `main`.
  4. Tag `v1.0.0` e acompanhar o Actions.
  5. Instalar pelo `Setup.exe`.
  6. Tag `v1.0.1` para testar a atualização.

## Migração no PC do Lucas
1. Sair do app.
2. Apagar a pasta `%LOCALAPPDATA%\Programs\CursorSuave` e o atalho "Cursor Suave" do Menu Iniciar (os que o `publicar.ps1` criou).
3. Instalar pelo `Setup.exe` da release.
4. Se "Iniciar com o Windows" estava marcado, o `OnFirstRun` corrige o caminho.

## Testes unitários
- **`DeveVerificar`:**
  1. Nunca verificou: sim.
  2. Há menos de 6 h: não.
  3. Há 6 h ou mais: sim.
  4. Desligado: não.
- **Configuração:** `VerificarAtualizacoes` tem o padrão `true` e faz ida e volta (salvar e carregar).
- **Hook de inicialização:** se existe valor `Run`, o novo valor tem o caminho atual (função pura que recebe o valor atual e o caminho e devolve o novo valor ou null).

## Roteiro na tela (Lucas)
| # | Caso | Resultado esperado |
|---|---|---|
| 1 | Baixar o `Setup.exe` da release v1.0.0 e instalar | Aparece o aviso do SmartScreen ("Mais informações → Executar assim mesmo"); o app abre e há um atalho no Menu Iniciar |
| 2 | Publicar a v1.0.1 (tag) e esperar ou clicar em "Verificar atualizações" | O balão "Cursor Suave 1.0.1 disponível" aparece |
| 3 | Clicar em Atualizar | O app reinicia e a bandeja mostra 1.0.1, com as configurações mantidas |
| 4 | Desinstalar (Configurações do Windows → Aplicativos) | O cursor original volta e o app não inicia mais com o Windows |
| 5 | Outro PC (opcional) | O `Setup.exe` instala sem precisar do .NET |

## Resultados
### 3a (implementada em 16/11/2026)
- **Build:** 0 erros e 0 avisos. **Testes:** 55 aprovados, 0 falhas (novos: `DeveVerificar` x4, `VerificarAtualizacoes` padrão e ida e volta, valor `Run` do hook x2). `dotnet publish -r win-x64 --self-contained true` gerou `CursorSuave.exe` e `Velopack.dll` (não executado).
- **Velopack:** NuGet `Velopack` 1.2.161 (a mais recente restaurável) e `vpk` 1.2.161 fixado no workflow (`VPK_VERSION`). API conferida no XML do pacote: `VelopackApp.Build()`, `OnFirstRun`, `OnAfterUpdateFastCallback`, `OnBeforeUninstallFastCallback`, `UpdateManager`, `GithubSource(repoUrl, token, prerelease)`, `CheckForUpdatesAsync`, `DownloadUpdatesAsync`, `ApplyUpdatesAndRestart`, `IsInstalled`, `CurrentVersion`.
- **Arquivos:** `Program.cs`, `Atualizador.cs`, `Desinstalacao.cs` (novos); `Inicializacao.cs`, `Configuracao.cs`, `App.xaml.cs`, `JanelaConfiguracoes.xaml(.cs)`, `CursorSuave.csproj`; `.github/workflows/release.yml`, `README.md`; removida a pasta `tools` (`publicar.ps1`).
- **Desvio:** o build e o teste usaram `-p:OutDir=` temporário porque o `CursorSuave.exe` estava rodando (não foi encerrado).
- **Desvio:** o valor original de "Ocultar ponteiro ao digitar" só existe em memória (`CursorSistema`); no hook de desinstalação, que roda em outro processo, `Restaurar()` não tem o que devolver (a alteração vale só na sessão e some ao sair do app).
- **Desvio:** o menu mostra o item "Atualizar" (oculto) logo abaixo da versão, antes de "Verificar atualizações".
- **Dados pessoais:** nenhum e-mail, token ou caminho com nome de usuário encontrado; só o primeiro nome "Lucas" nos documentos. Nada foi trocado.
- **Não verificado:** app não executado e GitHub não acessado (roteiro na tela e fase 3b ficam com o Lucas).

## Ajuste 3a.1 — controle do atraso do anel (16/11/2026)
**Pedido do Lucas:** um campo nas Configurações para controlar o atraso do anel.
- Atraso maior: o anel fica mais para trás e a bolinha pode sair dele ao arrastar.
- Atraso 0: o anel anda colado na bolinha.

**Decisões:**
- **O slider "Suavidade" vira "Atraso do anel"**, em milissegundos: de 0 a 500 ms, de 10 em 10, com padrão de 100 ms (equivale à suavidade 0,15 de hoje).
- **Fórmula:** o `Seguidor` passa a receber `atrasoSegundos` (τ) no lugar de `suavidade`. A cada quadro, `f = τ <= 0 ? 1 : 1 - exp(-dt / τ)`. Com 0, o anel fica colado no mouse.
- **Nova caixa de marcar "Manter a bolinha dentro do anel"** (padrão `true`).
  - **Marcada:** a trava atual continua; com atraso alto, o anel é puxado.
  - **Desmarcada:** sem trava; com atraso alto, a bolinha sai do anel.
- **Configuração:**
  - O campo `Suavidade` sai e entram `AtrasoMs` (int, de 0 a 500, padrão 100) e `ManterBolinhaDentro` (bool, padrão `true`).
  - **Migração:** se um JSON antigo tiver `Suavidade` e não tiver `AtrasoMs`, converter com `AtrasoMs = round(-1000 / (60 * ln(1 - s)))`, limitado à faixa.

**Testes unitários:**
- **Atraso 0:** o anel fica igual ao mouse em um quadro.
- **Atraso de 100 ms:** depois de 100 ms, o anel percorreu cerca de 63% da distância (1 - e⁻¹).
- **Sem trava:** com um salto grande, a distância pode passar do limite.
- **Configuração:** os padrões e a faixa de `AtrasoMs` estão corretos, e a migração de `Suavidade` 0,15 dá cerca de 100.

**Roteiro (Lucas):**
1. Mover o slider de atraso de 0 até 500. O efeito muda ao vivo: com 0 o anel fica colado, e com 500 fica bem atrasado.
2. Desmarcar "Manter a bolinha dentro do anel" com o atraso alto e arrastar o mouse rápido: a bolinha sai do anel e o anel volta a alcançá-la.

**Resultado:** build com 0 erros e 0 avisos; 62 testes passando (7 novos: atraso 0, 63% em 100 ms, sem trava, padrões, faixa e 2 de migração). Desvios: a migração de Suavidade 0,15 dá 103 ms (a fórmula do doc, arredondada), não exatamente 100; `Seguidor.SuavidadePadrao` foi mantida só para as animações do `EstadoAnel`. Slider reaproveita `SlSuav` (rótulo "Atraso do anel", exibe "N ms"). Não executei o app.
