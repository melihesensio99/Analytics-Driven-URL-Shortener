namespace UrlShortener.Core.Events;

public record UrlClickedEvent(
    string ShortCode,
    string? IpAddress,
    string? UserAgent,
    string? Referer,
    DateTime ClickedAt
);
