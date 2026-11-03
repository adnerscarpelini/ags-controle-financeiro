# Instalação como PWA

A [#38](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/38) permite abrir a
aplicação no celular como um app: ícone na tela inicial e tela cheia, sem a barra do navegador.

## O que existe

| Peça | Arquivo | Função |
| --- | --- | --- |
| Manifesto | `wwwroot/manifest.webmanifest` | Nome, `display: standalone`, `start_url` e escopo `/`, `background_color` = token `Background` (`#121212`), `theme_color` = token `Accent` (`#10B981`), ícones 192, 512 e maskable 512. |
| Ícones | `wwwroot/img/icon-*.png`, `apple-touch-icon.png` | Gerados do logo do projeto sobre o fundo `Background`; o maskable usa margem para a área segura. |
| Service worker | `wwwroot/sw.js` | Mínimo: só trata navegações e, se o servidor não responde, mostra a página offline. Não guarda nem serve dados; páginas, circuito e recursos vão sempre ao servidor. |
| Página offline | `wwwroot/offline.html` | "Não encontrei o servidor", com o motivo provável e o botão de tentar de novo. Usa `tokens.css`, guardado no cache só para isso. |
| Registro | `wwwroot/js/pwa.js`, tags em `App.razor` | Registra o service worker em contexto seguro; `manifest`, `apple-touch-icon` e `viewport-fit=cover` no `<head>`. |

Os arquivos da PWA são estáticos e portanto públicos; **todas as páginas e endpoints de dados continuam exigindo login**
(`AccessControlTests`, `PwaTests`). Nada é carregado de CDN. As cores do manifesto são as dos tokens escuros (conferidas em teste); a cor da barra do navegador acompanha o tema por `js/theme.js` ([#71](hide-values.md)); o `<head>` não repete cores literais, conforme a regra de tokens.

## Requisito: HTTPS confiado no aparelho

Navegadores só instalam PWA e registram service worker em contexto seguro. Pela rede local isso exige abrir a
aplicação pelo endereço HTTPS publicado do host (ver [HTTPS](https-certificate.md) e
[Docker](docker-deployment.md)) com o certificado local **confiado no dispositivo**. Sem confiança no certificado
o navegador avisa e o botão de instalar não aparece; o app continua funcionando normalmente na aba do navegador.

## Uso no celular

Entrada de lançamento, Faturas e Dashboard usam a grade responsiva: campos com alvo de toque de 44 px, texto de
16 px nos controles (o iOS não dá zoom ao focar) e colunas que se empilham no celular, sem rolagem horizontal da página;
tabelas largas rolam dentro do próprio painel. No app instalado a área segura do aparelho é respeitada.

Funcionamento offline com sincronização e notificações push estão fora do escopo.

## Roteiro de validação em campo

O que foi **verificado automaticamente**: manifesto e ícones válidos, cores iguais aos tokens, service worker sem cache de dados,
página offline, tags no `<head>` e exigência de login (`PwaTests`). O que **só pode ser comprovado no aparelho** e ainda
não foi feito nesta entrega:

1. No Docker (Linux, ou Windows via WSL 2) com HTTPS conforme a #59, abrir `https://<endereço do host>` no celular da mesma rede
   com o certificado confiado.
2. Android/Chrome: menu → **Instalar app**; conferir ícone na tela inicial e abertura sem barra do navegador.
3. iPhone/Safari: **Compartilhar → Adicionar à Tela de Início**; conferir ícone e tela cheia.
4. Parar o servidor (`docker compose stop`) e abrir o app instalado: deve aparecer "Não encontrei o servidor".
5. Em Lançamentos, lançar uma despesa só com toque, sem zoom nem rolagem horizontal.
6. Conferir Dashboard e Faturas no celular.

## Interface no celular (#103)

O PWA usa a mesma interface responsiva: abaixo de 768 px, gaveta, **+ Novo** flutuante, janelas em tela cheia e tabelas em cards. Veja [layout e navegação](layout-and-navigation.md). As folgas de área segura do aparelho (`safe-area-inset-*`) valem também para o botão flutuante e para o rodapé das janelas.
