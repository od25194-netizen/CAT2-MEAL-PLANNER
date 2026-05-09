using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMealPlanner.Core.Enums;
using MyMealPlanner.Core.Interfaces;
using MyMealPlanner.Core.Models;
using MyMealPlanner.Infrastructure.Data;

namespace MyMealPlanner.Services.Ranking;

/// <summary>
/// Calculates and persists dish rankings at Global, Continent, Country, City and Weekly scopes.
/// Designed to run as a Hangfire background job every 3 hours.
/// Formula: weighted engagement score with a time-decay factor to keep rankings fresh.
/// </summary>
public class RankingService : IRankingService
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<RankingService> _logger;

    public RankingService(ApplicationDbContext db, ILogger<RankingService> logger)
    {
        _db     = db;
        _logger = logger;
    }

    // ── Score Formula ─────────────────────────────────────────
    public double CalculateScore(Recipe recipe)
    {
        // Weighted engagement
        double raw =
            recipe.LikeCount     * 0.30 +
            recipe.SaveCount      * 0.25 +
            recipe.CookLogCount   * 0.20 +
            recipe.CommentCount   * 0.15 +
            recipe.ShareCount     * 0.10;

        // Rating bonus (0–5 → up to +20% lift)
        double ratingBonus = recipe.RatingAverage / 5.0 * 0.20 * raw;
        raw += ratingBonus;

        // Time decay: older recipes score lower so trending new recipes surface
        double ageDays = (DateTime.UtcNow - recipe.CreatedAt).TotalDays;
        return raw / Math.Pow(ageDays + 2, 0.75);
    }

    // ── Full Recalculation (Hangfire job) ─────────────────────
    public async Task RecalculateAllRankingsAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("[Ranking] Starting full recalculation at {Time}", DateTime.UtcNow);

        var published = await _db.Recipes
            .Where(r => r.IsPublished)
            .Include(r => r.Rankings)
            .ToListAsync(ct);

        // Remove stale rankings
        _db.DishRankings.RemoveRange(
            await _db.DishRankings.Where(r =>
                r.CalculatedAt < DateTime.UtcNow.AddDays(-1)).ToListAsync(ct));

        // Score all recipes
        var scored = published
            .Select(r => (Recipe: r, Score: CalculateScore(r)))
            .OrderByDescending(x => x.Score)
            .ToList();

        await UpsertRankingsAsync(scored, RankingScope.Global,    null,       ct);

        // Continent-level
        foreach (var continent in scored.Select(x => x.Recipe.OriginContinent)
                                        .Where(c => c is not null).Distinct())
        {
            var sub = scored.Where(x => x.Recipe.OriginContinent == continent).ToList();
            await UpsertRankingsAsync(sub, RankingScope.Continent, continent, ct);
        }

        // Country-level
        foreach (var country in scored.Select(x => x.Recipe.OriginCountry)
                                      .Where(c => c is not null).Distinct())
        {
            var sub = scored.Where(x => x.Recipe.OriginCountry == country).ToList();
            await UpsertRankingsAsync(sub, RankingScope.Country, country, ct);
        }

        // Weekly trending (last 7 days only)
        var weeklyScored = scored
            .Where(x => x.Recipe.CreatedAt >= DateTime.UtcNow.AddDays(-7))
            .OrderByDescending(x => x.Score)
            .ToList();
        await UpsertRankingsAsync(weeklyScored, RankingScope.Weekly, null, ct);

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("[Ranking] Recalculation complete — {Count} recipes ranked", published.Count);
    }

    // ── Query ─────────────────────────────────────────────────
    public async Task<List<DishRanking>> GetTopByScope(
        RankingScope scope, string? scopeValue, int count = 50)
        => await _db.DishRankings
            .Where(r => r.Scope == scope &&
                       (scopeValue == null || r.ScopeValue == scopeValue))
            .OrderBy(r => r.RankPosition)
            .Take(count)
            .Include(r => r.Recipe)
            .ToListAsync();

    public async Task<List<DishRanking>> GetTrendingThisWeekAsync(
        string? countryCode, int count = 20)
    {
        var query = _db.DishRankings
            .Where(r => r.Scope == RankingScope.Weekly);

        if (countryCode is not null)
            query = query.Where(r => r.ScopeValue == countryCode ||
                                     r.ScopeValue == null);

        return await query
            .OrderBy(r => r.RankPosition)
            .Take(count)
            .Include(r => r.Recipe)
            .ToListAsync();
    }

    // ── Private ───────────────────────────────────────────────
    private async Task UpsertRankingsAsync(
        List<(Recipe Recipe, double Score)> scored,
        RankingScope scope,
        string? scopeValue,
        CancellationToken ct)
    {
        // Load existing for this scope to calculate trend
        var existing = await _db.DishRankings
            .Where(r => r.Scope == scope && r.ScopeValue == scopeValue)
            .ToDictionaryAsync(r => r.RecipeId, r => r.RankPosition, ct);

        // Remove old and insert fresh
        var old = _db.DishRankings
            .Where(r => r.Scope == scope && r.ScopeValue == scopeValue);
        _db.DishRankings.RemoveRange(old);

        for (int i = 0; i < Math.Min(scored.Count, 200); i++)
        {
            var (recipe, score) = scored[i];
            int newPos = i + 1;
            int prevPos = existing.TryGetValue(recipe.Id, out var p) ? p : newPos;

            _db.DishRankings.Add(new DishRanking
            {
                RecipeId      = recipe.Id,
                Scope         = scope,
                ScopeValue    = scopeValue,
                RankPosition  = newPos,
                Score         = score,
                WeeklyTrend   = prevPos - newPos,   // positive = moved up
                CalculatedAt  = DateTime.UtcNow
            });
        }
    }
}
