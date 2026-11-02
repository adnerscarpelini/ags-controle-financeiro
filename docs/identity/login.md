# Login com usuário e senha

Origem: [#36](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/36). O usuário é criado por comando, veja [user-creation.md](user-creation.md). Decisão geral em [web-hosting.md](../architecture/web-hosting.md).

## Comportamento

- **Tudo exige login.** Uma política de fallback (`RequireAuthenticatedUser`) vale para todo endpoint, e `_Imports.razor` aplica `[Authorize]` a todo componente. A única exceção é a página `/login` (`[AllowAnonymous]`); os arquivos estáticos (CSS, fonte, imagens e `_framework/blazor.web.js`) são atendidos antes da autenticação.
- **Acesso direto sem sessão** redireciona para `/login?ReturnUrl=<caminho>` e, depois de entrar, volta ao caminho. Só caminhos da própria aplicação são aceitos como `ReturnUrl`: `https://...`, `//host`, `/\host` e caminhos sem `/` inicial caem em `/`.
- **Sem nenhum usuário cadastrado**, `/login` mostra a orientação de rodar `Ags.Finance.Web user create` no computador do serviço e não oferece formulário nem cadastro.
- **Mensagens:** senha errada e usuário inexistente dão a mesma resposta ("Usuário ou senha incorretos."), e o tempo de resposta também não distingue os dois casos (o caso sem usuário gasta o tempo de um hash).
- **Bloqueio:** cinco tentativas erradas seguidas bloqueiam novas tentativas por 5 minutos, mesmo com a senha certa. Tentativas durante o bloqueio não o prolongam. Uma entrada correta zera a contagem. O estado fica no banco (`FailedSignInCount`, `LockoutEnd`) e sobrevive a reinício do serviço. Só conta quando o usuário existe, para quem não conhece o nome de usuário não conseguir bloquear o dono.
- **Sessão:** cookie `ags.session` (`HttpOnly`, `Secure`, `SameSite=Lax`). Sem "Manter conectado", é cookie de sessão do navegador e o ticket vale 12 horas. Com a opção marcada, o cookie é persistente e vale 30 dias, inclusive depois de fechar e abrir o navegador. A validade não é renovada (sem expiração deslizante).
- **Sair:** botão "Sair" no fim do menu (`POST /account/sign-out`). Todas as respostas da aplicação (páginas e endpoints, menos arquivos estáticos) saem com `Cache-Control: no-store`, então o botão "voltar" do navegador não exibe páginas guardadas.
- **Trocar a senha logado:** em Configurações, informando a senha atual, a nova e a confirmação (`POST /account/password`). A sessão atual continua com o mesmo prazo; todas as outras terminam. Errar a senha atual conta para o bloqueio, como no login.

## Como a sessão deixa de valer

O cookie guarda o `ExternalId` do usuário e o `SecurityStamp`. A cada requisição `SessionCookieEvents` pergunta ao módulo Identity (`IValidateSession`) se o carimbo ainda é o do usuário; trocar a senha, pelo comando `user reset-password` ou na tela, gera um carimbo novo e derruba as sessões antigas na próxima requisição. Telas já abertas (circuito Blazor) são reconferidas a cada minuto por `RevalidatingSessionStateProvider` e recarregam para o login.

## Endpoints

| Rota | Quem acessa | O que faz |
| --- | --- | --- |
| `GET /login` | qualquer um | Página de login (renderização estática, sem circuito) |
| `POST /account/sign-in` | qualquer um | Autentica e emite o cookie |
| `POST /account/sign-out` | logado | Encerra a sessão |
| `POST /account/password` | logado | Troca a senha, informando a atual |

Os três `POST` exigem o token antifalsificação (o middleware valida e o grupo recusa com 400 quando falta, inclusive o `sign-out`, que não lê formulário). **Não existe rota que crie usuário nem que redefina a senha sem sessão**; criar e redefinir só existem nos comandos, e um teste de arquitetura impede que código do host fora de `Commands/` use `ICreateUser` ou `IResetPassword`.

## Arquivos

| Arquivo | Papel |
| --- | --- |
| `Authentication/AuthenticationSetup.cs` | Cookie, política de fallback, estado de autenticação em cascata e o middleware de `no-store` |
| `Authentication/AccountEndpoints.cs` | Os três endpoints acima |
| `Authentication/SessionCookieEvents.cs`, `RevalidatingSessionStateProvider.cs` | Validação do carimbo (HTTP e circuito) |
| `Authentication/SessionClaims.cs`, `SessionPolicy.cs`, `ReturnUrl.cs` | Identidade do cookie, prazos de 12 horas e 30 dias, saneamento do retorno |
| `Components/Pages/Login.razor`, `Layout/LoginLayout.razor`, `RedirectToLogin.razor` | Tela de login e redirecionamento quando a sessão acaba com a tela aberta |
| `Modules/Identity/Application/{SignIn,ChangePassword,ValidateSession}` | Casos de uso; `Domain/User` guarda a regra do bloqueio |

## Testes

- `UserTests`, `SignInTests`, `ChangePasswordTests` (Identity): bloqueio na quinta tentativa, limites de 4 e de 5 minutos, recomeço da contagem, usuário inexistente igual a senha errada, carimbo de segurança.
- `LoginTests` (Web): entrar, mensagem genérica, bloqueio mesmo com a senha certa, volta ao `/lancamentos`, retorno malicioso, cookie de sessão x persistente de 30 dias, `HttpOnly`/`Secure`/`SameSite`, sair, `no-store`, trocar a senha derruba as outras sessões, redefinição pelo comando derruba a sessão aberta, orientação sem usuário.
- `AccessControlTests` (Web): toda página roteável do projeto (descoberta por reflexão) redireciona para o login, com e sem usuário cadastrado; só `/login` e `/account/sign-in` são anônimos; a lista de endpoints que gravam é exatamente `sign-in`, `sign-out` e `password`; onze rotas candidatas a cadastro não criam usuário, logado ou não; `POST` sem token é recusado; nenhuma senha aparece em log, com o nível `Trace`.

## Limites conhecidos

- O cookie é autocontido: sair apaga o cookie do navegador, mas uma cópia roubada do cookie continua valendo até expirar ou até a senha mudar. Trocar a senha (`user reset-password`) é o jeito de revogar todas as sessões.
- Telas abertas em outro dispositivo só percebem a troca de senha em até 1 minuto (revalidação do circuito).
- Não há limite de tentativas por endereço de origem, só o bloqueio do usuário (que vale para todos os endereços).
