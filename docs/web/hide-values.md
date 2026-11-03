# Ocultar valores

A [#71](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/71) acrescenta o botão do
"olhinho" dos apps de banco: com ele ligado, todo valor monetário da tela aparece como `R$ ••••`.

## Como funciona

- O botão fica na barra superior (celular) e no menu lateral, acima de "Sair" (computador e gaveta do
  celular). Nome acessível "Ocultar valores" ou "Mostrar valores" e `aria-pressed` acompanham o estado.
  O componente é `Components/Layout/PrivacyToggle.razor`; o comportamento é todo de `wwwroot/js/privacy.js`.
- A escolha é **por dispositivo**: fica no `localStorage` do navegador (chave `ags.hide-values`), não no banco
  e não é sincronizada entre aparelhos. O padrão é mostrar. Outra aba do mesmo navegador acompanha a mudança.
- O script roda no `<head>` e usa um `MutationObserver`. Assim cobre todas as telas de uma vez, sem alterar uma
  por uma, e mascara os nós já durante a leitura da página, antes de o valor aparecer.
- Mascara o padrão `R$ 1.234,56` e `-R$ 50,00` (o formato de `Money.ToString`) em nós de texto e nos atributos
  `title` e `aria-label` (dicas de gráficos). Vale também para o que muda depois: baixa, pagamento, troca de
  mês. Quando o Blazor troca um texto, o valor novo já nasce oculto e o original guardado deixa de valer.
- Desligar restaura os valores originais exatamente como estavam.
- Não são alterados: campos de formulário e `textarea` (o que o usuário digita), porcentagens, contagens,
  datas, nomes, arquivos CSV e backups.
- Sem `localStorage` (janela privada, dados bloqueados), os valores aparecem e o botão funciona até recarregar.

## Limites

O mascaramento é só visual e feito no navegador, sobre o que já foi renderizado. Não protege contra quem tem
acesso ao navegador ou ao código-fonte da página, e as larguras das barras dos gráficos continuam
proporcionais aos valores. Também não oculta automaticamente ao trocar de aba nem por inatividade.

## Testes

O mascaramento é JavaScript sem runtime nos testes de CI. Foi verificado com um harness no Chrome em modo
headless: valor positivo, negativo e zero, atributos, campos de formulário intactos, nós adicionados e
textos alterados depois, restauração do valor mais recente, persistência entre cargas, `localStorage`
indisponível e máscara já aplicada no `DOMContentLoaded`. No CI, `PrivacyToggleTests` cobre o botão no layout
(barra superior e menu), nas páginas autenticadas, o script no `<head>` e a entrega dos arquivos, e
`UiTokensTests` impede cores literais em `.js`.

## Cor da barra do navegador

`js/theme.js` mantém `<meta name="theme-color">` igual à cor de fundo calculada do tema (escuro, claro ou
automático, inclusive quando o sistema muda e a cada página da navegação melhorada do Blazor), sem valores
literais de cor fora de `tokens.css`. O manifesto da PWA, que é um arquivo estático, continua com as cores
escuras na tela de abertura.
