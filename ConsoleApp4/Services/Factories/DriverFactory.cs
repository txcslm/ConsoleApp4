using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

namespace ParsingApp;

public static class DriverFactory
{
  public static IWebDriver Create(string path, int port = 0)
  {
    var options = new ChromeOptions();
    var stringPort = port.ToString();

    options.AddArgument($"--user-data-dir={path}");
    options.AddArgument("--dns-prefetch-disable");
    options.AddArgument("--no-sandbox");
    options.AddArgument("--disable-dev-shm-usage");
    options.AddArgument("--disable-gpu");
    options.AddArgument("--start-1920x1080");
    options.AddArgument($"--remote-debugging-port={stringPort}");
    options.AddArgument("--remote-allow-origins=*");
    options.AddArgument("--disable-backgrounding-occluded-windows");
    options.AddArgument("--disable-renderer-backgrounding");
    options.AddArgument("--disable-background-timer-throttling");
    options.AddArgument("--disable-application-cache");
    options.AddArgument("--disk-cache-size=1");
    options.AddArgument("--media-cache-size=1");
    return new ChromeDriver(options);
  }
}