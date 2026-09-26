using UrlShortener.Core.Dtos;
using UrlShortener.Core.Entities;
using UrlShortener.Core.Interfaces;

namespace UrlShortener.Application.Services;

public class UrlShortenerService : IUrlShortenerService
{
    private readonly IUrlRepository _repository;
    private readonly ICacheService _cacheService;
    private readonly IBase62Encoder _encoder;
    private readonly ILogger<UrlShortenerService> _logger;
    private static readonly TimeSpan CacheExpiry = TimeSpan.FromHours(24);

    public UrlShortenerService(
        IUrlRepository repository,
        ICacheService cacheService,
        IBase62Encoder encoder,
        ILogger<UrlShortenerService> logger)
    {
        _repository = repository;
        _cacheService = cacheService;
        _encoder = encoder;
        _logger = logger;
    }

    public async Task<ShortenUrlResponseDto> ShortenUrlAsync(ShortenUrlRequestDto request, string baseUrl, CancellationToken cancellationToken = default)
    {
        // 1. Check if URL was already shortened before
        var existing = await _repository.GetByLongUrlAsync(request.Url, cancellationToken);
        if (existing != null)
        {
            return new ShortenUrlResponseDto(
                existing.ShortCode,
                $"{baseUrl}/{existing.ShortCode}",
                existing.LongUrl,
                existing.CreatedAt
            );
        }

        // 2. Create Domain Entity
        var entity = new ShortenedUrl(request.Url);
        await _repository.AddAsync(entity, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken); // Generates Auto-Increment ID

        // 3. Generate Base62 ShortCode from ID
        var shortCode = _encoder.Encode(entity.Id);
        entity.SetShortCode(shortCode);
        await _repository.SaveChangesAsync(cancellationToken);

        // 4. Populate Cache (Write-Through)
        await _cacheService.SetStringAsync($"url:{shortCode}", entity.LongUrl, CacheExpiry, cancellationToken);

        _logger.LogInformation("URL Shortened successfully: {ShortCode} -> {LongUrl}", shortCode, entity.LongUrl);

        return new ShortenUrlResponseDto(
            shortCode,
            $"{baseUrl}/{shortCode}",
            entity.LongUrl,
            entity.CreatedAt
        );
    }

    public async Task<(string? LongUrl, bool FromCache)> GetLongUrlAsync(string shortCode, CancellationToken cancellationToken = default)
    {
        // 1. Step: Try Redis Cache (Cache Hit)
        var cachedUrl = await _cacheService.GetStringAsync($"url:{shortCode}", cancellationToken);
        if (!string.IsNullOrEmpty(cachedUrl))
        {
            _logger.LogInformation("Cache HIT for shortCode: {ShortCode}", shortCode);
            // Async click count increment in cache
            _ = _cacheService.IncrementAsync($"clicks:{shortCode}", 1, cancellationToken);
            return (cachedUrl, true);
        }

        _logger.LogWarning("Cache MISS for shortCode: {ShortCode}. Falling back to Database.", shortCode);

        // 2. Step: Database Fallback (Cache Miss)
        var record = await _repository.GetByShortCodeAsync(shortCode, cancellationToken);
        if (record == null)
        {
            return (null, false);
        }

        // 3. Step: Populate Cache for next read requests
        await _cacheService.SetStringAsync($"url:{shortCode}", record.LongUrl, CacheExpiry, cancellationToken);

        // Update Click count in DB
        record.IncrementClickCount();
        await _repository.UpdateAsync(record, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        return (record.LongUrl, false);
    }

    public async Task<UrlAnalyticsDto?> GetAnalyticsAsync(string shortCode, CancellationToken cancellationToken = default)
    {
        var record = await _repository.GetByShortCodeAsync(shortCode, cancellationToken);
        if (record == null)
        {
            return null;
        }

        var cachedClicksRaw = await _cacheService.GetStringAsync($"clicks:{shortCode}", cancellationToken);
        long cachedClicks = long.TryParse(cachedClicksRaw, out var parsed) ? parsed : 0;

        return new UrlAnalyticsDto(
            record.ShortCode,
            record.LongUrl,
            record.CreatedAt,
            TotalClicks: record.ClickCount + cachedClicks,
            DbClicks: record.ClickCount,
            CachedClicks: cachedClicks
        );
    }
}
