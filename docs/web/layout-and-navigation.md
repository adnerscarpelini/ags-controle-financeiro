# Layout e navegação

Origem: [#5](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/5) e [#96](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/96). As decisões de visual estão em [ui-design.md](../architecture/ui-design.md).

## Comportamento

- Menu lateral recolhível (256 px aberto, 76 px recolhido), na ordem: Dashboard, Lançamentos, Contas, Cartões, Planejamento, Relatórios, Reembolsos, Cadastros (Bancos, Categorias, Descrições e Pessoas), Mais opções (Anexos, Arquivos CSV e Backups) e Configurações. O item da tela aberta fica destacado, e o grupo dele aparece aberto.
- Aberto, o menu mostra ícone e rótulo, com os grupos em árvore. Recolhido, mostra só ícones com tooltip, e os grupos abrem em menu flutuante. A escolha fica guardada neste navegador (`localStorage`), e sem armazenamento vale até recarregar.
- O botão **+ Novo** fica sempre visível e abre as janelas de Despesa, Receita, Despesa cartão e Transferência, sem sair da tela atual (#97). Ctrl+N abre o menu. Ao salvar, `EntryChangeNotifier` avisa a tela aberta, que recarrega sem recarregar a página.
- No rodapé ficam [Ocultar valores](hide-values.md) (também na barra superior do celular), Recolher menu e **Sair** (veja [login.md](../identity/login.md)). Configurações também traz a troca de senha.
- A raiz `/` abre a tela inicial das preferências. `/cadastros` redireciona para `/cadastros/categorias`. `/pendentes` abre Lançamentos já filtrada (#98), `/transferencias` leva a Contas (#99), `/faturas`, `/compromissos` e `/cashback` levam a Cartões (#100), e `/cadastros/contas-cartoes` leva a Contas (#101).
- Abaixo de 768 px o menu vira uma gaveta: some da tela, abre pelo botão da barra superior e fecha ao escolher um item ou tocar na película escura. O **+ Novo** continua acessível em um botão flutuante. O conteúdo ocupa a largura inteira, janelas abrem em tela cheia com as ações no rodapé, e tabelas viram listas em linhas. Alvos de toque têm pelo menos 44 px (#103).
- A cultura da aplicação é `pt-BR` em todos os circuitos (`Program.cs`): `R$ 1.234,56` e `dd/MM/yyyy`, independentemente da cultura do sistema ou do navegador.
- Endereço inexistente responde 404 com a página **Página não encontrada** (`Components/Pages/NotFound.razor`), dentro do layout e com o menu. Sem login, o visitante vai para a tela de login.
- Nada vem da internet: CSS, JS e ícones do [MudBlazor](../architecture/ui-library.md), fonte Inter e demais recursos são servidos pela própria aplicação.
- A estrutura usa `MudLayout`, `MudAppBar`, `MudDrawer` e `MudMainContent`. A barra superior só aparece abaixo de 768 px; a película da gaveta é a da biblioteca.
- O tema do MudBlazor acompanha a preferência: `ThemeState` avisa o layout ao salvar em Configurações, e o modo automático segue o sistema (#95). A observação do sistema é feita por `agsTheme.watchSystem`, com `ObserveSystemDarkModeChange` desligado: ligado, a biblioteca trocaria a paleta sozinha e ignoraria o tema escolhido.
- CSS, JS e demais arquivos estáticos saem com `Cache-Control: no-cache`: o navegador revalida a cada carga, e uma atualização da aplicação chega sem limpar o cache.

## Arquivos

| Arquivo | Papel |
| --- | --- |
| `wwwroot/css/tokens.css` | Único lugar com cores literais: paleta nos temas escuro e claro (`light-dark()`), cores de banco, película da gaveta, espaçamento, raio e fonte, como variáveis CSS. |
| `wwwroot/css/app.css` | Estilos de estrutura e de componentes, só com `var(--...)`. Inclui `.numeric` (dígitos tabulares, alinhado à direita), `.income` e `.expense`. |
| `wwwroot/fonts/inter-latin-variable.woff2` | Inter variável (subconjunto latino), licença OFL em `Inter-LICENSE.txt`. |
| `wwwroot/img/favicon.png` | Favicon e marca do menu, gerado a partir do `logo.png` da raiz (64 × 64). |
| `Components/Layout/MainLayout.razor` | Provedores do MudBlazor e estrutura com barra superior, menu e conteúdo; guarda o estado da gaveta e o tema. |
| `Components/Layout/AppTheme.cs` | `MudTheme` com os mesmos valores de `tokens.css`. |
| `Components/Layout/ViewportState.cs` e `wwwroot/js/viewport.js` | Informam se a tela está na largura do celular. |
| `Components/HelpTip.razor` | Ajuda com mouse, teclado e toque. |
| `Components/Common/EmptyState.razor` e `ConfirmDialog.razor` | Estado vazio e confirmação padrão. |
| `Components/Layout/NavMenu.razor` | Árvore de seções (rota, rótulo, ícone), grupos abertos conforme a tela atual e modo recolhido. |
| `Components/Layout/NewEntryMenu.razor` | Botão + Novo. |
| `Components/Entries/EntryDialog.razor` e `TransferDialog.razor` | Janelas de lançamento e de transferência, abertas por `EntryDialogs`. |
| `Components/Common/AppDialogs.cs` | Opções únicas das janelas de formulário. |
| `Components/Common/MoneyField.razor` | Campo de valor em pt-BR, com teclado numérico. |
| `Components/Common/MonthPicker.razor`, `StatCard.razor` e `RowActions.razor` | Seletor de mês, card de total e ações por linha. |
| `Components/PageHeader.razor` | Título da página (`<PageTitle>` e `<h1>`), usado por todas as seções. |
| `Components/Cards/*.razor`, `CreditCardView.razor` e `Pages/InvoiceDetail.razor` | Carteira, janelas de cartão e de fatura e detalhe da fatura. |
| `Components/Common/RegistryPage.razor` e `Components/Registries/*.razor` | Padrão das telas de cadastro e suas janelas. |
| `Components/Dashboard/*.razor` | Seções do Dashboard, com `StatCard` e `MonthPicker`. |
| `Components/Accounts/*.razor` | Card da conta e janelas de conta, ajuste de saldo e movimentos. |
| `Components/Pages/*.razor` | Uma página por seção. |

## Como adicionar uma seção ou usar uma cor

- Seção nova: um item em `s_items` de `NavMenu.razor` e uma página com `@page` e `<PageHeader Title="..." />`.
- Cor nova: um token em `tokens.css`; componentes usam `var(--color-...)`. O teste `UiTokensTests` falha se um `.razor` ou `.css` fora de `tokens.css` tiver cor literal.

## Testes

- `LayoutPagesTests` (Web): cada seção mostra o título e destaca só o próprio item; `/` redireciona; favicon declarado; nenhuma referência externa nas páginas e nos CSS; ativos estáticos servidos.
- `MobileLayoutTests` (bUnit): opções de janela, tabelas em cards, cards e menu de Lançamentos, ordenação em seletor e + Novo no celular. O bUnit não mede layout; a verificação visual precisa de navegador.
- `MainLayoutTests` (bUnit): a gaveta começa fechada no celular, abre pelo botão, fecha pelo botão, pela película e ao escolher um item; árvore e grupos, destaque da tela atual, + Novo com quatro opções, modo recolhido lembrado, com nomes acessíveis, tooltip e menu flutuante; provedores presentes; o tema acompanha a preferência e o sistema.
- `CommonComponentsTests` (bUnit): `HelpTip` com teclado, toque e mouse; `EmptyState`; `ConfirmDialog` pelo `IDialogService`, confirmando e cancelando.
- `AppThemeTests`: `AppTheme` e `tokens.css` têm os mesmos valores.
- `CultureTests`: cultura `pt-BR`, `R$ 1.234,56` e `05/01/2027`.
- `UiTokensTests` (arquitetura): sem cores literais fora de `tokens.css`; a paleta documentada está nos tokens.

## Limites conhecidos

- A gaveta depende do circuito Blazor estar conectado; antes disso (primeiros instantes) o botão ainda não responde.
- Login (#36), PWA (#38) e tema claro/automático (#69) estão entregues.
- Carteira visual (#55), arquivamento reversível (#53), pendentes (#57) e dashboard de pendências (#58) estão entregues.
