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

        //var targetGuilds = new[]
        //{
        //    new { GuildName = "Competence Optional", RealmApi = "[EN] Evermoon",             RealmDisplay = "Evermoon" },
        //    new { GuildName = "Skill Issue",         RealmApi = "[EN] Evermoon",             RealmDisplay = "Evermoon" },
        //    new { GuildName = "Despair",             RealmApi = "[EN] Evermoon",             RealmDisplay = "Evermoon" },
        //    new { GuildName = "Mythic",              RealmApi = "[HU] Warriors of Darkness", RealmDisplay = "WoD" },
        //    new { GuildName = "Vistustan",           RealmApi = "[EN] Evermoon",             RealmDisplay = "Evermoon" },
        //    new { GuildName = "Yin Yang",            RealmApi = "[EN] Evermoon",             RealmDisplay = "Evermoon" },
        //    new { GuildName = "Cara Máxima",     RealmApi = "[EN] Evermoon",             RealmDisplay = "Evermoon" },
        //    new { GuildName = "Infernum",            RealmApi = "[HU] Tauri WoW Server",     RealmDisplay = "Tauri" },
        //    new { GuildName = "Shadow Hunters",      RealmApi = "[EN] Evermoon",             RealmDisplay = "Evermoon" },
        //    new { GuildName = "Army of Divergent",      RealmApi = "[EN] Evermoon",             RealmDisplay = "Evermoon" },
        //    new { GuildName = "Punishers",           RealmApi = "[EN] Evermoon",             RealmDisplay = "Evermoon" },
        //    new { GuildName = "Искатели легенд",     RealmApi = "[EN] Evermoon",             RealmDisplay = "Evermoon" }
        //};

        //foreach (var g in targetGuilds)
        //{
        //    await LoadGuildMembersLevel100Async(
        //        guildName: g.GuildName,
        //        apiRealm: g.RealmApi,
        //        displayRealm: g.RealmDisplay,
        //        apiUrl: apiUrl,
        //        secret: secret,
        //        output: allCharacters);
        //}

        // Key = (Name, DisplayRealm), Value = Points for TODAY
        Dictionary<(string Name, string DisplayRealm), int> todayResults = new();

        var distinctCharacters = allCharacters.Distinct().ToList();

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
                    todayResults[(name, displayRealm)] = pts;

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

        // === UPDATE / CREATE CSV WITH NEW DATE COLUMN ===
        var csvPath = Path.Combine(projectRoot, "AchievementLadder.csv");
        var today = DateTime.Today.ToString("yyyy-MM-dd");

        UpdateCsvWithTodaySnapshot(csvPath, today, todayResults);

        // === PRINT SORTED LADDER (BY TODAY'S POINTS) ===
        var sorted = todayResults.OrderByDescending(x => x.Value).ToList();
        var longestName = sorted.Max(x => (x.Key.Name + "-" + x.Key.DisplayRealm).Length);

        Console.WriteLine("\n=== SORTED LADDER (ALL REALMS, TODAY) ===");

        var rank = 1;
        foreach (var entry in sorted)
        {
            var displayKey = $"{entry.Key.Name}-{entry.Key.DisplayRealm}";
            Console.WriteLine($"{rank}. {displayKey.PadRight(longestName)}  {entry.Value} pts");
            rank++;
        }
    }

    // Reads existing CSV (if any) and appends/updates today's column
    private static void UpdateCsvWithTodaySnapshot(
        string csvPath,
        string today,
        Dictionary<(string Name, string Realm), int> todayResults)
    {
        // We assume: no commas in Character or Realm (safe for simple Split(','))
        List<string> lines = new();
        if (File.Exists(csvPath))
        {
            lines = File.ReadAllLines(csvPath, Encoding.UTF8).ToList();
        }

        // If file is empty or doesn't exist -> create new from scratch
        if (lines.Count == 0)
        {
            var header = $"Character,Realm,{today}";
            var rows = todayResults
                .OrderByDescending(x => x.Value)
                .Select(kvp => $"{kvp.Key.Name},{kvp.Key.Realm},{kvp.Value}");

            var allLines = new List<string> { header };
            allLines.AddRange(rows);

            File.WriteAllLines(csvPath, allLines, Encoding.UTF8);
            Console.WriteLine($"\nCSV created: {csvPath}");
            return;
        }

        // Parse header
        var headerParts = lines[0].Split(',').ToList();
        if (headerParts.Count < 2 || headerParts[0] != "Character" || headerParts[1] != "Realm")
        {
            throw new InvalidOperationException("CSV header is not in expected format (Character,Realm,...).");
        }

        // Existing dates after Character,Realm
        var dates = headerParts.Skip(2).ToList();

        // Ensure today's date is in header
        if (!dates.Contains(today))
        {
            dates.Add(today);
        }

        // Rebuild header with updated dates
        var fullHeader = new List<string> { "Character", "Realm" };
        fullHeader.AddRange(dates);
        lines[0] = string.Join(",", fullHeader);

        int todayColIndex = 2 + dates.IndexOf(today); // column index of today's date in each row

        // Build map of existing rows: (Name, Realm) -> columns
        var rowMap = new Dictionary<(string Name, string Realm), List<string>>();

        for (int i = 1; i < lines.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
                continue;

            var parts = lines[i].Split(',').ToList();
            if (parts.Count < 2)
                continue;

            string name = parts[0];
            string realm = parts[1];

            // Pad columns so length matches header
            while (parts.Count < fullHeader.Count)
                parts.Add("");

            rowMap[(name, realm)] = parts;
        }

        // Merge today's results into rowMap
        foreach (var kvp in todayResults)
        {
            var key = kvp.Key;
            var pts = kvp.Value.ToString();

            if (!rowMap.TryGetValue(key, out var cols))
            {
                // New character: create row with blanks for all dates, then set today's value
                cols = new List<string> { key.Name, key.Realm };
                // Add empty cells for all dates
                for (int i = 0; i < dates.Count; i++)
                {
                    cols.Add("");
                }
                rowMap[key] = cols;
            }

            // Ensure row is long enough
            while (cols.Count < fullHeader.Count)
                cols.Add("");

            cols[todayColIndex] = pts;
        }

        // Characters that were in CSV but not in today's run keep blank for today's column (already padded)

        // Rebuild lines sorted by today's points (desc). If no value for today, treat as 0.
        var rowsList = rowMap.Values.ToList();

        // === KEEP ONLY TOP 100 ===
        rowsList = rowsList
            .OrderByDescending(cols =>
            {
                if (cols.Count > todayColIndex && int.TryParse(cols[todayColIndex], out var v))
                    return v;
                return 0;
            })
            .Take(100)  // <-- LIMIT TO TOP 100
            .ToList();

        var outLines = new List<string> { string.Join(",", fullHeader) };
        outLines.AddRange(rowsList.Select(cols => string.Join(",", cols)));

        File.WriteAllLines(csvPath, outLines, Encoding.UTF8);
        Console.WriteLine($"\nCSV updated (Top 100 saved): {csvPath}");
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
