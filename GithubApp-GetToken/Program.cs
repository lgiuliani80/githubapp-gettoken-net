using GithubApp_GetToken;
using GithubApp_GetToken.Config;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Identity.Web;
using Microsoft.IdentityModel.Logging;
using System.Reflection;

const int INSTALLATION_CACHE_DURATION_HOURS = 1;

var builder = WebApplication.CreateBuilder(args);

var authBuilder = builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme);

IdentityModelEventSource.ShowPII = true;

if (builder.Configuration["AzureAd:TenantId"] is not null)
{
    authBuilder.AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"),
        subscribeToJwtBearerMiddlewareDiagnosticsEvents: true);
}
else if (builder.Configuration.GetSection("OpenId").GetChildren().Any())
{
    authBuilder.AddJwtBearer(opt => builder.Configuration.Bind("OpenId", opt));
}
else if (builder.Configuration.GetSection("JWT").GetChildren().Any())
{
    authBuilder.AddJwtBearer(opt => builder.Configuration.Bind("JWT", opt));
}

builder.Services.AddAuthorization();
builder.Services.AddHealthChecks().AddCheck<ContactGithub>("ContactGithub");

builder.Services.AddDistributedMemoryCache(); // Can be replaced with a true distributed cache, like Redis
builder.Services.AddOpenApi();

builder.Services.Configure<Github>(builder.Configuration.GetSection("Github"));
builder.Services.AddSingleton<GithubUtils>();

builder.Services.AddGithubClient();

var app = builder.Build();

if (app.Environment.IsDevelopment() || app.Configuration.GetValue("MapOpenApi", false))
{
    app.MapOpenApi();
}

var authenticated = app.Configuration.GetValue("RequireAuthentication", true);

static async Task PopulateCache(IDistributedCache cache, GithubClient cli)
{
    var installations = await cli.GetAppInstallationsAsync();

    foreach (var item in installations)
    {
        await cache.SetStringAsync(item.Account.Login, item.Id.ToString(), new DistributedCacheEntryOptions 
        { 
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(INSTALLATION_CACHE_DURATION_HOURS) 
        });
    }
}

//app.UseHttpsRedirection();

app.MapHealthChecks("/healthz");

app.MapGet("/version", () =>
{
    return new
    {
        ProductName = Assembly.GetEntryAssembly()!.GetCustomAttribute<AssemblyProductAttribute>()?.Product,
        AssemblyName = Assembly.GetEntryAssembly()!.GetName().Name,
        Assembly.GetEntryAssembly()!.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version,
        NETRuntime = Environment.Version.ToString(),
        OperatingSystem = Environment.OSVersion.VersionString
    };

})
.WithName("GetVersion")
.WithOpenApi();

app.MapGet("/jwt", (GithubUtils gh) =>
{
    return Results.Content(gh.GetJWTToken(), "text/plain");
})
.WithName("GetGithubAppJwt")
.WithOpenApi()
.ConditionallyRequireAuthorization(authenticated);

app.MapGet("/installations", async (GithubClient cli) =>
{
    return Results.Ok(await cli.GetAppInstallationsAsync());
})
.WithName("GetGithubAppInstallations")
.WithOpenApi()
.ConditionallyRequireAuthorization(authenticated);

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
.WithOpenApi()
.ConditionallyRequireAuthorization(authenticated);

app.Run();

class ContactGithub(GithubClient ghcli) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await ghcli.PingGithub();
            return HealthCheckResult.Healthy("Able to contact GitHub");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Error contacting GitHub", ex);
        }
    }
}

public static class EndpointConventionBuilderBuilderExtension
{
    public static TBuilder ConditionallyRequireAuthorization<TBuilder>(this TBuilder builder, bool requireAuthorization) where TBuilder : IEndpointConventionBuilder
    {
        if (requireAuthorization)
        {
            builder.RequireAuthorization();
        }

        return builder;
    }
}