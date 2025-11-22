using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace FastP.Services
{
    public class UpdateService
    {
        private const string RepoOwner = "Deithand";
        private const string RepoName = "FastP";
        private const string UpdateUrl = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";

        public async Task<UpdateInfo?> CheckForUpdatesAsync()
        {
            try
            {
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("FastP", "1.0"));

                    var response = await client.GetAsync(UpdateUrl);
                    if (!response.IsSuccessStatusCode)
                        return null;

                    var json = await response.Content.ReadAsStringAsync();
                    var release = JsonSerializer.Deserialize<GitHubRelease>(json);

                    if (release == null) return null;

                    var currentVersion = Assembly.GetExecutingAssembly().GetName().Version;
                    
                    // Очищаем тег версии от префикса 'v', если он есть (например, v1.1.0 -> 1.1.0)
                    var tagName = release.TagName?.TrimStart('v') ?? "0.0.0";
                    
                    if (Version.TryParse(tagName, out var latestVersion))
                    {
                        if (latestVersion > currentVersion)
                        {
                            return new UpdateInfo
                            {
                                Version = release.TagName,
                                Description = release.Body,
                                DownloadUrl = release.HtmlUrl // Ведем пользователя на страницу релиза
                            };
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Update check failed: {ex.Message}");
            }
            return null;
        }
    }

    public class UpdateInfo
    {
        public string? Version { get; set; }
        public string? Description { get; set; }
        public string? DownloadUrl { get; set; }
    }

    // Классы для десериализации ответа GitHub
    internal class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }

        [JsonPropertyName("body")]
        public string? Body { get; set; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; set; }
    }
}

