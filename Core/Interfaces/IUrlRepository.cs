using UrlShortener.Core.Entities;

namespace UrlShortener.Core.Interfaces;

public interface IUrlRepository
{
    Task<ShortenedUrl?> GetByLongUrlAsync(string longUrl, CancellationToken cancellationToken = default);
    Task<ShortenedUrl?> GetByShortCodeAsync(string shortCode, CancellationToken cancellationToken = default);
    Task AddAsync(ShortenedUrl shortenedUrl, CancellationToken cancellationToken = default);
    Task UpdateAsync(ShortenedUrl shortenedUrl, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
