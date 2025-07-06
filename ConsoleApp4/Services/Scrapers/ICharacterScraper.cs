namespace ParsingApp;

public interface ICharacterScraper
{
  IAsyncEnumerable<CharacterInfo> GetPopularCharactersAsync(
    IEnumerable<string> segments,
    int minChats,
    int pagesToScan,
    CancellationToken token);
}