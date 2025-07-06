using Newtonsoft.Json;

namespace ParsingApp;

public class JsonExportService : ICharacterDownloadService
{
    private readonly object _lock = new();
    private const string FileName = "characters.json";
    private readonly HashSet<string> _existingIds;
    private readonly HashSet<string> _existingUrls;
    private List<CharacterEntry> _entries;

    public JsonExportService()
    {
        // Загружаем существующие записи в память при старте
        if (File.Exists(FileName))
        {
            var json = File.ReadAllText(FileName);
            _entries = JsonConvert.DeserializeObject<List<CharacterEntry>>(json) ?? new();
            _existingIds = new HashSet<string>(_entries.Select(e => e.Id), StringComparer.OrdinalIgnoreCase);
            _existingUrls = new HashSet<string>(_entries.Select(e => e.Url), StringComparer.OrdinalIgnoreCase);
        }
        else
        {
            _entries = new List<CharacterEntry>();
            _existingIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _existingUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }
    
    public int EntriesCount => _entries.Count;

    public bool AlreadyExists(string url)
    {
        var id = ExtractId(url);
        return _existingIds.Contains(id) || _existingUrls.Contains(url);
    }

    public string? Download(CharacterInfo character)
    {
        var entry = new CharacterEntry
        {
            Id = ExtractId(character.Url),
            Name = character.Name,
            Url = character.Url
        };

        lock (_lock)
        {
            if (!_existingIds.Contains(entry.Id) && !_existingUrls.Contains(entry.Url))
            {
                _entries.Add(entry);
                _existingIds.Add(entry.Id);
                _existingUrls.Add(entry.Url);
                
                var json = JsonConvert.SerializeObject(_entries, Formatting.Indented);
                File.WriteAllText(FileName, json);
                return "new"; // Индикатор что добавлена новая запись
            }
            return null; // Уже существует
        }
    }

    private static string ExtractId(string url)
    {
        int idx = url.LastIndexOf('/') + 1;
        if (idx > 0 && idx < url.Length)
            return url[idx..];
        return url;
    }
}
