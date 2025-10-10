# Market News

Market news provides real-time and historical news feeds from various sources. News can significantly impact trading decisions and market movements, making it an important data type for traders.

## Overview

News data in StockSharp is represented by the `NewsMessage` class, which contains:
- News headline and full story
- Source and publication time
- Associated security (if applicable)
- Priority level
- URL link to full article
- Language and expiration date

## NewsMessage

The main class for news data:

### Properties

```csharp
public class NewsMessage : BaseSubscriptionIdMessage<NewsMessage>
{
    // Unique news identifier
    public string Id { get; set; }

    // Electronic board code
    public string BoardCode { get; set; }

    // Associated security (optional)
    public SecurityId? SecurityId { get; set; }

    // News source (e.g., "Reuters", "Bloomberg", "Dow Jones")
    public string Source { get; set; }

    // News headline
    public string Headline { get; set; }

    // Full news story/text
    public string Story { get; set; }

    // News publication timestamp
    public DateTimeOffset ServerTime { get; set; }

    // URL link to full article
    public string Url { get; set; }

    // News priority (Low, Regular, High)
    public NewsPriorities? Priority { get; set; }

    // Language code (e.g., "en", "ru")
    public string Language { get; set; }

    // Expiration date (when news becomes obsolete)
    public DateTimeOffset? ExpiryDate { get; set; }

    // Product ID (internal)
    public long ProductId { get; set; }

    // Attachments (file IDs)
    public long[] Attachments { get; set; }

    // Sequence number
    public long SeqNum { get; set; }

    // Transaction ID
    public long TransactionId { get; set; }

    // Data type
    public override DataType DataType => DataType.News;
}
```

## News Priorities

```csharp
public enum NewsPriorities
{
    Low,        // Low priority news
    Regular,    // Regular/normal priority
    High        // High priority/breaking news
}
```

## Subscribing to News

### Basic Subscription (All News)

```csharp
using StockSharp.Algo;
using StockSharp.Messages;

var connector = new Connector();

// Subscribe to all news
var subscription = connector.Subscribe(new Subscription(DataType.News));
```

### Subscribe to News for Specific Security

```csharp
// Subscribe to news for a specific security
var security = connector.GetSecurity("AAPL@NASDAQ");
var subscription = connector.Subscribe(new Subscription(DataType.News, security));
```

### Historical News

```csharp
// Subscribe to historical news
var subscription = connector.Subscribe(new Subscription(DataType.News)
{
    From = DateTimeOffset.Now.AddDays(-7),  // Last 7 days
    To = DateTimeOffset.Now
});
```

## Handling News Data

### NewsReceived Event

The primary event for receiving news:

```csharp
connector.NewsReceived += (subscription, news) =>
{
    Console.WriteLine($"News: {news.Headline}");
    Console.WriteLine($"  Source: {news.Source}");
    Console.WriteLine($"  Time: {news.ServerTime:yyyy-MM-dd HH:mm:ss}");
    Console.WriteLine($"  Priority: {news.Priority}");
    Console.WriteLine($"  Security: {news.SecurityId}");

    if (!string.IsNullOrEmpty(news.Story))
    {
        Console.WriteLine($"  Story: {news.Story.Substring(0, Math.Min(200, news.Story.Length))}...");
    }

    if (!string.IsNullOrEmpty(news.Url))
    {
        Console.WriteLine($"  URL: {news.Url}");
    }
};
```

### Working with News Entity

StockSharp also provides a `News` entity class that wraps NewsMessage:

```csharp
connector.NewsReceived += (subscription, newsMsg) =>
{
    // Convert to News entity (automatically done by connector)
    var news = new News
    {
        Id = newsMsg.Id,
        Source = newsMsg.Source,
        Headline = newsMsg.Headline,
        Story = newsMsg.Story,
        ServerTime = newsMsg.ServerTime,
        Url = newsMsg.Url,
        Priority = newsMsg.Priority,
        Board = newsMsg.BoardCode,
        Security = newsMsg.SecurityId.HasValue
            ? connector.GetSecurity(newsMsg.SecurityId.Value)
            : null
    };

    // Process news entity
    ProcessNews(news);
};
```

## Practical Examples

### Example 1: High-Priority News Alert

```csharp
connector.NewsReceived += (subscription, news) =>
{
    // Alert on high-priority news
    if (news.Priority == NewsPriorities.High)
    {
        Console.WriteLine($"*** BREAKING NEWS ***");
        Console.WriteLine($"Time: {news.ServerTime:HH:mm:ss}");
        Console.WriteLine($"Source: {news.Source}");
        Console.WriteLine($"Headline: {news.Headline}");

        if (news.SecurityId.HasValue)
        {
            Console.WriteLine($"Security: {news.SecurityId}");
            Console.WriteLine($"=> Consider immediate review of positions");
        }

        // Play alert sound, send notification, etc.
        NotifyTrader(news);
    }
};

void NotifyTrader(NewsMessage news)
{
    // Send email, SMS, or desktop notification
    // Play alert sound
    // Flash trading terminal
}
```

### Example 2: Security-Specific News Monitoring

```csharp
var watchlist = new[] { "AAPL@NASDAQ", "MSFT@NASDAQ", "GOOGL@NASDAQ" };
var watchlistIds = watchlist.Select(id => SecurityId.Parse(id)).ToHashSet();

connector.NewsReceived += (subscription, news) =>
{
    // Filter news for watchlist securities
    if (news.SecurityId.HasValue && watchlistIds.Contains(news.SecurityId.Value))
    {
        Console.WriteLine($"NEWS for {news.SecurityId}:");
        Console.WriteLine($"  {news.Headline}");
        Console.WriteLine($"  Priority: {news.Priority}");
        Console.WriteLine($"  Source: {news.Source}");

        // Check if news requires action
        if (RequiresAction(news))
        {
            Console.WriteLine($"  => ACTION REQUIRED!");
            ReviewPosition(news.SecurityId.Value);
        }
    }
};

bool RequiresAction(NewsMessage news)
{
    var keywords = new[] { "earnings", "FDA", "merger", "bankruptcy", "lawsuit" };
    var headline = news.Headline?.ToLower() ?? "";

    return keywords.Any(k => headline.Contains(k));
}
```

### Example 3: News Sentiment Analysis

```csharp
var sentimentKeywords = new Dictionary<string, int>
{
    // Positive keywords
    ["profit"] = 1,
    ["growth"] = 1,
    ["beat"] = 1,
    ["upgrade"] = 1,
    ["buy"] = 1,
    ["outperform"] = 1,

    // Negative keywords
    ["loss"] = -1,
    ["decline"] = -1,
    ["miss"] = -1,
    ["downgrade"] = -1,
    ["sell"] = -1,
    ["underperform"] = -1
};

connector.NewsReceived += (subscription, news) =>
{
    var sentiment = CalculateSentiment(news.Headline + " " + news.Story);

    Console.WriteLine($"News: {news.Headline}");
    Console.WriteLine($"  Sentiment: {sentiment}");

    if (sentiment > 0)
        Console.WriteLine($"  => POSITIVE sentiment");
    else if (sentiment < 0)
        Console.WriteLine($"  => NEGATIVE sentiment");
    else
        Console.WriteLine($"  => NEUTRAL sentiment");
};

int CalculateSentiment(string text)
{
    if (string.IsNullOrEmpty(text))
        return 0;

    var lowerText = text.ToLower();
    return sentimentKeywords
        .Where(kv => lowerText.Contains(kv.Key))
        .Sum(kv => kv.Value);
}
```

### Example 4: News Aggregation by Source

```csharp
var newsCountBySource = new Dictionary<string, int>();

connector.NewsReceived += (subscription, news) =>
{
    var source = news.Source ?? "Unknown";

    if (!newsCountBySource.ContainsKey(source))
        newsCountBySource[source] = 0;

    newsCountBySource[source]++;

    // Display statistics every 100 news items
    var totalNews = newsCountBySource.Values.Sum();
    if (totalNews % 100 == 0)
    {
        Console.WriteLine($"\nNews Distribution ({totalNews} total):");
        foreach (var (src, count) in newsCountBySource.OrderByDescending(kv => kv.Value))
        {
            var percent = (count * 100.0) / totalNews;
            Console.WriteLine($"  {src}: {count} ({percent:F1}%)");
        }
    }
};
```

### Example 5: News Timeline

```csharp
var recentNews = new Queue<NewsMessage>();
var maxNews = 50;

connector.NewsReceived += (subscription, news) =>
{
    recentNews.Enqueue(news);

    while (recentNews.Count > maxNews)
        recentNews.Dequeue();

    // Display recent news
    Console.Clear();
    Console.WriteLine($"Recent News ({recentNews.Count}):");
    Console.WriteLine(new string('=', 80));

    foreach (var item in recentNews.Reverse())
    {
        var priority = item.Priority == NewsPriorities.High ? "[HIGH]" :
                      item.Priority == NewsPriorities.Low ? "[LOW]" : "";

        var security = item.SecurityId.HasValue ? $" ({item.SecurityId})" : "";

        Console.WriteLine($"{item.ServerTime:HH:mm:ss} {priority} {item.Source}{security}");
        Console.WriteLine($"  {item.Headline}");
        Console.WriteLine();
    }
};
```

### Example 6: Economic Calendar Integration

```csharp
var economicEvents = new Dictionary<string, string>
{
    ["Non-Farm Payrolls"] = "High Impact",
    ["FOMC"] = "High Impact",
    ["GDP"] = "High Impact",
    ["CPI"] = "Medium Impact",
    ["Unemployment"] = "Medium Impact",
    ["Retail Sales"] = "Medium Impact"
};

connector.NewsReceived += (subscription, news) =>
{
    var headline = news.Headline ?? "";

    // Check if news relates to economic event
    foreach (var (eventName, impact) in economicEvents)
    {
        if (headline.Contains(eventName, StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"*** ECONOMIC EVENT ***");
            Console.WriteLine($"Event: {eventName}");
            Console.WriteLine($"Impact: {impact}");
            Console.WriteLine($"Time: {news.ServerTime:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine($"Headline: {headline}");

            if (impact == "High Impact")
            {
                Console.WriteLine($"=> Consider reducing position sizes");
                Console.WriteLine($"=> Tighten stop losses");
                Console.WriteLine($"=> Increase monitoring frequency");
            }
        }
    }
};
```

### Example 7: News-Based Trading Signal

```csharp
public class NewsStrategy : Strategy
{
    private readonly HashSet<string> _positiveKeywords = new()
    {
        "beats expectations", "strong earnings", "raises guidance",
        "positive outlook", "record revenue"
    };

    private readonly HashSet<string> _negativeKeywords = new()
    {
        "misses estimates", "weak earnings", "lowers guidance",
        "negative outlook", "revenue decline"
    };

    protected override void OnStarted(DateTimeOffset time)
    {
        base.OnStarted(time);

        // Subscribe to news for our security
        Connector.Subscribe(new Subscription(DataType.News, Security));

        Connector.NewsReceived += OnNewsReceived;
    }

    private void OnNewsReceived(Subscription subscription, NewsMessage news)
    {
        // Only process news for our security
        if (!news.SecurityId.HasValue ||
            news.SecurityId.Value != Security.ToSecurityId())
            return;

        // Only process high-priority news
        if (news.Priority != NewsPriorities.High)
            return;

        var text = (news.Headline + " " + news.Story).ToLower();

        // Check for positive signals
        var positiveSignal = _positiveKeywords.Any(k => text.Contains(k));
        var negativeSignal = _negativeKeywords.Any(k => text.Contains(k));

        if (positiveSignal && !negativeSignal)
        {
            this.AddInfoLog($"POSITIVE NEWS: {news.Headline}");

            if (Position <= 0)
            {
                this.AddInfoLog("=> Buying on positive news");
                BuyMarket(Volume);
            }
        }
        else if (negativeSignal && !positiveSignal)
        {
            this.AddInfoLog($"NEGATIVE NEWS: {news.Headline}");

            if (Position >= 0)
            {
                this.AddInfoLog("=> Selling on negative news");
                SellMarket(Volume);
            }
        }
    }

    protected override void OnStopped(DateTimeOffset time)
    {
        Connector.NewsReceived -= OnNewsReceived;
        base.OnStopped(time);
    }
}
```

### Example 8: Multi-Language News

```csharp
var supportedLanguages = new[] { "en", "ru", "zh", "ja" };

connector.NewsReceived += (subscription, news) =>
{
    var language = news.Language ?? "en";

    if (supportedLanguages.Contains(language))
    {
        Console.WriteLine($"[{language.ToUpper()}] {news.Headline}");

        // Language-specific processing
        switch (language)
        {
            case "en":
                ProcessEnglishNews(news);
                break;

            case "ru":
                ProcessRussianNews(news);
                break;

            case "zh":
                ProcessChineseNews(news);
                break;
        }
    }
};
```

### Example 9: News Link Handler

```csharp
connector.NewsReceived += (subscription, news) =>
{
    Console.WriteLine($"News: {news.Headline}");
    Console.WriteLine($"  Time: {news.ServerTime:yyyy-MM-dd HH:mm:ss}");
    Console.WriteLine($"  Source: {news.Source}");

    // Check if full article is available
    if (!string.IsNullOrEmpty(news.Url))
    {
        Console.WriteLine($"  Full Article: {news.Url}");

        // Open in browser for important news
        if (news.Priority == NewsPriorities.High)
        {
            OpenInBrowser(news.Url);
        }
    }
    else if (!string.IsNullOrEmpty(news.Story))
    {
        // Display full story if available
        Console.WriteLine($"  Story: {news.Story}");
    }
};

void OpenInBrowser(string url)
{
    try
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error opening URL: {ex.Message}");
    }
}
```

## News Storage and History

### Storing News for Later Analysis

```csharp
var newsHistory = new List<NewsMessage>();

connector.NewsReceived += (subscription, news) =>
{
    // Store news
    newsHistory.Add(news);

    // Keep only last 1000 news items
    if (newsHistory.Count > 1000)
        newsHistory.RemoveAt(0);
};

// Query historical news
void QueryNews(string keyword)
{
    var results = newsHistory
        .Where(n => (n.Headline ?? "").Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                   (n.Story ?? "").Contains(keyword, StringComparison.OrdinalIgnoreCase))
        .OrderByDescending(n => n.ServerTime)
        .ToList();

    Console.WriteLine($"Found {results.Count} news items containing '{keyword}':");
    foreach (var news in results.Take(10))
    {
        Console.WriteLine($"  {news.ServerTime:yyyy-MM-dd HH:mm} - {news.Headline}");
    }
}
```

## Connector Support

News support varies by connector:

```csharp
// Check if adapter supports news
var adapter = connector.Adapter;
if (adapter.IsMarketDataTypeSupported(DataType.News))
{
    Console.WriteLine("News is supported");
}
else
{
    Console.WriteLine("News is NOT supported");
}
```

Common sources supporting news:
- Bloomberg
- Reuters
- Dow Jones
- IQFeed
- Interactive Brokers (limited)
- Some broker-specific feeds

## Best Practices

1. **Filter by Priority**: Focus on high-priority news for trading decisions
2. **Security Association**: Pay special attention to security-specific news
3. **Keyword Matching**: Use keyword lists for automated news analysis
4. **Response Time**: News can cause rapid market movements; act quickly
5. **False Positives**: Always verify news authenticity before trading

```csharp
// Good: Filtered news processing
connector.NewsReceived += (subscription, news) =>
{
    // Filter by priority
    if (news.Priority != NewsPriorities.High)
        return;

    // Filter by security
    if (!news.SecurityId.HasValue)
        return;

    // Process relevant news
    ProcessImportantNews(news);
};

// Good: Safe keyword matching
bool ContainsKeyword(NewsMessage news, string keyword)
{
    var headline = news.Headline ?? "";
    var story = news.Story ?? "";

    return headline.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
           story.Contains(keyword, StringComparison.OrdinalIgnoreCase);
}
```

## Unsubscribing from News

```csharp
// Unsubscribe using subscription object
connector.UnSubscribe(subscription);

// Or find and unsubscribe
var subscriptions = connector.FindSubscriptions(null, DataType.News);
foreach (var sub in subscriptions)
{
    connector.UnSubscribe(sub);
}
```

## Related Topics

- [Level1 Data](level1-data.md) - Market quotes and statistics
- [Security Information](../02-Core-Concepts/securities.md) - Security details
- [Event-Driven Trading](../06-Strategies/event-driven.md) - Trading on news events
- [Risk Management](../07-Risk-Management/overview.md) - Managing news-driven volatility
