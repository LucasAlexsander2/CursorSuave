# Fase 4 — Configurações com visual do Windows 11, campos digitáveis e "Restaurar padrão"

## Objetivo
A janela de Configurações deve parecer um app atual do Windows 11, e não um app antigo.
- **Visual:** cantos arredondados, cartões, tema claro ou escuro seguindo o sistema e fundo Mica.
- **Campos digitáveis:** todo valor ajustável pode ser digitado, além de mexido no slider.
- **Botão "Restaurar padrão":** devolve os ajustes ao padrão. O padrão novo é a configuração atual do Lucas.

## Decisões
- **Tema Fluent nativo do WPF** (.NET 9 em diante): `Application.ThemeMode = ThemeMode.System` no `App`, ou só na janela, com `ThemeMode="System"`.
  - **Vantagem:** dá cantos arredondados, controles no estilo Windows 11 e claro ou escuro automático, sem biblioteca externa.
  - **Experimental:** a API ainda é marcada como experimental (diagnóstico `WPF0001`). Ela entra em `NoWarn` no csproj, com um comentário explicando o motivo, para continuar com 0 avisos.
  - **Plano B:** se o tema Fluent não estiver disponível ou quebrar o build, usar estilos próprios (ResourceDictionary com `CornerRadius` de 4 nos controles e 8 nos cartões) e registrar nos Resultados.
- **Janela:**
  - **Cantos e fundo:** cantos arredondados reforçados com `DwmSetWindowAttribute(DWMWA_WINDOW_CORNER_PREFERENCE = 33, DWMWCP_ROUND = 2)` e fundo Mica com `DWMWA_SYSTEMBACKDROP_TYPE = 38, DWMSBT_MAINWINDOW = 2`. As duas chamadas podem falhar sem quebrar nada (Windows 10 ignora).
  - **Tamanho:** largura de cerca de 560 px, altura limitada à tela, com `ScrollViewer`.
  - **Rodapé fixo:** "Restaurar padrão" à esquerda; "Cancelar" e "Salvar" à direita, com "Salvar" em destaque (`AccentButtonStyle`, ou a cor de destaque do sistema no plano B).
- **Organização em cartões** (`Border` com `CornerRadius` 8, fundo de cartão do tema e espaçamento de 16):
  1. **Anel:** tamanho, cor, opacidade, opacidade em clicáveis, fundo em clicáveis, atraso, manter a bolinha dentro, efeito de clique.
  2. **Bolinha:** usar o mesmo visual do anel, tamanho, cor, as três opacidades.
  3. **Barra de texto:** mostrar a barra, altura e largura.
  4. **Mouse parado:** apagar quando parado, segundos, opacidade parada do anel.
  5. **Pausas:** pausar em tela cheia e a lista de programas pausados.
  6. **Sistema:** iniciar com o Windows, verificar atualizações e a versão atual.
  - **Linhas:** cada linha tem o rótulo à esquerda e o controle à direita, como nas Configurações do Windows.
- **Campos digitáveis:** cada slider ganha uma `TextBox` estreita ao lado (cerca de 64 px), com a unidade logo depois: "px", "ms", "s" ou "%".
  - **Opacidades:** aparecem e são digitadas em **%**, de 10 a 100 ou de 0 a 90 conforme a faixa. Internamente continuam de 0 a 1.
  - **Quando o valor é aplicado:** ao apertar Enter ou ao sair do campo. Valor fora da faixa é limitado à faixa; texto inválido volta ao valor atual.
  - **Sincronia:** mexer no slider atualiza o texto, e digitar atualiza o slider.
  - **Ajustes rápidos:** seta para cima e para baixo no campo soma ou subtrai um passo.
  - **Lógica pura testável:** `CampoNumerico.Interpretar(texto, min, max, casas)` aceita vírgula ou ponto e ignora a unidade digitada ("120 ms" vale 120). Devolve o valor limitado, ou null se o texto for inválido.
  - **Arredondamento:** os sliders arredondam pelo passo (`IsSnapToTickEnabled` / `TickFrequency`), para não gravar valores como 0,5983 no JSON.
- **Cor:** as 6 amostras viram botões redondos de 24 px, com um anel de seleção na cor escolhida, mais o campo hex. Isso vale para o anel e para a bolinha.
- **"Restaurar padrão":**
  - **Confirmação:** pede antes "Voltar todos os ajustes de aparência e comportamento ao padrão?" (Sim/Não).
  - **Efeito:** devolve ao padrão só os campos de aparência e comportamento, com prévia ao vivo. **Não mexe** na lista de programas pausados, em "Iniciar com o Windows" nem em "Verificar atualizações".
  - **Salvar e Cancelar:** como em qualquer outra mudança, só grava ao Salvar, e o Cancelar desfaz.
  - **Lógica pura:** `Configuracao.ComPadroesVisuais()` devolve a configuração com esses campos no padrão e mantém os outros.
- **Novos padrões** (vindos do `config.json` atual do Lucas; os campos que não aparecem continuam com o padrão de hoje):

  | Campo | Antes | Agora |
  |---|---|---|
  | TamanhoAnel | 26 | **25** |
  | TamanhoBolinha | 6 | **5** |
  | Opacidade | 0,6 | 0,6 |
  | OpacidadeEmLinks | 1,0 | 1,0 |
  | AlturaBarra | 18 | **14** |
  | LarguraBarra | 2 | **1** |
  | AtrasoMs | 100 | 100 |
  | SumirParado | true | **false** |
  | OpacidadeParado | 0,3 | 0,3 |
  | PausarTelaCheia | true | **false** |
  | Demais (cores, bolinha, efeitos) | — | sem mudança |

  Quem já tem `config.json` não é afetado, porque os valores salvos continuam valendo.

## Testes unitários
- **`CampoNumerico.Interpretar`:**
  1. Aceita "12", "12,5", "12.5" e "120 ms".
  2. Texto vazio ou "abc" devolve null.
  3. Valor abaixo ou acima da faixa é limitado.
  4. O arredondamento respeita as casas pedidas.
- **Conversão de opacidade:** 60 (%) vira 0,6 e volta para 60.
- **`ComPadroesVisuais`:** volta os campos visuais ao padrão e mantém `ProgramasPausados`, `VerificarAtualizacoes` e a opção de iniciar com o Windows.
- **Novos padrões:** um JSON vazio dá os valores da tabela. Os testes antigos que dependiam dos padrões anteriores são atualizados.

## Roteiro na tela (Lucas)
| # | Caso | Resultado esperado |
|---|---|---|
| 1 | Abrir as Configurações com o Windows no tema claro e depois no escuro | A janela segue o tema, com cantos arredondados, cartões e controles no estilo Windows 11 |
| 2 | Digitar "40" no atraso, Enter; digitar "abc" e sair do campo | O anel muda para 40 ms e o slider acompanha; "abc" volta ao valor anterior |
| 3 | Restaurar padrão → Sim | Os ajustes voltam à tabela acima; os programas pausados continuam; Cancelar desfaz |
| 4 | Sem regressão: Salvar e reabrir | Os valores salvos continuam e o efeito ao vivo funciona |

## Resultados
- **Build:** `dotnet build` com 0 erros e 0 avisos.
- **Testes:** 123 aprovados, 0 falhas (novos: `CampoNumericoTests.cs` com `Interpretar`, conversão de opacidade, `ComPadroesVisuais` e novos padrões; testes antigos atualizados para 25/14/1).
- **Tema:** Fluent nativo (`ThemeMode="System"` na janela), confirmado no SDK 10.0.13; `WPF0001` em `NoWarn` no csproj com comentário. Plano B não foi necessário.
- **Estilo da janela:** `DwmSetWindowAttribute` (cantos arredondados e Mica) em `SourceInitialized`, com falha tolerada. Não verificado na tela (o app não foi executado).
- **Desvios:**
  - Janela ficou com `ResizeMode="CanMinimize"` e altura 720, limitada a 95% da área de trabalho, com `ScrollViewer`.
  - As linhas de slider são montadas em código (`LinhaSlider`), não em XAML, para não repetir 12 blocos.
  - O tamanho da bolinha fica fora do painel que "mesmo visual" desabilita (como antes).
  - Atraso mantém o passo de 10 ms do slider, então "45" vira 50.
  - Opacidades gravadas arredondadas em 2 casas.
  - A versão no cartão Sistema vem de `_atualizador.VersaoAtual` (parâmetro novo opcional da janela).
