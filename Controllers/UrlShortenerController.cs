using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using UrlShortener.Application.Services;
using UrlShortener.Core.Dtos;
using UrlShortener.Core.Events;
using UrlShortener.Core.Interfaces;

namespace UrlShortener.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UrlShortenerController : ControllerBase
{
    private readonly IUrlShortenerService _service;
    private readonly IMessagePublisher _publisher;

    public UrlShortenerController(IUrlShortenerService service, IMessagePublisher publisher)
    {
        _service = service;
        _publisher = publisher;
    }

    /// <summary>
    /// Uzun URL'yi alır, veritabanı ID'si ve Base62 algoritması ile kısa link üretir.
    /// Güvenlik: IP Başına 1 Dakikada En Fazla 10 İstek (ShortenPolicy)
    /// </summary>
    [HttpPost("shorten")]
    [EnableRateLimiting("ShortenPolicy")]
    public async Task<IActionResult> Shorten([FromBody] ShortenUrlRequestDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Url) || !Uri.TryCreate(request.Url, UriKind.Absolute, out _))
        {
            return BadRequest(new { error = "Geçerli bir URL adresi giriniz." });
        }

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var response = await _service.ShortenUrlAsync(request, baseUrl, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Kısa kod için toplam tıklanma, DB tıklanması ve Redis önbellek tıklanması istatistiklerini getirir.
    /// </summary>
    [HttpGet("analytics/{shortCode}")]
    public async Task<IActionResult> GetAnalytics(string shortCode, CancellationToken cancellationToken)
    {
        var analytics = await _service.GetAnalyticsAsync(shortCode, cancellationToken);
        if (analytics == null)
        {
            return NotFound(new { error = "Kısa URL bulunamadı." });
        }
        return Ok(analytics);
    }

    /// <summary>
    /// Kısa Koda Tıklama ve HTTP 302 Yönlendirmesi
    /// (Root seviyesinde çalışır: http://localhost:5000/{shortCode})
    /// Güvenlik: IP Başına 1 Dakikada En Fazla 100 Yönlendirme İsteyi (RedirectPolicy)
    /// </summary>
    [HttpGet("/{shortCode}")]
    [EnableRateLimiting("RedirectPolicy")]
    public async Task<IActionResult> RedirectToLongUrl(string shortCode, CancellationToken cancellationToken)
    {
        var (longUrl, fromCache) = await _service.GetLongUrlAsync(shortCode, cancellationToken);

        if (longUrl == null)
        {
            return NotFound(new { error = "Kısa URL bulunamadı." });
        }

        // ASENKRON EVENT PUBLISH (Fire-and-forget: Kullanıcı yönlendirmesini yavaşlatmaz!)
        var clickEvent = new UrlClickedEvent(
            ShortCode: shortCode,
            IpAddress: HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent: Request.Headers["User-Agent"].ToString(),
            Referer: Request.Headers["Referer"].ToString(),
            ClickedAt: DateTime.UtcNow
        );

        _ = _publisher.PublishAsync("url-clicks", clickEvent, cancellationToken);

        // Response Header içinde X-Cache-Status bilgisini ekliyoruz:
        Response.Headers["X-Cache-Status"] = fromCache ? "HIT (Redis)" : "MISS (PostgreSQL)";

        // HTTP 302 Temporary Redirect (permanent: false)
        return Redirect(longUrl);
    }
}
