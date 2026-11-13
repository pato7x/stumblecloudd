using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
var protector = app.Services
    .GetRequiredService<IDataProtectionProvider>()
    .CreateProtector("StumbleCloud.DiscordProfile.v1");
using var http = new HttpClient();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/auth/discord", (HttpContext context) =>
{
    if (!TryGetOAuthSettings(context, out var settings, out var error))
        return Results.Problem(error, statusCode: StatusCodes.Status503ServiceUnavailable);

    var state = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    context.Response.Cookies.Append("sc_discord_state", state, new CookieOptions
    {
        HttpOnly = true,
        Secure = app.Environment.IsProduction() || context.Request.IsHttps,
        SameSite = SameSiteMode.Lax,
        Path = "/",
        MaxAge = TimeSpan.FromMinutes(10)
    });

    var authorizeUrl = "https://discord.com/oauth2/authorize" +
        $"?client_id={Uri.EscapeDataString(settings.ClientId)}" +
        "&response_type=code" +
        $"&redirect_uri={Uri.EscapeDataString(settings.RedirectUri)}" +
        "&scope=identify" +
        $"&state={Uri.EscapeDataString(state)}";

    return Results.Redirect(authorizeUrl);
});

app.MapGet("/auth/discord/callback", async (HttpContext context) =>
{
    if (!TryGetOAuthSettings(context, out var settings, out var error))
        return Results.Problem(error, statusCode: StatusCodes.Status503ServiceUnavailable);

    var receivedState = context.Request.Query["state"].ToString();
    var savedState = context.Request.Cookies["sc_discord_state"];
    context.Response.Cookies.Delete("sc_discord_state", new CookieOptions { Path = "/" });

    if (string.IsNullOrEmpty(receivedState) || string.IsNullOrEmpty(savedState) ||
        !FixedTimeEquals(receivedState, savedState))
    {
        return Results.BadRequest("A validação de segurança do login falhou. Tente conectar novamente.");
    }

    if (context.Request.Query.ContainsKey("error"))
        return Results.Redirect("/?discord=cancelled");

    var code = context.Request.Query["code"].ToString();
    if (string.IsNullOrWhiteSpace(code))
        return Results.BadRequest("O Discord não retornou o código de autorização. Tente novamente.");

    try
    {
        using var tokenResponse = await http.PostAsync(
            "https://discord.com/api/v10/oauth2/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = settings.ClientId,
                ["client_secret"] = settings.ClientSecret,
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = settings.RedirectUri
            }),
            context.RequestAborted);

        if (!tokenResponse.IsSuccessStatusCode)
            return Results.Problem("O Discord recusou a troca do código de login. Confira a URL de retorno cadastrada.", statusCode: StatusCodes.Status502BadGateway);

        var token = await tokenResponse.Content.ReadFromJsonAsync<DiscordToken>(cancellationToken: context.RequestAborted);
        if (string.IsNullOrWhiteSpace(token?.AccessToken))
            return Results.Problem("O Discord não retornou um token de acesso.", statusCode: StatusCodes.Status502BadGateway);

        using var userRequest = new HttpRequestMessage(HttpMethod.Get, "https://discord.com/api/v10/users/@me");
        userRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        using var userResponse = await http.SendAsync(userRequest, context.RequestAborted);
        if (!userResponse.IsSuccessStatusCode)
            return Results.Problem("Não consegui ler o perfil autorizado do Discord.", statusCode: StatusCodes.Status502BadGateway);

        var user = await userResponse.Content.ReadFromJsonAsync<DiscordUser>(cancellationToken: context.RequestAborted);
        if (user is null || string.IsNullOrWhiteSpace(user.Id) || string.IsNullOrWhiteSpace(user.Username))
            return Results.Problem("O perfil recebido do Discord está incompleto.", statusCode: StatusCodes.Status502BadGateway);

        var name = string.IsNullOrWhiteSpace(user.GlobalName) ? user.Username : user.GlobalName;
        var avatarUrl = BuildAvatarUrl(user);
        var profile = new ConnectedProfile(user.Id, name, user.Username, avatarUrl);
        var protectedProfile = protector.Protect(JsonSerializer.Serialize(profile));

        context.Response.Cookies.Append("sc_discord_profile", protectedProfile, new CookieOptions
        {
            HttpOnly = true,
            Secure = app.Environment.IsProduction() || context.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            MaxAge = TimeSpan.FromDays(7)
        });
        context.Response.Headers.CacheControl = "no-store";
        return Results.Redirect("/");
    }
    catch (Exception exception) when (exception is HttpRequestException or JsonException or CryptographicException)
    {
        return Results.Problem("Não foi possível concluir o login com o Discord. Tente novamente.", statusCode: StatusCodes.Status502BadGateway);
    }
});

app.MapGet("/api/me", (HttpContext context) =>
{
    context.Response.Headers.CacheControl = "no-store";
    var protectedProfile = context.Request.Cookies["sc_discord_profile"];
    if (string.IsNullOrWhiteSpace(protectedProfile))
        return Results.Unauthorized();

    try
    {
        var json = protector.Unprotect(protectedProfile);
        var profile = JsonSerializer.Deserialize<ConnectedProfile>(json);
        return profile is null ? Results.Unauthorized() : Results.Json(profile);
    }
    catch (Exception exception) when (exception is CryptographicException or JsonException)
    {
        context.Response.Cookies.Delete("sc_discord_profile", new CookieOptions { Path = "/" });
        return Results.Unauthorized();
    }
});

app.MapGet("/auth/logout", (HttpContext context) =>
{
    context.Response.Cookies.Delete("sc_discord_profile", new CookieOptions { Path = "/" });
    return Results.Redirect("/");
});

app.Run();

static bool TryGetOAuthSettings(HttpContext context, out OAuthSettings settings, out string error)
{
    var configuration = context.RequestServices.GetRequiredService<IConfiguration>();
    var clientId = configuration["DISCORD_CLIENT_ID"];
    var clientSecret = configuration["DISCORD_CLIENT_SECRET"];
    var redirectUri = configuration["DISCORD_REDIRECT_URI"];

    if (string.IsNullOrWhiteSpace(clientId) ||
        string.IsNullOrWhiteSpace(clientSecret) ||
        string.IsNullOrWhiteSpace(redirectUri) ||
        !Uri.TryCreate(redirectUri, UriKind.Absolute, out _))
    {
        settings = default!;
        error = "Configure as variáveis DISCORD_CLIENT_ID, DISCORD_CLIENT_SECRET e DISCORD_REDIRECT_URI no servidor.";
        return false;
    }

    settings = new OAuthSettings(clientId, clientSecret, redirectUri);
    error = string.Empty;
    return true;
}

static bool FixedTimeEquals(string left, string right)
{
    var leftBytes = Encoding.UTF8.GetBytes(left);
    var rightBytes = Encoding.UTF8.GetBytes(right);
    return leftBytes.Length == rightBytes.Length && CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
}

static string BuildAvatarUrl(DiscordUser user)
{
    if (!string.IsNullOrWhiteSpace(user.Avatar))
    {
        var extension = user.Avatar.StartsWith("a_", StringComparison.Ordinal) ? "gif" : "png";
        return $"https://cdn.discordapp.com/avatars/{Uri.EscapeDataString(user.Id)}/{Uri.EscapeDataString(user.Avatar)}.{extension}?size=128";
    }

    var defaultAvatar = ulong.TryParse(user.Id, out var userId) ? (userId >> 22) % 6 : 0;
    return $"https://cdn.discordapp.com/embed/avatars/{defaultAvatar}.png";
}

sealed record OAuthSettings(string ClientId, string ClientSecret, string RedirectUri);
sealed record ConnectedProfile(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("displayName")] string DisplayName,
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("avatarUrl")] string AvatarUrl);
sealed record DiscordToken([property: JsonPropertyName("access_token")] string AccessToken);
sealed record DiscordUser(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("global_name")] string? GlobalName,
    [property: JsonPropertyName("avatar")] string? Avatar);
