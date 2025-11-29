using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

public class Program
{
    private static readonly HttpClient client = new HttpClient();

    public static async Task Main()
    {
        var baseDir = AppContext.BaseDirectory;
        var projectRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", ".."));

        var config = new ConfigurationBuilder()
            .SetBasePath(projectRoot)
            .AddJsonFile("appsettings.Secrets.json", optional: false, reloadOnChange: true)
            .Build();

        string baseUrl = config["TauriApi:BaseUrl"];
        string apiKey = config["TauriApi:ApiKey"];
        string secret = config["TauriApi:Secret"];

        string apiUrl = $"{baseUrl}?apikey={apiKey}";

        var allCharacters = new List<(string Name, string ApiRealm, string DisplayRealm)>();

        LoadCharacters("evermoon.txt", "[EN] Evermoon", "Evermoon", allCharacters);
        LoadCharacters("tauri.txt", "[HU] Tauri WoW Server", "Tauri", allCharacters);
        LoadCharacters("wod.txt", "[HU] Warriors of Darkness", "WoD", allCharacters);

        var targetGuilds = new[]
        {
            new { GuildName = "Competence Optional", RealmApi = "[EN] Evermoon",           RealmDisplay = "Evermoon" },
            new { GuildName = "Skill Issue",         RealmApi = "[EN] Evermoon",           RealmDisplay = "Evermoon" },
            new { GuildName = "Despair",             RealmApi = "[EN] Evermoon",           RealmDisplay = "Evermoon" },
            new { GuildName = "Mythic",              RealmApi = "[HU] Warriors of Darkness", RealmDisplay = "WoD" },
            new { GuildName = "LOS CARA MÁXIMA",     RealmApi = "[EN] Evermoon",           RealmDisplay = "Evermoon" },
            new { GuildName = "Vistustan",           RealmApi = "[EN] Evermoon",           RealmDisplay = "Evermoon" },
            new { GuildName = "Yin Yang",            RealmApi = "[EN] Evermoon",           RealmDisplay = "Evermoon" },
            new { GuildName = "Infernum",            RealmApi = "[HU] Tauri WoW Server",   RealmDisplay = "Tauri" },
            new { GuildName = "Shadow Hunters",      RealmApi = "[EN] Evermoon",           RealmDisplay = "Evermoon" },
            new { GuildName = "Punishers",           RealmApi = "[EN] Evermoon",           RealmDisplay = "Evermoon" },
            new { GuildName = "Искатели легенд",     RealmApi = "[EN] Evermoon",           RealmDisplay = "Evermoon" }
        };

        foreach (var g in targetGuilds)
        {
            await LoadGuildMembersLevel100Async(
                guildName: g.GuildName,
                apiRealm: g.RealmApi,
                displayRealm: g.RealmDisplay,
                apiUrl: apiUrl,
                secret: secret,
                output: allCharacters);
        }

        Dictionary<string, (int Points, string DisplayRealm)> results = new();

        foreach (var (name, apiRealm, displayRealm) in allCharacters)
        {
            var body = new
            {
                secret = secret,
                url = "character-achievements",
                @params = new
                {
                    r = apiRealm,
                    n = name
                }
            };

            var jsonBody = JsonSerializer.Serialize(body);
            var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            try
            {
                var response = await client.PostAsync(apiUrl, content);
                string responseString = await response.Content.ReadAsStringAsync();

                var apiResponse = JsonSerializer.Deserialize<ApiResponse>(responseString);

                if (apiResponse?.response != null)
                {
                    int pts = apiResponse.response.pts;
                    results[name + "-" + displayRealm] = (pts, displayRealm);

                    Console.WriteLine($"{name}-{displayRealm} -> {pts} pts");
                }
                else
                {
                    Console.WriteLine($"{name}-{displayRealm}: No data or error.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching {name} from {displayRealm}: {ex.Message}");
            }
        }

        var sorted = results.OrderByDescending(x => x.Value.Points).ToList();
        var longestName = sorted.Max(x => x.Key.Length);

        Console.WriteLine("\n=== SORTED LADDER (ALL REALMS) ===");

        var rank = 1;
        foreach (var entry in sorted)
        {
            Console.WriteLine($"{rank}. {entry.Key.PadRight(longestName)}  {entry.Value.Points} pts");
            rank++;
        }
    }

    static void LoadCharacters(
        string fileName,
        string apiRealm,
        string displayRealm,
        List<(string, string, string)> output)
    {
        var baseDir = AppContext.BaseDirectory;
        var filePath = Path.Combine(baseDir, "..", "..", "..", "Data", fileName);

        string content = File.ReadAllText(filePath);

        using var doc = JsonDocument.Parse(content);
        var array = doc.RootElement.EnumerateObject().First().Value;

        foreach (var item in array.EnumerateArray())
        {
            if (item.TryGetProperty("name", out JsonElement nameProp))
            {
                string name = nameProp.GetString();
                output.Add((name, apiRealm, displayRealm));
            }
        }
    }

    static async Task LoadGuildMembersLevel100Async(
        string guildName,
        string apiRealm,
        string displayRealm,
        string apiUrl,
        string secret,
        List<(string, string, string)> output)
    {
        var body = new
        {
            secret = secret,
            url = "guild-info",
            @params = new
            {
                r = apiRealm,
                gn = guildName
            }
        };

        var jsonBody = JsonSerializer.Serialize(body);
        var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

        try
        {
            var response = await client.PostAsync(apiUrl, content);
            string responseString = await response.Content.ReadAsStringAsync();

            var guildInfo = JsonSerializer.Deserialize<GuildInfoResponse>(responseString);

            if (guildInfo?.response?.guildList == null)
            {
                Console.WriteLine($"Guild {guildName} ({displayRealm}): no guildList in response.");
                return;
            }

            int added = 0;

            foreach (var member in guildInfo.response.guildList.Values)
            {
                if (member.level == 100)
                {
                    output.Add((member.name, apiRealm, displayRealm));
                    added++;
                }
            }

            Console.WriteLine($"Guild {guildName} ({displayRealm}): added {added} level 100 members.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading guild {guildName} ({displayRealm}): {ex.Message}");
        }
    }
}

public class ApiResponse
{
    public bool success { get; set; }
    public int errorcode { get; set; }
    public string errorstring { get; set; }
    public ApiResponseInner response { get; set; }
}

public class ApiResponseInner
{
    public int pts { get; set; }
}

public class GuildInfoResponse
{
    public bool success { get; set; }
    public int errorcode { get; set; }
    public string errorstring { get; set; }
    public GuildInfoInner response { get; set; }
}

public class GuildInfoInner
{
    public Dictionary<string, GuildMember> guildList { get; set; }
}

public class GuildMember
{
    public string name { get; set; }
    public int level { get; set; }
    public string realm { get; set; }
}
