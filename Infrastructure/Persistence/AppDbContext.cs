using Microsoft.EntityFrameworkCore;
using UrlShortener.Core.Entities;

namespace UrlShortener.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<ShortenedUrl> ShortenedUrls => Set<ShortenedUrl>();
    public DbSet<UrlClickLog> UrlClickLogs => Set<UrlClickLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ShortenedUrl>(entity =>
        {
            entity.ToTable("shortened_urls");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ShortCode).IsUnique();
            entity.HasIndex(e => e.LongUrl);
            entity.Property(e => e.LongUrl).IsRequired();
            entity.Property(e => e.ShortCode).HasMaxLength(10);
        });

        modelBuilder.Entity<UrlClickLog>(entity =>
        {
            entity.ToTable("url_click_logs");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ShortCode);
            entity.HasIndex(e => e.ClickedAt);
            entity.Property(e => e.ShortCode).IsRequired().HasMaxLength(10);
        });
    }
}
