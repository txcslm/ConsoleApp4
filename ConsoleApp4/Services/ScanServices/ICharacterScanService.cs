namespace ParsingApp;

public interface ICharacterScanService
{
  IAsyncEnumerable<CharacterInfo> ScanCharactersAsync(
    IEnumerable<string> segments,
    int minChats,
    int pagesToScan,
    CancellationToken token);
}