# Stumble Cloud

Site ASP.NET Core 8 com dashboard responsivo e login Discord. O projeto está pronto para entrar em um repositório GitHub e publicar como serviço Docker no Render.

## Colocar no GitHub

Crie um repositório no GitHub, extraia os arquivos deste pacote e envie o conteúdo da pasta para o repositório. Não envie `bin`, `obj` nem segredos; o `.gitignore` já exclui arquivos locais e `.env`.

## Publicar no Render

1. Entre em Render e conecte sua conta GitHub.
2. Escolha **New → Blueprint**, selecione o repositório `StumbleCloud` e deixe o Render ler `render.yaml`.
3. Abra as configurações do serviço criado e defina `DISCORD_CLIENT_ID`, `DISCORD_CLIENT_SECRET` e `DISCORD_REDIRECT_URI` como variáveis de ambiente secretas.
4. Use como `DISCORD_REDIRECT_URI` o endereço do Render terminado em `/auth/discord/callback`, por exemplo `https://SEU-SITE.onrender.com/auth/discord/callback`.
5. No Discord Developer Portal, abra **OAuth2 → Redirects** e cadastre exatamente essa mesma URL.
6. Salve e faça o deploy. Abra o endereço `onrender.com` e teste **Conectar Discord**.

O `client secret` fica somente nas variáveis do serviço; nunca o coloque em arquivos do repositório nem compartilhe publicamente. O plano gratuito do Render pode suspender o serviço após 15 minutos sem acessos e levar cerca de um minuto para iniciar quando alguém voltar. Consulte as [limitações do plano gratuito](https://render.com/docs/free).

GitHub Pages sozinho não executa este backend ASP.NET. O GitHub guarda o código; o Render executa o site e o fluxo OAuth. Consulte a [documentação do GitHub Pages](https://docs.github.com/en/pages/getting-started-with-github-pages/what-is-github-pages).

## Executar localmente

```powershell
$env:DISCORD_CLIENT_ID = "COLE_O_CLIENT_ID"
$env:DISCORD_CLIENT_SECRET = "COLE_O_CLIENT_SECRET"
$env:DISCORD_REDIRECT_URI = "http://localhost:5080/auth/discord/callback"
dotnet run --urls http://localhost:5080
```

Cadastre `http://localhost:5080/auth/discord/callback` em **OAuth2 → Redirects** e abra `http://localhost:5080`.

As estatísticas e resultados mostrados ainda são exemplos estáticos; apenas nome e avatar vêm da conta Discord conectada.
