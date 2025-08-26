using GithubApp_GetToken.Model;
using System.Net.Http.Headers;
using System.Reflection;
using System.Web;

namespace GithubApp_GetToken;

public class GithubClient(ILogger<GithubClient> logger, HttpClient htcli)
{
    internal static readonly string CLIENT_NAME = "lg-net-github-client";
    internal static readonly string CLIENT_VERSION = 
        Assembly.GetEntryAssembly()!.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version ?? "0.1";

    public async Task PingGithub()
    {
        var response = await htcli.GetAsync("/app/installations?per_page=1");
        response.EnsureSuccessStatusCode();
    }

    public async Task<AppResponse?> GetAppAsync()
    {
        return await htcli.GetFromJsonAsync<AppResponse>("/app");
    }

    public async Task<GithubAppInstallaton[]> GetAppInstallationsAsync()
    {
        return await htcli.GetFromJsonAsync<GithubAppInstallaton[]>("/app/installations") ?? [];
    }

    public async Task<string> GetAccessTokenForInstallation(long installationId)
    {
        var response = await htcli.PostAsJsonAsync($"/app/installations/{installationId}/access_tokens", new { });
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<InstallationTokenResponse>())?.Token 
            ?? throw new Exception("Unable to retrieve token");
    }
}

class GithubAppBearerTokenHandler(GithubUtils utils) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = utils.GetJWTToken();
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return base.SendAsync(request, cancellationToken);
    }
}

public static class GithubClientExtensions
{
    public static IServiceCollection AddGithubClient(this IServiceCollection services)
    {
        services.AddTransient<GithubAppBearerTokenHandler>();

        services.AddHttpClient<GithubClient>(htcli =>
        {
            htcli.BaseAddress = new Uri("https://api.github.com");
            htcli.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            htcli.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(GithubClient.CLIENT_NAME, GithubClient.CLIENT_VERSION));
            htcli.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");

        }).AddHttpMessageHandler<GithubAppBearerTokenHandler>();

        return services;
    }
}



