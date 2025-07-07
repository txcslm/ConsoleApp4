using System.Threading.Channels;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using ParsingApp;

public class SpicySeleniumScraper : ICharacterScraper
{
    private readonly IWebDriver _driver;
    private readonly IProgress<string>? _progress;
    private readonly JsonExportService? _exportService;
    private readonly WebDriverWait _wait;
    private const string NextPageBtnXPath = "//*[@id='root']/div[2]/div/div[2]/div[1]/div[2]/div[2]/div/button[9]";
    private const string CardsContainerXPath = "//*[@id=\"root\"]/div[2]/div/div[2]/div[1]/div[2]/div[1]/div[2]/div[3]/div";

    public SpicySeleniumScraper(IWebDriver driver, IProgress<string>? progress, JsonExportService? exportService = null)
    {
        _driver = driver;
        _progress = progress;
        _exportService = exportService;
        _wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(10));
    }

    public IAsyncEnumerable<CharacterInfo> GetPopularCharactersAsync(
        IEnumerable<string> segments,
        int minChats,
        int pagesToScan,
        CancellationToken token,
        int startPage = 1) // Добавляем параметр стартовой страницы
    {
        var channel = Channel.CreateUnbounded<CharacterInfo>();

        _ = Task.Run(() =>
        {
            try
            {
                // Если стартовая страница больше 1, сразу переходим на нужную
                if (startPage > 1)
                {
                    _progress?.Report($"NAVIGATE: открываем страницу {startPage}");
                    var directUrl = $"https://spicychat.ai/?public_characters_alias%2Fsort%2Fnum_messages_24h%3Adesc%5BsortBy%5D=public_characters_alias&public_characters_alias%2Fsort%2Fnum_messages_24h%3Adesc%5Bpage%5D={startPage}";
                    _driver.Navigate().GoToUrl(directUrl);
                    Thread.Sleep(5000); // Даем больше времени на загрузку глубокой страницы
                }
                else
                {
                    _progress?.Report("NAVIGATE: открываем https://spicychat.ai");
                    _driver.Navigate().GoToUrl("https://spicychat.ai");
                    Thread.Sleep(3000);

                    _progress?.Report("CLICK: нажимаем Trending");
                    _wait.Until(d => d.FindElement(By.XPath("//*[@aria-label='Trending']"))).Click();

                    _progress?.Report("CLICK: нажимаем Popular");
                    _wait.Until(d => d.FindElement(By.XPath("//button[@aria-label='Popular']"))).Click();
                    Thread.Sleep(2000);
                }

                // Корректируем цикл с учетом стартовой страницы
                int endPage = startPage + pagesToScan - 1;
                
                for (int page = startPage; page <= endPage; page++)
                {
                    token.ThrowIfCancellationRequested();
                    _progress?.Report($"PAGE {page} (обработано {page - startPage + 1} из {pagesToScan})");

                    _progress?.Report($"FIND: контейнер карточек");
                    
                    // Увеличиваем таймаут для глубоких страниц
                    var longWait = new WebDriverWait(_driver, TimeSpan.FromSeconds(30));
                    var container = longWait.Until(d => d.FindElement(By.XPath(CardsContainerXPath)));

                    var cards = container.FindElements(By.XPath("./div")).ToList();
                    _progress?.Report($"FOUND: карточек на странице — {cards.Count}");

                    // Если карточек мало или нет - возможно достигли конца
                    if (cards.Count == 0)
                    {
                        _progress?.Report($"[WARNING] На странице {page} нет карточек. Возможно, достигнут конец списка.");
                        break;
                    }

                    // Быстрая проверка страницы
                    if (_exportService != null)
                    {
                        int newOnPage = 0;
                        var urlsToCheck = new List<(IWebElement card, string url)>();
                        
                        // Сначала собираем все URL для быстрой проверки
                        foreach (var card in cards)
                        {
                            try
                            {
                                var linkElem = card.FindElement(By.XPath(".//a[contains(@href,'/chatbot/')]"));
                                var url = linkElem.GetAttribute("href") ?? string.Empty;
                                if (!string.IsNullOrEmpty(url))
                                {
                                    urlsToCheck.Add((card, url));
                                    if (!_exportService.AlreadyExists(url))
                                        newOnPage++;
                                }
                            }
                            catch { /* игнорируем ошибки при быстрой проверке */ }
                        }

                        if (newOnPage == 0)
                        {
                            _progress?.Report($"[SKIP PAGE] Все {cards.Count} персонажей уже в базе!");
                            if (page < endPage)
                            {
                                if (!TryClickNextPage())
                                {
                                    // Если не можем кликнуть Next, пробуем прямой переход
                                    _progress?.Report($"NAVIGATE: прямой переход на страницу {page + 1}");
                                    var nextUrl = $"https://spicychat.ai/?public_characters_alias%2Fsort%2Fnum_messages_24h%3Adesc%5BsortBy%5D=public_characters_alias&public_characters_alias%2Fsort%2Fnum_messages_24h%3Adesc%5Bpage%5D={page + 1}";
                                    _driver.Navigate().GoToUrl(nextUrl);
                                    Thread.Sleep(3000);
                                }
                                else
                                {
                                    Thread.Sleep(2000);
                                }
                            }
                            continue;
                        }
                        _progress?.Report($"[INFO] Новых персонажей на странице: {newOnPage}/{cards.Count}");
                    }

                    // Обработка карточек
                    int processedOnPage = 0;
                    int publicFound = 0;
                    
                    foreach (var card in cards)
                    {
                        token.ThrowIfCancellationRequested();
                        processedOnPage++;
                        
                        try
                        {
                            var nameElem = card.FindElement(By.CssSelector("p.text-label-lg"));
                            var name = nameElem.Text.Trim();

                            var linkElem = card.FindElement(By.XPath(".//a[contains(@href,'/chatbot/')]"));
                            var url = linkElem.GetAttribute("href") ?? string.Empty;

                            // Быстрая проверка существования перед детальным парсингом
                            if (_exportService != null && _exportService.AlreadyExists(url))
                            {
                                _progress?.Report($"[{processedOnPage}/{cards.Count}] [SKIP] {name} — уже в базе");
                                continue;
                            }

                            _progress?.Report($"[{processedOnPage}/{cards.Count}] PARSE: {name}");

                            var infoAnchor = card.FindElement(By.XPath(".//a[@aria-label='character-info']"));
                            var iconDiv = infoAnchor.FindElement(By.XPath(".//div/div"));

                            var bgColor = (string)((IJavaScriptExecutor)_driver)
                                .ExecuteScript("return getComputedStyle(arguments[0]).backgroundColor;", iconDiv)!;
                            var isPublic = bgColor.Contains("151, 80, 221");

                            if (isPublic)
                            {
                                publicFound++;
                                _progress?.Report($"[OK]   {name} — {url}");
                                channel.Writer.TryWrite(new CharacterInfo(name, url, false, 0));
                            }
                            else
                            {
                                _progress?.Report($"[PRIV] {name} — приватный");
                            }
                        }
                        catch (NoSuchElementException)
                        {
                            _progress?.Report($"[{processedOnPage}/{cards.Count}] [SKIP] элемент не найден");
                        }
                        catch (StaleElementReferenceException)
                        {
                            _progress?.Report($"[{processedOnPage}/{cards.Count}] [SKIP] элемент устарел");
                        }
                        catch (WebDriverException wex)
                        {
                            _progress?.Report($"[{processedOnPage}/{cards.Count}] [ERROR] WebDriver: {wex.Message}");
                        }
                    }

                    _progress?.Report($"[PAGE SUMMARY] Страница {page}: Обработано: {processedOnPage}, Публичных: {publicFound}");

                    // Переход на следующую страницу
                    if (page < endPage)
                    {
                        if (!TryClickNextPage())
                        {
                            // Используем прямой переход если кнопка не работает
                            _progress?.Report($"NAVIGATE: прямой переход на страницу {page + 1}");
                            var nextUrl = $"https://spicychat.ai/?public_characters_alias%2Fsort%2Fnum_messages_24h%3Adesc%5BsortBy%5D=public_characters_alias&public_characters_alias%2Fsort%2Fnum_messages_24h%3Adesc%5Bpage%5D={page + 1}";
                            _driver.Navigate().GoToUrl(nextUrl);
                            Thread.Sleep(3000);
                        }
                        else
                        {
                            Thread.Sleep(2000);
                        }
                    }
                }

                channel.Writer.Complete();
            }
            catch (OperationCanceledException)
            {
                _progress?.Report("CANCELLED: Операция отменена пользователем");
                channel.Writer.Complete();
            }
            catch (Exception ex)
            {
                _progress?.Report($"FATAL: {ex.Message}");
                channel.Writer.Complete(ex);
            }
        }, token);

        return channel.Reader.ReadAllAsync(token);
    }

    private bool TryClickNextPage()
    {
        _progress?.Report("CLICK: next page");
        try
        {
            var nextBtn = FindNextPageButton();
            if (nextBtn == null)
            {
                _progress?.Report("NEXT not found, выходим");
                return false;
            }

            if (nextBtn.Enabled)
            {
                ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].scrollIntoView(true);", nextBtn);
                Thread.Sleep(500);
                nextBtn.Click();
                return true;
            }
            else
            {
                _progress?.Report("NEXT disabled, выходим");
                return false;
            }
        }
        catch (Exception ex)
        {
            _progress?.Report($"NEXT error: {ex.Message}");
            return false;
        }
    }

    private IWebElement? FindNextPageButton()
    {
        try
        {
            return _driver.FindElement(By.XPath(NextPageBtnXPath));
        }
        catch (NoSuchElementException)
        {
            // Альтернативные селекторы
            var selectors = new[]
            {
                "button[aria-label='next-page']",
                "button:has(svg.lucide-chevron-right)",
                "button:contains('>')"
            };

            foreach (var selector in selectors)
            {
                try
                {
                    var elements = _driver.FindElements(By.CssSelector(selector));
                    if (elements.Count > 0)
                        return elements.Last();
                }
                catch { /* продолжаем поиск */ }
            }

            return null;
        }
    }
}
