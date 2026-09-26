using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using UrlShortener.Application.Services;
using UrlShortener.Core.Interfaces;
using UrlShortener.Infrastructure.Caching;
using UrlShortener.Infrastructure.Messaging;
using UrlShortener.Infrastructure.Persistence;
using UrlShortener.Infrastructure.Services;
using UrlShortener.Middleware;

var builder = WebApplication.CreateBuilder(args);

// 1. Add Controllers & Swagger OpenAPI Generator
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 2. IP-Based Rate Limiting Configuration (Abuse & Bot Prevention)
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            status = 429,
            error = "İstek sınırı aşıldı! Lütfen 1 dakika sonra tekrar deneyiniz.",
            timestamp = DateTime.UtcNow
        }, cancellationToken: token);
    };

    // Policy 1: Shorten API Rate Limit (Max 10 requests per minute per IP)
    options.AddPolicy("ShortenPolicy", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    // Policy 2: Redirect Rate Limit (Max 100 requests per minute per IP)
    options.AddPolicy("RedirectPolicy", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

// 3. Connection Strings Configuration
var postgresConnectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Host=localhost;Database=urlshortener;Username=postgres;Password=postgres";
var redisConnectionString = builder.Configuration.GetConnectionString("Redis") 
    ?? "localhost:6379";

// 4. Dependency Injection Container (SOLID - IoC)
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(postgresConnectionString));

builder.Services.AddSingleton<IConnectionMultiplexer>(sp => 
    ConnectionMultiplexer.Connect(redisConnectionString));

// Infrastructure Abstractions Injection
builder.Services.AddSingleton<IBase62Encoder, Base62Encoder>();
builder.Services.AddScoped<ICacheService, RedisCacheService>();
builder.Services.AddScoped<IUrlRepository, UrlRepository>();
builder.Services.AddScoped<IUrlShortenerService, UrlShortenerService>();

// Event-Driven RabbitMQ Messaging & Consumer Injection
builder.Services.AddSingleton<IMessagePublisher, RabbitMQPublisher>();
builder.Services.AddHostedService<UrlClickConsumerWorker>();

var app = builder.Build();

// 5. Rate Limiter Middleware
app.UseRateLimiter();

// 6. Swagger UI Middleware
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "URL Shortener API v1");
    c.RoutePrefix = "swagger";
});

// 7. Global Exception Handling Middleware
app.UseMiddleware<ExceptionHandlingMiddleware>();

// 8. Automatic Database Schema Creation
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

// 9. Map Controllers
app.MapControllers();

app.Run();
