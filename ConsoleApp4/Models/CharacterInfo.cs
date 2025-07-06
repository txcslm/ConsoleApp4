namespace ParsingApp;

public class CharacterInfo(string name, string url, bool hasFireTag, int chatCount)
{
  public string Name { get; set; } = name;

  public string Url { get; set; } = url;

  public bool HasFireTag { get; set; } = hasFireTag;

  public int ChatCount { get; set; } = chatCount;

  public HashSet<string> Tags { get; set; } = new();
  public bool IsNsfw { get; set; }
}