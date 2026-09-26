namespace UrlShortener.Core.Entities;

public class ShortenedUrl
{
    public long Id { get; private set; }
    public string LongUrl { get; private set; } = string.Empty;
    public string ShortCode { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public long ClickCount { get; private set; } = 0;

    // EF Core Constructor
    private ShortenedUrl() { }

    public ShortenedUrl(string longUrl)
    {
        if (string.IsNullOrWhiteSpace(longUrl))
            throw new ArgumentException("Long URL cannot be empty.", nameof(longUrl));

        LongUrl = longUrl;
        CreatedAt = DateTime.UtcNow;
        ClickCount = 0;
    }

    public void SetShortCode(string shortCode)
    {
        if (string.IsNullOrWhiteSpace(shortCode))
            throw new ArgumentException("Short code cannot be empty.", nameof(shortCode));

        ShortCode = shortCode;
    }

    public void IncrementClickCount(long count = 1)
    {
        ClickCount += count;
    }
}
