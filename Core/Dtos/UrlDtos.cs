namespace UrlShortener.Core.Dtos;

public record ShortenUrlResponseDto(
    string ShortCode,
    string ShortUrl,
    string LongUrl,
    DateTime CreatedAt
);

public record UrlAnalyticsDto(
    string ShortCode,
    string LongUrl,
    DateTime CreatedAt,
    long TotalClicks,
    long DbClicks,
    long CachedClicks
);
