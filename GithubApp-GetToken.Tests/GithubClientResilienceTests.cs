using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using GithubApp_GetToken;
using GithubApp_GetToken.Config;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GithubApp_GetToken.Tests;

public class GithubClientResilienceTests
{
    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.RequestTimeout)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task GetRetriesTransientFailureAndPreservesAuthentication(HttpStatusCode status)
    {
        var handler = new StubHandler((request, attempt) =>
        {
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.False(string.IsNullOrEmpty(request.Headers.Authorization?.Parameter));
            return attempt == 1 ? Response(status) : Response(HttpStatusCode.OK, "{}");
        });
        using var services = CreateServices(handler);

        await services.GetRequiredService<GithubClient>().GetAppAsync();

        Assert.Equal(2, handler.Attempts);
    }

    [Fact]
    public async Task GetRetriesNetworkFailure()
    {
        var handler = new StubHandler((_, attempt) => attempt == 1
            ? throw new HttpRequestException("Network failure")
            : Response(HttpStatusCode.OK, "{}"));
        using var services = CreateServices(handler);

        await services.GetRequiredService<GithubClient>().GetAppAsync();

        Assert.Equal(2, handler.Attempts);
    }

    [Fact]
    public async Task GetDoesNotRetryNonTransientFailure()
    {
        var handler = new StubHandler((_, _) => Response(HttpStatusCode.BadRequest));
        using var services = CreateServices(handler);

        await Assert.ThrowsAsync<HttpRequestException>(() => services.GetRequiredService<GithubClient>().GetAppAsync());

        Assert.Equal(1, handler.Attempts);
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.Unauthorized)]
    public async Task GetStopsAfterStandardRetryLimit(HttpStatusCode status)
    {
        var handler = new StubHandler((_, _) => Response(status));
        using var services = CreateServices(handler);

        await Assert.ThrowsAsync<HttpRequestException>(() => services.GetRequiredService<GithubClient>().GetAppAsync());

        Assert.Equal(4, handler.Attempts);
    }

    [Fact]
    public async Task CreatingTokenDoesNotRetryTransientFailure()
    {
        var handler = new StubHandler((request, _) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            return Response(HttpStatusCode.ServiceUnavailable);
        });
        using var services = CreateServices(handler);

        await Assert.ThrowsAsync<HttpRequestException>(() => services.GetRequiredService<GithubClient>().GetAccessTokenForInstallation(123));

        Assert.Equal(1, handler.Attempts);
    }

    [Fact]
    public async Task CreatingTokenRetriesUnauthorizedResponse()
    {
        var handler = new StubHandler((request, attempt) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.False(string.IsNullOrEmpty(request.Headers.Authorization?.Parameter));
            Assert.NotEqual("invalid", request.Headers.Authorization?.Parameter);
            if (attempt == 1)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "invalid");
                return Response(HttpStatusCode.Unauthorized);
            }
            return Response(HttpStatusCode.Created, "{\"token\":\"test-token\"}");
        });
        using var services = CreateServices(handler);

        var token = await services.GetRequiredService<GithubClient>().GetAccessTokenForInstallation(123);

        Assert.Equal("test-token", token);
        Assert.Equal(2, handler.Attempts);
    }

    [Fact]
    public async Task CreatingTokenStopsAfterUnauthorizedRetryLimit()
    {
        var handler = new StubHandler((_, _) => Response(HttpStatusCode.Unauthorized));
        using var services = CreateServices(handler);

        await Assert.ThrowsAsync<HttpRequestException>(() => services.GetRequiredService<GithubClient>().GetAccessTokenForInstallation(123));

        Assert.Equal(4, handler.Attempts);
    }

    [Fact]
    public async Task CreatingTokenReturnsSuccessfulResponse()
    {
        var handler = new StubHandler((_, _) => Response(HttpStatusCode.Created, "{\"token\":\"test-token\"}"));
        using var services = CreateServices(handler);

        var token = await services.GetRequiredService<GithubClient>().GetAccessTokenForInstallation(123);

        Assert.Equal("test-token", token);
        Assert.Equal(1, handler.Attempts);
    }

    [Fact]
    public async Task CancellationStopsRetries()
    {
        using var cancellation = new CancellationTokenSource();
        var handler = new StubHandler((_, _) =>
        {
            cancellation.CancelAfter(TimeSpan.FromMilliseconds(100));
            var response = Response(HttpStatusCode.ServiceUnavailable);
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(10));
            return response;
        });
        using var services = CreateServices(handler);
        var client = services.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(GithubClient));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetAsync("/app", cancellation.Token));

        Assert.Equal(1, handler.Attempts);
    }

    private static ServiceProvider CreateServices(HttpMessageHandler handler)
    {
        using var rsa = RSA.Create(2048);
        var privateKey = rsa.ExportRSAPrivateKeyPem();
        var services = new ServiceCollection();
        services.AddLogging();
        services.Configure<Github>(options =>
        {
            options.ClientId = "test-client";
            options.PrivateKey = privateKey;
        });
        services.AddSingleton<GithubUtils>();
        services.AddGithubClient();
        services.AddHttpClient(nameof(GithubClient)).ConfigurePrimaryHttpMessageHandler(() => handler);
        return services.BuildServiceProvider();
    }

    private static HttpResponseMessage Response(HttpStatusCode status, string content = "")
    {
        var response = new HttpResponseMessage(status) { Content = new StringContent(content) };
        response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero);
        return response;
    }

    private sealed class StubHandler(Func<HttpRequestMessage, int, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public int Attempts { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Attempts++;
            return Task.FromResult(respond(request, Attempts));
        }
    }
}
