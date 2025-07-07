using ParsingApp;

public interface ICharacterScanService
{
  IAsyncEnumerable<CharacterInfo> ScanCharactersAsync(
    IEnumerable<string> segments,
    int minChats,
    int pagesToScan,
    CancellationToken token,
    int startPage = 1); // Добавляем параметр
}