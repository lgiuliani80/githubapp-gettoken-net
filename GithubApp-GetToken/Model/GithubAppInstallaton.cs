using System.Text.Json.Serialization;

namespace GithubApp_GetToken.Model
{
    public class GithubAppInstallaton
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("client_id")]
        public string ClientId { get; set; } = null!;

        [JsonPropertyName("account")]
        public Account Account { get; set; } = null!;

        [JsonPropertyName("repository_selection")]
        public string? RepositorySelection { get; set; }

        [JsonPropertyName("access_tokens_url")]
        public string AccessTokensUrl { get; set; } = null!;

        [JsonPropertyName("repositories_url")]
        public string RepositoriesUrl { get; set; } = null!;

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; set; }

        [JsonPropertyName("app_id")]
        public int AppId { get; set; }

        [JsonPropertyName("app_slug")]
        public string AppSlug { get; set; } = null!;

        [JsonPropertyName("target_id")]
        public int TargetId { get; set; }

        [JsonPropertyName("target_type")]
        public string TargetType { get; set; } = null!;

        [JsonPropertyName("permissions")]
        public Dictionary<string, string> Permissions { get; set; } = [];

        [JsonPropertyName("events")]
        public object[] Events { get; set; } = [];

        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("updated_at")]
        public DateTime UpdatedAt { get; set; }

        [JsonPropertyName("single_file_name")]
        public object? SingleFileName { get; set; }

        [JsonPropertyName("has_multiple_single_files")]
        public bool HasMultipleSingleFiles { get; set; }

        [JsonPropertyName("single_file_paths")]
        public object[] SingleFilePaths { get; set; } = [];

        [JsonPropertyName("suspended_by")]
        public object? SuspendedBy { get; set; }

        [JsonPropertyName("suspended_at")]
        public object? SuspendedAt { get; set; }
    }

    public class Account
    {
        [JsonPropertyName("login")]
        public string Login { get; set; } = null!;

        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("node_id")]
        public string NodeId { get; set; } = null!;

        [JsonPropertyName("avatar_url")]
        public string? AvatarUrl { get; set; }

        [JsonPropertyName("gravatar_id")]
        public string? GravatarId { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; } = null!;

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; set; }

        [JsonPropertyName("followers_url")]
        public string? FollowersUrl { get; set; }

        [JsonPropertyName("following_url")]
        public string? FollowingUrl { get; set; }

        [JsonPropertyName("gists_url")]
        public string? GistsUrl { get; set; }

        [JsonPropertyName("starred_url")]
        public string? StarredUrl { get; set; }

        [JsonPropertyName("subscriptions_url")]
        public string? SubscriptionsUrl { get; set; }

        [JsonPropertyName("organizations_url")]
        public string OrganizationsUrl { get; set; } = null!;

        [JsonPropertyName("repos_url")]
        public string ReposUrl { get; set; } = null!;

        [JsonPropertyName("events_url")]
        public string? EventsUrl { get; set; }

        [JsonPropertyName("received_events_url")]
        public string? ReceivedEventsUrl { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; } = null!;

        [JsonPropertyName("user_view_type")]
        public string? UserViewType { get; set; }

        [JsonPropertyName("site_admin")]
        public bool SiteAdmin { get; set; }
    }

}

public class InstallationTokenResponse
{
    public string Token { get; set; } = null!;
}
