using UrlShortener.Core.Dtos;
using UrlShortener.Core.Entities;
using UrlShortener.Core.Interfaces;

namespace UrlShortener.Application.Services;

public class UrlShortenerAppService : IUrlShortenerService
{
    private readonly IUrlRepository _repository;
    private readonly ICacheService _cache;
    private readonly IBase62Encoder _encoder;
    private static readonly TimeSpan CacheExpiry = TimeSpan.FromHours(24);

    public UrlShortenerAppService(
        IUrlRepository repository,
        ICacheService cache,
        IBase62Encoder encoder)
    {
        _repository = repository;
        _cache = cache;
        _encoder = encoder;
    }

    public async Task<ShortenUrlResponseDto> ShortenUrlAsync(
        ShortenUrlRequestDto request, 
        string baseUrl, 
        CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetByLongUrlAsync(request.Url, cancellationToken);
        if (existing != null)
        {
            var fullShortUrl = $"{baseUrl}/{existing.ShortCode}";
            return new ShortenUrlResponseDto(existing.ShortCode, fullShortUrl, existing.LongUrl, existing.CreatedAt);
        }

        var shortenedUrl = new ShortenedUrl(request.Url);
        await _repository.AddAsync(shortenedUrl, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        var shortCode = _encoder.Encode(shortenedUrl.Id);
        shortenedUrl.SetShortCode(shortCode);
        await _repository.SaveChangesAsync(cancellationToken);

        await _cache.SetStringAsync($"url:{shortCode}", shortenedUrl.LongUrl, CacheExpiry, cancellationToken);

        var finalShortUrl = $"{baseUrl}/{shortCode}";
        return new ShortenUrlResponseDto(shortCode, finalShortUrl, shortenedUrl.LongUrl, shortenedUrl.CreatedAt);
    }

    public async Task<(string? LongUrl, bool FromCache)> GetLongUrlAsync(
        string shortCode, 
        CancellationToken cancellationToken = default)
    {
        // 1. Try Redis Cache
        var cachedUrl = await _cache.GetStringAsync($"url:{shortCode}", cancellationToken);
        if (!string.IsNullOrEmpty(cachedUrl))
        {
            _ = _cache.IncrementAsync($"clicks:{shortCode}", 1, cancellationToken);
            return (cachedUrl, true);
        }

        // 2. Fallback to Database
        var record = await _repository.GetByShortCodeAsync(shortCode, cancellationToken);
        if (record == null)
        {
            return (null, false);
        }

        // 3. Populate Redis Cache & Increment DB click count
        await _cache.SetStringAsync($"url:{shortCode}", record.LongUrl, CacheExpiry, cancellationToken);
        record.IncrementClickCount();
        await _repository.SaveChangesAsync(cancellationToken);

        return (record.LongUrl, false);
    }

    public async Task<UrlAnalyticsDto?> GetAnalyticsAsync(
        string shortCode, 
        CancellationToken cancellationToken = default)
    {
        var record = await _repository.GetByShortCodeAsync(shortCode, cancellationToken);
        if (record == null)
        {
            return null;
        }

        var redisClicksStr = await _cache.GetStringAsync($"clicks:{shortCode}", cancellationToken);
        long cachedClicks = 0;
        if (!string.IsNullOrEmpty(redisClicksStr) && long.TryParse(redisClicksStr, out var parsed))
        {
            cachedClicks = parsed;
        }

        return new UrlAnalyticsDto(
            record.ShortCode,
            record.LongUrl,
            record.CreatedAt,
            record.ClickCount + cachedClicks,
            record.ClickCount,
            cachedClicks
        );
    }
}
