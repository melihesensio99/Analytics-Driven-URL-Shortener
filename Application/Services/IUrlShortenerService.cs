using UrlShortener.Core.Dtos;

namespace UrlShortener.Application.Services;

public interface IUrlShortenerService
{
    Task<ShortenUrlResponseDto> ShortenUrlAsync(ShortenUrlRequestDto request, string baseUrl, CancellationToken cancellationToken = default);
    Task<(string? LongUrl, bool FromCache)> GetLongUrlAsync(string shortCode, CancellationToken cancellationToken = default);
    Task<UrlAnalyticsDto?> GetAnalyticsAsync(string shortCode, CancellationToken cancellationToken = default);
}
