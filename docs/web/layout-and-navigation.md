# Layout e navegação

Origem: [#5](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/5). As decisões de visual estão em [ui-design.md](../architecture/ui-design.md).

## Comportamento

- Menu lateral fixo (240 px) com sete seções, na ordem: Lançamentos, Faturas, Cashback, Reembolsos, Dashboard, Cadastros e Configurações. O item da página aberta fica com a cor `Accent`. No fim do menu fica o botão "Sair" (veja [login.md](../identity/login.md)). Configurações também traz a troca de senha.
- As sete seções usam `/lancamentos`, `/faturas`, `/cashback`, `/reembolsos`, `/dashboard`, `/cadastros` e `/configuracoes`. A raiz `/` redireciona para `/dashboard`. Cadastros já acessa Contas e cartões (#6–#8); Configurações tem certificado e troca de senha. As demais páginas de negócio ainda são placeholders.
- Abaixo de 768 px o menu vira uma gaveta: some da tela, abre pelo botão da barra superior e fecha ao escolher um item ou tocar na película escura. O conteúdo ocupa a largura inteira e não há rolagem horizontal. Itens e botão têm pelo menos 44 px de altura.
- A cultura da aplicação é `pt-BR` em todos os circuitos (`Program.cs`): `R$ 1.234,56` e `dd/MM/yyyy`, independentemente da cultura do sistema ou do navegador.
- Nada vem da internet: CSS, fonte Inter e ícones (SVG embutidos) são servidos pela própria aplicação.

## Arquivos

| Arquivo | Papel |
| --- | --- |
| `wwwroot/css/tokens.css` | Único lugar com cores literais: paleta nos temas escuro e claro (`light-dark()`), cores de banco, película da gaveta, espaçamento, raio e fonte, como variáveis CSS. |
| `wwwroot/css/app.css` | Estilos de estrutura e de componentes, só com `var(--...)`. Inclui `.numeric` (dígitos tabulares, alinhado à direita), `.income` e `.expense`. |
| `wwwroot/fonts/inter-latin-variable.woff2` | Inter variável (subconjunto latino), licença OFL em `Inter-LICENSE.txt`. |
| `wwwroot/img/favicon.png` | Favicon e marca do menu, gerado a partir do `logo.png` da raiz (64 × 64). |
| `Components/Layout/MainLayout.razor` | Estrutura com barra superior, película, menu e conteúdo; guarda o estado da gaveta. |
| `Components/Layout/NavMenu.razor` | Lista de seções (rota, rótulo, ícone) e `NavLink`s. |
| `Components/PageHeader.razor` | Título da página (`<PageTitle>` e `<h1>`), usado por todas as seções. |
| `Components/Pages/*.razor` | Uma página por seção. |

## Como adicionar uma seção ou usar uma cor

- Seção nova: uma linha em `NavMenu.razor` e uma página com `@page` e `<PageHeader Title="..." />`.
- Cor nova: um token em `tokens.css`; componentes usam `var(--color-...)`. O teste `UiTokensTests` falha se um `.razor` ou `.css` fora de `tokens.css` tiver cor literal.

## Testes

- `LayoutPagesTests` (Web): cada seção mostra o título e destaca só o próprio item; `/` redireciona; favicon declarado; nenhuma referência externa nas páginas e nos CSS; ativos estáticos servidos.
- `MainLayoutTests` (bUnit): a gaveta começa fechada, abre pelo botão, fecha pelo botão, pela película e ao escolher um item.
- `CultureTests`: cultura `pt-BR`, `R$ 1.234,56` e `05/01/2027`.
- `UiTokensTests` (arquitetura): sem cores literais fora de `tokens.css`; a paleta documentada está nos tokens.

## Limites conhecidos

- A gaveta depende do circuito Blazor estar conectado; antes disso (primeiros instantes) o botão ainda não responde.
- Login (#36), PWA (#38) e tema claro/automático (#69) estão entregues.
- Carteira visual (#55), arquivamento reversível (#53), pendentes (#57) e dashboard de pendências (#58) estão entregues.
