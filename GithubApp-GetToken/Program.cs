using GithubApp_GetToken;
using GithubApp_GetToken.Config;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Identity.Web;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));
builder.Services.AddAuthorization();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddOpenApi();

builder.Services.Configure<Github>(builder.Configuration.GetSection("Github"));
builder.Services.AddSingleton<GithubUtils>();

builder.Services.AddGithubClient();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

static async Task PopulateCache(IDistributedCache cache, GithubClient cli)
{
    var installations = await cli.GetAppInstallationsAsync();

    foreach (var item in installations)
    {
        await cache.SetStringAsync(item.Account.Login, item.Id.ToString(), new DistributedCacheEntryOptions 
        { 
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1) 
        });
    }
}

//app.UseHttpsRedirection();

app.MapGet("/jwt", (GithubUtils gh) =>
{
    return Results.Content(gh.GetJWTToken(), "text/plain");
})
.WithName("GetGithubAppJwt")
.WithOpenApi();
//.RequireAuthorization();

app.MapGet("/installations", async (GithubClient cli) =>
{
    return Results.Ok(await cli.GetAppInstallationsAsync());
})
.WithName("GetGithubAppInstallations")
.WithOpenApi();
//.RequireAuthorization();


app.MapGet("/installations/{org}/token", static async (string org,
    ILogger<Program> logger, IDistributedCache installationCache, GithubClient ghcli) =>
{
    long installationId;

    if (long.TryParse(org, out long id))
    {
        installationId = id;
    }
    else
    {
        var retrieved = await installationCache.GetStringAsync(org);
        if (retrieved is null)
        {
            await PopulateCache(installationCache, ghcli);
            retrieved = await installationCache.GetStringAsync(org);
        }
        if (retrieved is null)
        {
            return Results.NotFound(new { error = "Organization not found in installations" });
        }

        installationId = long.Parse(retrieved);
    }

    return Results.Content(await ghcli.GetAccessTokenForInstallation(installationId), "text/plain");
})
.WithName("GetGithubInstallationToken")
.WithOpenApi();
//.RequireAuthorization();

app.Run();
