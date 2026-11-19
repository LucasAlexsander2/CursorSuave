# Fase 6 — Configurações em abas, modo Simples/Avançado e "Novidades" a cada versão

Versão alvo: **1.0.6**. A fase tem três partes (6a, 6b e 6c), feitas por agentes em sequência porque todas mexem na `JanelaConfiguracoes`.

## 6a — Abas, submenu de seções e modo Avançado

**Hoje:** a janela é um só `ScrollViewer` com 7 cartões. Do primeiro ao último: Anel, Bolinha, Barra de texto, Carregando, Mouse parado, Pausas e Sistema.

**Decisões:**
- **Abas no topo:** `TabControl` no estilo Fluent, com ícone (Segoe Fluent Icons) e texto. A largura continua em cerca de 560 px. Abas no topo, e não uma lista lateral, para a janela não ficar mais larga.
  1. **Mouse** (ícone `E962`): todas as configurações.
  2. **Sobre** (ícone `E946`): versão, atualização e novidades.
- **Aba Mouse, de cima para baixo:**
  1. **Barra fixa no topo** (fica parada enquanto o conteúdo rola):
     - **Interruptor "Avançado":** à direita.
     - **Submenu:** à esquerda, uma fileira de "pílulas" com as seções (Geral, Anel, Bolinha, Barra, Carregando, Parado, Pausas). Clicar numa pílula rola até a seção (`BringIntoView` no cartão). A pílula da seção visível fica destacada enquanto o conteúdo rola. Se a fileira não couber, ela rola na horizontal.
  2. **Os cartões, com um cartão novo "Geral" no começo:** "Iniciar com o Windows" e "Esconder o anel em prints e gravações" saem do antigo cartão Sistema e vão para lá.
  3. **Rodapé:** "Restaurar padrão", "Cancelar" e "Salvar" continuam fixos embaixo e valem para a aba Mouse.
- **Aba Sobre:**
  - **Conteúdo:** ícone e nome do app, versão atual, "Verificar atualizações automaticamente", a linha de atualização da fase 5 (botão, barra de progresso e tempo restante), o botão "Ver novidades" (abre o modal da 6b com a versão atual) e o link do projeto no GitHub.
  - **Rodapé:** é o mesmo da janela. "Verificar atualizações automaticamente" também só é gravado ao Salvar.
- **Modo Simples e Avançado:**
  - **Avançado ligado:** a tela fica como hoje, com todas as opções.
  - **Avançado desligado (Simples):** mostra só o essencial, conforme a tabela abaixo. As seções continuam as mesmas, então o submenu não muda.
  - **Opções escondidas:** continuam valendo com o valor que tinham. Esconder não reseta nada.
  - **Ao vivo:** trocar o interruptor só muda o que aparece. Ele é gravado no `config.json` na hora, sem depender do Salvar, porque é preferência da tela e não do cursor.

  | Seção | Simples mostra | Só no Avançado |
  |---|---|---|
  | Geral | Iniciar com o Windows, Esconder o anel em prints | — |
  | Anel | Tamanho, Cor, Opacidade, Atraso | Opacidade em clicáveis, Fundo em clicáveis, Manter a bolinha dentro, Efeito de clique |
  | Bolinha | Tamanho, "Usar o mesmo visual do anel", Cor (quando não usa o mesmo visual) | As três opacidades |
  | Barra de texto | Mostrar barra | Altura, Largura |
  | Carregando | Trocar a ampulheta, Cor, Prévia | Tamanho, Opacidade |
  | Mouse parado | Apagar quando parado | Segundos, Opacidade parada |
  | Pausas | Pausar em tela cheia | Lista de programas pausados |

- **Usuário antigo e usuário novo:**
  - **Campo novo:** `ModoAvancado` (bool) na `Configuracao`.
  - **Usuário antigo** (já existe `config.json`, mas sem o campo): `ModoAvancado = true`. Segue o padrão do `MigrarSuavidade`, que olha se a propriedade existe no texto do JSON.
  - **Usuário novo** (sem `config.json`): `ModoAvancado = false`.
  - **`config.json` corrompido:** vale o padrão de usuário novo, false.
  - **Restaurar padrão:** `ComPadroesVisuais()` não mexe no `ModoAvancado`.
  - **Lógica pura:** `Configuracao.Carregar(texto)` cuida disso, com testes.

## 6b — Modal "Novidades" uma vez a cada versão

**Decisões:**
- **Fonte das novidades:** o arquivo `src/CursorSuave/Novidades.json`, embutido como recurso (`EmbeddedResource`). Funciona sem internet e fica em português.
  ```json
  [ { "versao": "1.0.6", "itens": [ { "icone": "E8A9", "titulo": "Abas", "texto": "..." } ] } ]
  ```
- **Quando aparece:** ao abrir as Configurações, se `UltimaVersaoVista` for menor que a versão atual e existir nota para alguma versão no intervalo `(UltimaVersaoVista, atual]`.
  - **Conteúdo:** junta as notas dessas versões, da mais nova para a mais velha. Quem pulou da 1.0.6 para a 1.0.8 vê as duas.
  - **Depois de fechar:** grava `UltimaVersaoVista = atual` na hora, sem depender do Salvar, para aparecer uma vez só.
- **Usuário novo:** na primeira execução, sem `config.json`, `UltimaVersaoVista` já recebe a versão atual. Quem acabou de instalar não vê "o que mudou".
- **Usuário antigo:** `config.json` sem o campo é tratado como `UltimaVersaoVista = "1.0.5"`, então vê as novidades da 1.0.6.
- **Versão de teste:** quando o app não está instalado (`_atualizador.Instalado` false), o modal não abre sozinho. Nela, o modal só aparece pelo botão "Ver novidades" da aba Sobre.
- **Visual do modal:** janela pequena (cerca de 420 px), dona da tela de Configurações, com o mesmo tema, cantos arredondados e Mica (`EstiloJanela`).
  - **Conteúdo:** título "Novidades da versão X", uma lista com ícone, título em negrito e texto curto para cada item, e o botão em destaque "Entendi". Esc também fecha.
  - **Abertura:** abre depois de a tela de Configurações aparecer (`ContentRendered`), centralizado nela.
- **Lógica pura `Novidades`:**
  - **`Ler(json)`:** lê o arquivo.
  - **`Pendentes(lista, vista, atual)`:** devolve as notas do intervalo, comparando com `System.Version`. Versão inválida conta como nenhuma.
  - **`DaVersao(lista, versao)`:** usada pelo "Ver novidades".
- **Regra para toda versão daqui em diante:**
  - **No release:** um passo novo no `release.yml`, antes do `vpk pack`, falha se o `Novidades.json` não tiver entrada para a versão da tag. Assim nenhuma versão sai sem as novidades.
  - **No README:** uma linha nova na seção de como publicar.
- **Novidades da 1.0.6** (escritas em linguagem de usuário):
  1. **Configurações em abas:** "Mouse" e "Sobre", com atalhos para cada seção.
  2. **Modo Simples e Avançado:** o Simples mostra só o essencial.
  3. **Atualizar pela tela Sobre:** sem fechar o app, com porcentagem e tempo restante; reinicie quando quiser.
  4. **Novidades:** esta janela aparece uma vez a cada versão.

## 6c — Baixar sem fechar e "Reiniciar para aplicar"

**Hoje:** "Atualizar agora" pede confirmação, baixa e logo chama `ApplyUpdatesAndRestart`, que fecha o app.

**Decisões:**
- **Baixar sem reiniciar:** "Atualizar agora" baixa com progresso e tempo restante (como na fase 5), sem a confirmação e sem fechar.
  - **Ao terminar:** o estado novo "Pronto" mostra o texto "Versão X baixada. Reinicie para aplicar." e o botão em destaque "Reiniciar agora".
  - **Por que a confirmação sai:** baixar não fecha mais o app.
- **"Reiniciar agora":**
  - **Ajustes não salvos:** se a aba Mouse tiver mudanças, pergunta antes: "Há ajustes não salvos. Salvar antes de reiniciar?" (Salvar / Descartar / Cancelar).
  - **Depois:** chama `antesDeReiniciar` (restaura o cursor e apaga `sessao.json`) e `ApplyUpdatesAndRestart`.
- **Se o usuário não reiniciar:** o Velopack aplica a atualização baixada na próxima abertura do app (`VelopackApp` com a aplicação na abertura, que é o padrão; o agente confere que ela não está desligada no `Program.cs`). Na próxima vez que entrar, já está atualizado, inclusive depois de reiniciar o Windows.
  - **Sair pelo menu:** com atualização baixada, o "Sair" chama `WaitExitThenApplyUpdates(alvo, silent: true, restart: false)`. A atualização é aplicada logo ao fechar, e a próxima abertura fica mais rápida.
  - **Se essa chamada falhar:** vale a aplicação na abertura.
- **Onde o aviso aparece:**
  - **Menu da bandeja:** a faixa de atualização mostra "Reiniciar para atualizar" no lugar de "Versão X disponível".
  - **Aba Sobre:** reabrir a tela mostra de novo o estado "Pronto".
- **Estado puro:** o `ControleAtualizacao` ganha o estado `Pronto`, guardado em memória. Depois de reiniciar, o Velopack já resolve.
- **Novidades da 1.0.6:** o item 3 passa a dizer "Atualizar pela tela Sobre sem fechar o app; reinicie quando quiser."
- **Limite do teste:** este fluxo só pode ser visto de verdade na atualização 1.0.6 → 1.0.7, porque a 1.0.5 ainda baixa e reinicia direto.

## Testes unitários
- **`Configuracao`:**
  1. JSON antigo sem `ModoAvancado` dá true.
  2. Sem arquivo, o padrão é false.
  3. JSON com `false` continua false.
  4. `ComPadroesVisuais` mantém `ModoAvancado` e `UltimaVersaoVista`.
  5. JSON antigo sem `UltimaVersaoVista` dá "1.0.5".
- **`Novidades`:**
  1. Lê o JSON.
  2. `Pendentes` com `vista` 1.0.5 e `atual` 1.0.6 devolve 1 nota.
  3. `vista` igual a `atual` não devolve nada.
  4. Pulo de versões junta as notas, da mais nova para a mais velha.
  5. Versão inválida não devolve nada.
  6. O recurso embutido tem entrada para 1.0.6, como garantia contra esquecer.
- **`ControleAtualizacao`** (se a lógica de estados for testável sem o Velopack): depois do download, vai para Pronto; uma falha volta para Disponível.
- **Visibilidade por modo:** uma função pura `ModoTela.Visivel(campo, avancado)` (ou uma tabela equivalente) decide o que aparece, com testes da tabela acima.

## Roteiro na tela (Lucas)
| # | Caso | Por que na tela | Resultado esperado |
|---|---|---|---|
| 1 | Abrir as Configurações (versão de teste) | Abas e layout | Abas Mouse e Sobre com ícones; Avançado ligado (você é usuário antigo); janela mais curta de rolar |
| 2 | Clicar nas pílulas Carregando e depois Geral; rolar com a roda do mouse | Rolagem e destaque | Vai direto à seção; a pílula da seção visível fica destacada |
| 3 | Desligar Avançado | Visibilidade | Só as opções da coluna "Simples"; os valores escondidos continuam valendo no cursor |
| 4 | Aba Sobre → "Ver novidades" | Visual do modal | Modal arredondado com as 4 novidades da 1.0.6; "Entendi" fecha |
| 5 | Sem regressão: mudar o tamanho do anel, Salvar e reabrir | Prévia e gravação | Funciona como na 1.0.5 |
| 6 | Depois de publicar: atualizar a 1.0.5 instalada pela aba de Configurações | Fluxo real | Barra com porcentagem e tempo restante; ao reabrir as Configurações na 1.0.6, o modal de novidades aparece uma vez |

## Resultados
(preenchido na implementação)

### 6a
- Build: 0 erros, 0 avisos. Testes: 189 aprovados (novos: 5 de `Configuracao` para ModoAvancado e 5 casos/teorias de `ModoTelaTests`). Não executei o app (roteiro na tela fica com o Lucas).
- Desvio: o interruptor "Avançado" é um `CheckBox` (o tema Fluent do WPF não tem ToggleSwitch).
- Desvio: a pílula rola com `ScrollToVerticalOffset` (topo do cartão alinhado à vista) em vez de `BringIntoView`, que deixaria o cartão no rodapé da vista.
- Interruptor grava via `Configuracao.GravarModoAvancado` (só o campo, sobre o arquivo salvo); `_original` da janela e `_config` do App acompanham, para Cancelar e salvamento da bandeja não reverterem.
- Aba Sobre: "Ver novidades" com handler vazio (`TODO(6b)`); linha de atualização movida para lá. Teste 5 de `Configuracao` (UltimaVersaoVista) é da 6b.

### 6b/6c
- Build: 0 erros, 0 avisos. Testes: 202 aprovados (novos: 7 de `Novidades`, 2 de `Configuracao` para UltimaVersaoVista, 4 de `ControleAtualizacao`). Não executei o app (roteiro na tela fica com o Lucas).
- Desvio: criei a interface `IFonteAtualizacao` (implementada por `Atualizador`) para testar os estados do `ControleAtualizacao` sem o Velopack.
- Desvio: `UltimaVersaoVista` tem padrão "1.0.5" (assim JSON antigo sem o campo já dá 1.0.5, sem migração); usuário novo (sem `config.json`) recebe a versão atual no `App.OnStartup`.
- `WaitExitThenApplyUpdates(VelopackAsset, bool silent, bool restart, string[] restartArgs)` confere com a assinatura do 1.2.161 (usei `silent: true, restart: false`). `Program.cs` não desliga a aplicação na abertura (sem `SetAutoApplyOnStartup(false)`).
- Interruptor "Avançado" agora é um `ToggleButton` com o estilo `LinhaInterruptor`, movido para `Estilos.xaml` (mesclado em `App.xaml`); o menu da bandeja continua usando o mesmo estilo.
- "Ver novidades" mostra a nota da versão atual ou, se não houver, a última do arquivo. Clique em "Reiniciar para atualizar" no menu abre as Configurações e reinicia (com a pergunta Salvar/Descartar/Cancelar se houver ajustes); o balão de "versão disponível" só baixa.
- `release.yml`: passo "Conferir novidades da versão" (grep da versão da tag em `Novidades.json`) antes do `vpk pack`; linha no README.
