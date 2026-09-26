namespace UrlShortener.Core.Entities;

public class UrlClickLog
{
    public long Id { get; private set; }
    public string ShortCode { get; private set; } = string.Empty;
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }
    public string? Referer { get; private set; }
    public DateTime ClickedAt { get; private set; }

    // EF Core Constructor
    private UrlClickLog() { }

    public UrlClickLog(string shortCode, string? ipAddress, string? userAgent, string? referer, DateTime clickedAt)
    {
        ShortCode = shortCode;
        IpAddress = ipAddress;
        UserAgent = userAgent;
        Referer = referer;
        ClickedAt = clickedAt;
    }
}
