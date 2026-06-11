using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.JSInterop;

namespace OutingLeaderboard.Data;

public class PlayerEntry
{
    public string Name { get; set; } = "";
    public int SmashWins { get; set; }
    public int MagicWins { get; set; }
    public int PickleballWins { get; set; }

    // magic pays 3: a commander win means you beat three people
    [JsonIgnore]
    public int Points => SmashWins + PickleballWins + 3 * MagicWins;
}

public class ScoreBoardData
{
    public string EventName { get; set; } = "COMPANY OUTING";
    public DateTime UpdatedAt { get; set; }
    public List<PlayerEntry> Players { get; set; } = new();
}

public class GitHubConfig
{
    public string Owner { get; set; } = "moontourist";
    public string Repo { get; set; } = "haloboard";
    public string Branch { get; set; } = "main";
    public string Path { get; set; } = "wwwroot/data/scores.json";
    public string Token { get; set; } = "";
}

/// <summary>
/// Score data access. Reads the newer of the deployed scores.json and the
/// browser's localStorage copy (admin edits land in localStorage first), and
/// can publish by committing scores.json back to the repo via the GitHub API.
/// </summary>
public class ScoreStore(HttpClient http, IJSRuntime js)
{
    private const string ScoresKey = "leaderboard-scores";
    private const string ConfigKey = "leaderboard-github";

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public async Task<ScoreBoardData?> LoadAsync()
    {
        ScoreBoardData? remote = null;
        ScoreBoardData? local = null;
        try
        {
            remote = await http.GetFromJsonAsync<ScoreBoardData>($"data/scores.json?t={DateTime.UtcNow.Ticks}");
        }
        catch { }
        try
        {
            var raw = await js.InvokeAsync<string?>("localStorage.getItem", ScoresKey);
            if (!string.IsNullOrWhiteSpace(raw))
                local = JsonSerializer.Deserialize<ScoreBoardData>(raw, JsonOpts);
        }
        catch { }

        if (local is null) return remote;
        if (remote is null) return local;
        return local.UpdatedAt >= remote.UpdatedAt ? local : remote;
    }

    public async Task SaveLocalAsync(ScoreBoardData data)
    {
        data.UpdatedAt = DateTime.UtcNow;
        await js.InvokeVoidAsync("localStorage.setItem", ScoresKey, JsonSerializer.Serialize(data, JsonOpts));
    }

    public async Task ClearLocalAsync()
        => await js.InvokeVoidAsync("localStorage.removeItem", ScoresKey);

    public async Task<GitHubConfig> LoadConfigAsync()
    {
        try
        {
            var raw = await js.InvokeAsync<string?>("localStorage.getItem", ConfigKey);
            if (!string.IsNullOrWhiteSpace(raw))
                return JsonSerializer.Deserialize<GitHubConfig>(raw, JsonOpts) ?? new();
        }
        catch { }
        return new();
    }

    public async Task SaveConfigAsync(GitHubConfig cfg)
        => await js.InvokeVoidAsync("localStorage.setItem", ConfigKey, JsonSerializer.Serialize(cfg, JsonOpts));

    public async Task<string> PushToGitHubAsync(ScoreBoardData data, GitHubConfig cfg)
    {
        data.UpdatedAt = DateTime.UtcNow;
        var url = $"https://api.github.com/repos/{cfg.Owner.Trim()}/{cfg.Repo.Trim()}/contents/{cfg.Path.Trim()}";

        // the contents API needs the current blob sha to update an existing file
        string? sha = null;
        using (var get = NewRequest(HttpMethod.Get, $"{url}?ref={cfg.Branch.Trim()}", cfg))
        {
            var resp = await http.SendAsync(get);
            if (resp.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
                sha = doc.RootElement.GetProperty("sha").GetString();
            }
            else if (resp.StatusCode != System.Net.HttpStatusCode.NotFound)
            {
                return $"ERROR {(int)resp.StatusCode} reading {cfg.Path}: {await resp.Content.ReadAsStringAsync()}";
            }
        }

        var json = JsonSerializer.Serialize(data, JsonOpts);
        var body = new
        {
            message = $"Update scores ({data.UpdatedAt:yyyy-MM-dd HH:mm} UTC)",
            content = Convert.ToBase64String(Encoding.UTF8.GetBytes(json)),
            branch = cfg.Branch.Trim(),
            sha,
        };
        using var put = NewRequest(HttpMethod.Put, url, cfg);
        put.Content = JsonContent.Create(body, options: JsonOpts);
        var putResp = await http.SendAsync(put);
        if (!putResp.IsSuccessStatusCode)
            return $"ERROR {(int)putResp.StatusCode}: {await putResp.Content.ReadAsStringAsync()}";

        // keep the local copy identical to what was committed
        await js.InvokeVoidAsync("localStorage.setItem", ScoresKey, json);
        return "OK: committed scores.json — Pages redeploys in ~1 min, dashboards update on their next poll";
    }

    private static HttpRequestMessage NewRequest(HttpMethod method, string url, GitHubConfig cfg)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.TryAddWithoutValidation("Authorization", $"Bearer {cfg.Token.Trim()}");
        req.Headers.TryAddWithoutValidation("Accept", "application/vnd.github+json");
        return req;
    }
}
