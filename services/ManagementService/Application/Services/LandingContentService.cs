using System.Text.Json;
using ManagementService.Application.Interfaces;
using ManagementService.Contracts.Landing;
using ManagementService.Contracts.Responses.Admin;
using ManagementService.Domain.Entities;
using ManagementService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ManagementService.Application.Services;

public sealed class LandingContentService : ILandingContentService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    private readonly ManagementDbContext _dbContext;
    private readonly ILandingDefaultsProvider _defaultsProvider;
    private readonly ILogger<LandingContentService> _logger;

    public LandingContentService(
        ManagementDbContext dbContext,
        ILandingDefaultsProvider defaultsProvider,
        ILogger<LandingContentService> logger)
    {
        _dbContext = dbContext;
        _defaultsProvider = defaultsProvider;
        _logger = logger;
    }

    public async Task<LandingContentDocument> GetPublicAsync(string locale, CancellationToken cancellationToken = default)
    {
        var normalizedLocale = LandingLocales.Normalize(locale);
        var entity = await _dbContext.LandingLocaleContents
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Locale == normalizedLocale, cancellationToken);

        return entity is null
            ? await _defaultsProvider.GetAsync(normalizedLocale, cancellationToken)
            : DeserializeOrFallback(entity, cancellationToken);
    }

    public async Task<AdminLandingLocaleContentResponse> GetAdminAsync(string locale, CancellationToken cancellationToken = default)
    {
        var normalizedLocale = LandingLocales.Normalize(locale);
        var entity = await _dbContext.LandingLocaleContents
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Locale == normalizedLocale, cancellationToken);

        if (entity is null)
        {
            return new AdminLandingLocaleContentResponse
            {
                Locale = normalizedLocale,
                Source = "fallback",
                Content = await _defaultsProvider.GetAsync(normalizedLocale, cancellationToken)
            };
        }

        return new AdminLandingLocaleContentResponse
        {
            Locale = normalizedLocale,
            Source = "database",
            UpdatedAtUtc = entity.UpdatedAtUtc,
            UpdatedByUserId = entity.UpdatedByUserId,
            UpdatedByEmail = entity.UpdatedByEmail,
            Content = DeserializeOrFallback(entity, cancellationToken)
        };
    }

    public async Task<AdminLandingLocaleContentResponse> SaveAsync(
        string locale,
        LandingContentDocument content,
        Guid? updatedByUserId,
        string? updatedByEmail,
        CancellationToken cancellationToken = default)
    {
        var normalizedLocale = LandingLocales.Normalize(locale);
        var entity = await _dbContext.LandingLocaleContents
            .FirstOrDefaultAsync(item => item.Locale == normalizedLocale, cancellationToken);

        if (entity is null)
        {
            entity = new LandingLocaleContent
            {
                Locale = normalizedLocale
            };
            _dbContext.LandingLocaleContents.Add(entity);
        }

        entity.ContentJson = JsonSerializer.Serialize(content, JsonOptions);
        entity.UpdatedAtUtc = DateTime.UtcNow;
        entity.UpdatedByUserId = updatedByUserId;
        entity.UpdatedByEmail = updatedByEmail;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new AdminLandingLocaleContentResponse
        {
            Locale = normalizedLocale,
            Source = "database",
            UpdatedAtUtc = entity.UpdatedAtUtc,
            UpdatedByUserId = entity.UpdatedByUserId,
            UpdatedByEmail = entity.UpdatedByEmail,
            Content = content
        };
    }

    private LandingContentDocument DeserializeOrFallback(LandingLocaleContent entity, CancellationToken cancellationToken)
    {
        try
        {
            return JsonSerializer.Deserialize<LandingContentDocument>(entity.ContentJson, JsonOptions) ?? new LandingContentDocument();
        }
        catch (JsonException exception)
        {
            _logger.LogError(
                exception,
                "Failed to deserialize landing CMS content for locale {Locale}. Falling back to bundled landing defaults.",
                entity.Locale);
            return _defaultsProvider.GetAsync(entity.Locale, cancellationToken).GetAwaiter().GetResult();
        }
    }
}
