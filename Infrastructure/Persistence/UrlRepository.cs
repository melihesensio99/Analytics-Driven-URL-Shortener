using Microsoft.EntityFrameworkCore;
using UrlShortener.Core.Entities;
using UrlShortener.Core.Interfaces;

namespace UrlShortener.Infrastructure.Persistence;

public class UrlRepository : IUrlRepository
{
    private readonly AppDbContext _context;

    public UrlRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ShortenedUrl?> GetByLongUrlAsync(string longUrl, CancellationToken cancellationToken = default)
    {
        return await _context.ShortenedUrls
            .FirstOrDefaultAsync(u => u.LongUrl == longUrl, cancellationToken);
    }

    public async Task<ShortenedUrl?> GetByShortCodeAsync(string shortCode, CancellationToken cancellationToken = default)
    {
        return await _context.ShortenedUrls
            .FirstOrDefaultAsync(u => u.ShortCode == shortCode, cancellationToken);
    }

    public async Task AddAsync(ShortenedUrl shortenedUrl, CancellationToken cancellationToken = default)
    {
        await _context.ShortenedUrls.AddAsync(shortenedUrl, cancellationToken);
    }

    public Task UpdateAsync(ShortenedUrl shortenedUrl, CancellationToken cancellationToken = default)
    {
        _context.ShortenedUrls.Update(shortenedUrl);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
