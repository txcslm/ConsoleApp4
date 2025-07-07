using ParsingApp;

public class CharacterScanService : ICharacterScanService
{
  private readonly ICharacterScraper _scraper;

  public CharacterScanService(ICharacterScraper scraper)
  {
    _scraper = scraper;
  }

  public IAsyncEnumerable<CharacterInfo> ScanCharactersAsync(
    IEnumerable<string> segments,
    int minChats,
    int pagesToScan,
    CancellationToken token,
    int startPage = 1) // Добавляем параметр
  {
    return _scraper.GetPopularCharactersAsync(segments, minChats, pagesToScan, token, startPage);
  }
}