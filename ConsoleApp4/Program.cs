using OpenQA.Selenium;
using ParsingApp;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== SpicyChat Parser Console ===");
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Запуск приложения...\n");

        // Настройки парсинга
        int pagesToScan = 5;
        int minChats = 0;
        var segments = new List<string>();

        // Запрашиваем параметры у пользователя
        Console.Write("Количество страниц для сканирования (по умолчанию 5): ");
        var pagesInput = Console.ReadLine();
        if (int.TryParse(pagesInput, out int pages) && pages > 0)
            pagesToScan = pages;
        
        Console.Write("Стартовая страница (по умолчанию 1): ");
        var startPageInput = Console.ReadLine();
        int startPage = 1;
        if (int.TryParse(startPageInput, out int start) && start > 0)
            startPage = start;

        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Старт со страницы: {startPage}");
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Будет отсканировано страниц: {pagesToScan} (с {startPage} по {startPage + pagesToScan - 1})");

        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Будет отсканировано страниц: {pagesToScan}");

        // Создаем папку для профиля Chrome
        var profilePath = Path.Combine(Directory.GetCurrentDirectory(), "ChromeProfile");
        Directory.CreateDirectory(profilePath);

        IWebDriver? driver = null;
        var startTime = DateTime.Now;
        
        try
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Инициализация ChromeDriver...");
            driver = DriverFactory.Create(profilePath, 9222);
            
            // Progress reporter для логов
            var progress = new Progress<string>(message => 
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");
            });

            // Создаем сервисы
            var exporter = new JsonExportService();
            var existingCount = exporter.EntriesCount;
            
            var scraper = new SpicySeleniumScraper(driver, progress, exporter);
            var scanner = new CharacterScanService(scraper);

            // CancellationToken для graceful shutdown
            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (s, e) => 
            {
                e.Cancel = true;
                cts.Cancel();
                Console.WriteLine($"\n[{DateTime.Now:HH:mm:ss}] Получен сигнал отмены...");
            };

            // Счетчики
            int foundCount = 0;
            int savedCount = 0;
            int skippedCount = 0;
            int errorCount = 0;

            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Начинаем сканирование...");
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Уже в базе: {existingCount} персонажей\n");

            // Основной цикл сканирования и сохранения
            var buffer = new List<CharacterInfo>();
            
            await foreach (var character in scanner.ScanCharactersAsync(segments, minChats, pagesToScan, cts.Token, startPage))
            {
                foundCount++;
                
                // Буферизация для batch-логирования
                buffer.Add(character);
                
                if (buffer.Count >= 5 || foundCount % 10 == 0)
                {
                    Console.WriteLine($"\n[{DateTime.Now:HH:mm:ss}] === Обработка batch #{foundCount / 5 + 1} ===");
                    
                    foreach (var ch in buffer)
                    {
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ✓ Персонаж: {ch.Name}");
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}]   URL: {ch.Url}");
                        
                        try
                        {
                            var result = exporter.Download(ch);
                            if (result != null)
                            {
                                savedCount++;
                                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}]   ✓ Добавлен в characters.json");
                            }
                            else
                            {
                                skippedCount++;
                                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}]   ⚠ Уже существует в базе");
                            }
                        }
                        catch (Exception ex)
                        {
                            errorCount++;
                            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}]   ✗ Ошибка: {ex.Message}");
                        }
                    }
                    
                    buffer.Clear();
                }
            }

            // Обработка оставшихся в буфере
            if (buffer.Count > 0)
            {
                foreach (var ch in buffer)
                {
                    try
                    {
                        var result = exporter.Download(ch);
                        if (result != null) savedCount++;
                        else skippedCount++;
                    }
                    catch { errorCount++; }
                }
            }

            var elapsed = DateTime.Now - startTime;

            // Итоговая статистика
            Console.WriteLine($"\n[{DateTime.Now:HH:mm:ss}] {new string('=', 50)}");
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ЗАВЕРШЕНО!");
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Время выполнения: {elapsed:hh\\:mm\\:ss}");
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Найдено публичных персонажей: {foundCount}");
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Сохранено новых: {savedCount}");
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Пропущено (дубликаты): {skippedCount}");
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Ошибок: {errorCount}");
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Всего в базе: {exporter.EntriesCount} (было {existingCount})");
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Файл: {Path.GetFullPath("characters.json")}");           
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {new string('=', 50)}");
            
            if (foundCount > 0)
            {
                var charactersPerMinute = foundCount / Math.Max(elapsed.TotalMinutes, 1);
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Скорость: {charactersPerMinute:F1} персонажей/мин");
            }
        }
        catch (WebDriverException wex)
        {
            Console.WriteLine($"\n[{DateTime.Now:HH:mm:ss}] ОШИБКА WebDriver: {wex.Message}");
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Возможно, ChromeDriver не установлен или устарел");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n[{DateTime.Now:HH:mm:ss}] ОШИБКА: {ex.Message}");
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Тип: {ex.GetType().Name}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Внутренняя ошибка: {ex.InnerException.Message}");
            }
        }
        finally
        {
            if (driver != null)
            {
                try
                {
                    Console.WriteLine($"\n[{DateTime.Now:HH:mm:ss}] Закрытие браузера...");
                    driver.Quit();
                    driver.Dispose();
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Браузер закрыт");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Ошибка при закрытии браузера: {ex.Message}");
                }
            }
        }

        Console.WriteLine($"\n[{DateTime.Now:HH:mm:ss}] Нажмите любую клавишу для выхода...");
        Console.ReadKey();
    }
}
