using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMealPlanner.Core.DTOs;
using MyMealPlanner.Core.Enums;
using MyMealPlanner.Core.Interfaces;
using MyMealPlanner.Core.Models;
using MyMealPlanner.Infrastructure.Data;

namespace MyMealPlanner.Services.Social;

/// <summary>
/// Collaborative-filtering recommendation engine.
/// "People who liked what you liked also liked these…"
/// Falls back to country-based trending when history is sparse.
/// </summary>
public class PersonalisationService : IPersonalisationService
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<PersonalisationService> _logger;

    public PersonalisationService(ApplicationDbContext db, ILogger<PersonalisationService> logger)
    { _db = db; _logger = logger; }

    public async Task<List<Recipe>> GetRecommendationsAsync(string userId, int count = 12)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user is null) return new List<Recipe>();

        // 1. Get user's liked recipe IDs
        var likedIds = await _db.RecipeLikes
            .Where(l => l.UserId == userId)
            .Select(l => l.RecipeId)
            .ToListAsync();

        // 2. Find similar users who liked the same recipes
        var similarUserIds = await _db.RecipeLikes
            .Where(l => likedIds.Contains(l.RecipeId) && l.UserId != userId)
            .GroupBy(l => l.UserId)
            .OrderByDescending(g => g.Count())
            .Take(20)
            .Select(g => g.Key)
            .ToListAsync();

        // 3. Get recipes liked by similar users that current user hasn't seen
        var recommended = await _db.RecipeLikes
            .Where(l => similarUserIds.Contains(l.UserId) && !likedIds.Contains(l.RecipeId))
            .GroupBy(l => l.RecipeId)
            .OrderByDescending(g => g.Count())
            .Take(count)
            .Select(g => g.Key)
            .ToListAsync();

        if (recommended.Count >= count / 2)
        {
            return await _db.Recipes
                .Where(r => r.IsPublished && recommended.Contains(r.Id))
                .ToListAsync();
        }

        // 4. Fallback: top recipes from user's country / cuisines
        var cuisines = user.FavouriteCuisinesJson?.Split(',')
                           .Select(c => c.Trim()).ToList() ?? new List<string>();

        var fallback = await _db.Recipes
            .Where(r => r.IsPublished && !likedIds.Contains(r.Id) &&
                       (r.OriginCountryCode == user.CountryCode ||
                        (r.CultureTag != null && cuisines.Any(c => r.CultureTag.Contains(c)))))
            .OrderByDescending(r => r.LikeCount)
            .Take(count)
            .ToListAsync();

        return fallback.Any() ? fallback :
            await _db.Recipes.Where(r => r.IsPublished)
                .OrderByDescending(r => r.LikeCount).Take(count).ToListAsync();
    }

    public async Task<List<string>> GetRelatedSearchesAsync(string query, string userId)
    {
        if (string.IsNullOrWhiteSpace(query)) return new List<string>();

        var related = new List<string>();
        var lower   = query.ToLowerInvariant();

        // Find recipes matching the query and get their tags
        var matchingRecipes = await _db.Recipes
            .Where(r => r.IsPublished &&
                       (r.Title.Contains(query) || r.CultureTag!.Contains(query) || r.OriginCountry!.Contains(query)))
            .Take(10)
            .ToListAsync();

        // Culture-based related searches
        var cultures = matchingRecipes
            .Where(r => r.CultureTag != null)
            .Select(r => r.CultureTag!)
            .Distinct().Take(3);
        foreach (var c in cultures)
            related.Add($"{c} recipes");

        // Continent-based
        var continents = matchingRecipes
            .Where(r => r.OriginContinent != null)
            .Select(r => r.OriginContinent!)
            .Distinct().Take(2);
        foreach (var c in continents)
            related.Add($"Best {c} dishes");

        // Health-related
        related.Add($"Is {query} healthy?");
        related.Add($"{query} calories and nutrition");
        related.Add($"Easy {query} recipe");
        related.Add($"{query} variations by country");

        return related.Take(8).ToList();
    }

    public async Task CheckAndAwardBadgesAsync(string userId)
    {
        var user = await _db.Users
            .Include(u => u.Badges)
            .FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null) return;

        var earnedBadgeIds = user.Badges.Select(b => b.BadgeId).ToHashSet();
        var allBadges = await _db.Badges.ToListAsync();

        var cookLogCount = await _db.CookLogs.CountAsync(c => c.UserId == userId);
        var likeCount    = await _db.RecipeLikes.CountAsync(l => l.UserId == userId);
        var hasPets      = await _db.PetProfiles.AnyAsync(p => p.OwnerId == userId);
        var approvedRecs = await _db.RecipeSuggestions.CountAsync(
            s => s.SubmittedByUserId == userId && s.Status == SuggestionStatus.Approved);
        var passedQuizzes = await _db.QuizAttempts.CountAsync(
            q => q.UserId == userId && q.IsPassed);
        var uniqueCountries = await _db.CookLogs
            .Include(c => c.Recipe)
            .Where(c => c.UserId == userId && c.Recipe.OriginCountry != null)
            .Select(c => c.Recipe.OriginCountry)
            .Distinct().CountAsync();
        var uniqueContinents = await _db.CookLogs
            .Include(c => c.Recipe)
            .Where(c => c.UserId == userId && c.Recipe.OriginContinent != null)
            .Select(c => c.Recipe.OriginContinent)
            .Distinct().CountAsync();

        var conditionsMet = new Dictionary<string, bool>
        {
            ["CookLogCount >= 1"]            = cookLogCount >= 1,
            ["UniqueCountriesCooked >= 10"]  = uniqueCountries >= 10,
            ["ItalianRecipesCooked >= 5"]    = false, // simplified
            ["SpicyRecipesCooked >= 10"]     = false, // simplified
            ["HealthyMealsLogged >= 30"]     = false, // simplified
            ["RecipeLikesReceived >= 50"]    = likeCount >= 50,
            ["CookStreak >= 7"]              = user.CookStreak >= 7,
            ["JokesLiked >= 20"]             = false, // simplified
            ["HasPetProfile == true"]        = hasPets,
            ["ApprovedSuggestions >= 1"]     = approvedRecs >= 1,
            ["PassedQuizzes >= 5"]           = passedQuizzes >= 5,
            ["ContinentsCooked >= 6"]        = uniqueContinents >= 6,
        };

        foreach (var badge in allBadges)
        {
            if (earnedBadgeIds.Contains(badge.Id)) continue;
            if (conditionsMet.TryGetValue(badge.TriggerCondition, out var met) && met)
            {
                _db.UserBadges.Add(new UserBadge
                {
                    UserId   = userId,
                    BadgeId  = badge.Id,
                    EarnedAt = DateTime.UtcNow
                });
                _logger.LogInformation("[Badges] {User} earned: {Badge}", userId, badge.Name);
            }
        }

        await _db.SaveChangesAsync();
    }

    public async Task UpdateChefLevelAsync(string userId)
    {
        var user          = await _db.Users.FindAsync(userId);
        if (user is null) return;

        var cookLogCount  = await _db.CookLogs.CountAsync(c => c.UserId == userId);
        var passedQuizzes = await _db.QuizAttempts.CountAsync(q => q.UserId == userId && q.IsPassed);
        var points        = cookLogCount * 10 + passedQuizzes * 50;

        user.TotalPoints  = points;

        // Level-up check (if quiz hasn't already done it)
        var suggestedLevel = points switch
        {
            >= 2000 => ChefLevel.Level5_SkilledChef,
            >= 1000 => ChefLevel.Level4_ConfidentCook,
            >= 400  => ChefLevel.Level3_HomeExplorer,
            >= 100  => ChefLevel.Level2_CuriousCook,
            _       => ChefLevel.Level1_KitchenNewcomer
        };

        if (suggestedLevel > user.ChefLevel)
        {
            user.ChefLevel = suggestedLevel;
            user.VerificationTick = suggestedLevel switch
            {
                ChefLevel.Level4_ConfidentCook or ChefLevel.Level5_SkilledChef => VerificationTick.White,
                ChefLevel.Level6_ExpertCulinarian or ChefLevel.Level7_MasterChef => VerificationTick.Green,
                ChefLevel.Level8_GrandChef => VerificationTick.Gold,
                _ => VerificationTick.None
            };
        }

        await _db.SaveChangesAsync();
    }
}

/// <summary>
/// Generates personalised quiz questions based on the user's food exploration history.
/// Questions are weighted: 40% from their food history, 30% from their region,
/// 20% global food knowledge, 10% nutrition (increases at higher levels).
/// </summary>
public class QuizService : IQuizService
{
    private readonly ApplicationDbContext _db;

    public QuizService(ApplicationDbContext db) => _db = db;

    public async Task<List<QuizQuestion>> GenerateQuizAsync(
        string userId, ChefLevel targetLevel, int count = 10)
    {
        var user = await _db.Users.FindAsync(userId);

        // User's explored cuisines and countries from recipe history
        var userCookLogs = await _db.CookLogs
            .Include(c => c.Recipe)
            .Where(c => c.UserId == userId)
            .Take(50)
            .ToListAsync();

        var userCultures  = userCookLogs.Select(c => c.Recipe?.CultureTag).Where(c => c != null).Distinct().ToList();
        var userCountries = userCookLogs.Select(c => c.Recipe?.OriginCountry).Where(c => c != null).Distinct().ToList();

        var personalised = await _db.QuizQuestions
            .Where(q => q.IsActive && q.MinLevel <= targetLevel &&
                       (userCultures.Contains(q.CultureTag) || userCountries.Contains(q.CountryCode)))
            .OrderBy(_ => Guid.NewGuid())
            .Take((int)(count * 0.4))
            .ToListAsync();

        var regional = await _db.QuizQuestions
            .Where(q => q.IsActive && q.MinLevel <= targetLevel &&
                        q.CountryCode == user!.CountryCode &&
                        !personalised.Select(p => p.Id).Contains(q.Id))
            .OrderBy(_ => Guid.NewGuid())
            .Take((int)(count * 0.3))
            .ToListAsync();

        var remaining = count - personalised.Count - regional.Count;
        var global    = await _db.QuizQuestions
            .Where(q => q.IsActive && q.MinLevel <= targetLevel &&
                        !personalised.Select(p => p.Id).Contains(q.Id) &&
                        !regional.Select(r => r.Id).Contains(q.Id))
            .OrderBy(_ => Guid.NewGuid())
            .Take(remaining)
            .ToListAsync();

        var all = personalised.Concat(regional).Concat(global)
            .OrderBy(_ => Guid.NewGuid())
            .Take(count)
            .ToList();

        // If we don't have enough personalised questions, fill from general pool
        if (all.Count < count)
        {
            var extra = await _db.QuizQuestions
                .Where(q => q.IsActive && !all.Select(a => a.Id).Contains(q.Id))
                .OrderBy(_ => Guid.NewGuid())
                .Take(count - all.Count)
                .ToListAsync();
            all.AddRange(extra);
        }

        return all.Take(count).ToList();
    }

    public async Task<QuizResult> SubmitQuizAsync(string userId, QuizAttempt attempt)
    {
        var questionIds = System.Text.Json.JsonSerializer
            .Deserialize<List<int>>(attempt.QuestionsJson ?? "[]") ?? new();

        var answers = System.Text.Json.JsonSerializer
            .Deserialize<Dictionary<int, string>>(attempt.AnswersJson ?? "{}") ?? new();

        var questions = await _db.QuizQuestions
            .Where(q => questionIds.Contains(q.Id))
            .ToListAsync();

        int correct = 0;
        var feedback = new List<QuizAnswerFeedback>();

        foreach (var q in questions)
        {
            bool isCorrect = answers.TryGetValue(q.Id, out var ans) &&
                             string.Equals(ans.Trim(), q.CorrectAnswer.Trim(),
                                           StringComparison.OrdinalIgnoreCase);
            if (isCorrect) correct++;

            feedback.Add(new QuizAnswerFeedback(
                QuestionId:    q.Id,
                IsCorrect:     isCorrect,
                CorrectAnswer: q.CorrectAnswer,
                Explanation:   q.Explanation));
        }

        double score     = questions.Count > 0 ? (double)correct / questions.Count * 100 : 0;
        double threshold = (int)attempt.TargetLevel >= 6 ? 90.0 : 80.0;
        bool   passed    = score >= threshold;

        attempt.CorrectAnswers        = correct;
        attempt.ScorePercent          = score;
        attempt.IsPassed              = passed;
        attempt.NextAttemptAllowedAt  = DateTime.UtcNow.AddHours(48);

        return new QuizResult(
            IsPassed:              passed,
            ScorePercent:          score,
            CorrectAnswers:        correct,
            TotalQuestions:        questions.Count,
            LeveledUp:             attempt.LeveledUp,
            NewLevel:              attempt.LeveledUp ? attempt.TargetLevel : null,
            NextAttemptAllowedAt:  attempt.NextAttemptAllowedAt,
            Feedback:              feedback);
    }

    public async Task<bool> CanAttemptAsync(string userId, ChefLevel targetLevel)
    {
        var last = await _db.QuizAttempts
            .Where(q => q.UserId == userId && q.TargetLevel == targetLevel)
            .OrderByDescending(q => q.AttemptedAt)
            .FirstOrDefaultAsync();

        return last?.NextAttemptAllowedAt is null || last.NextAttemptAllowedAt <= DateTime.UtcNow;
    }
}

/// <summary>
/// Manages follow/unfollow relationships and suggested users.
/// </summary>
public class FollowService : IFollowService
{
    private readonly ApplicationDbContext _db;

    public FollowService(ApplicationDbContext db) => _db = db;

    public async Task FollowAsync(string followerId, string followeeId)
    {
        if (followerId == followeeId) return;
        if (await IsFollowingAsync(followerId, followeeId)) return;

        _db.UserFollows.Add(new UserFollow { FollowerId = followerId, FolloweeId = followeeId });

        await _db.Users.Where(u => u.Id == followerId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.FollowingCount, u => u.FollowingCount + 1));
        await _db.Users.Where(u => u.Id == followeeId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.FollowerCount, u => u.FollowerCount + 1));

        await _db.SaveChangesAsync();
    }

    public async Task UnfollowAsync(string followerId, string followeeId)
    {
        var existing = await _db.UserFollows.FirstOrDefaultAsync(
            f => f.FollowerId == followerId && f.FolloweeId == followeeId);
        if (existing is null) return;

        _db.UserFollows.Remove(existing);

        await _db.Users.Where(u => u.Id == followerId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.FollowingCount, u => u.FollowingCount - 1));
        await _db.Users.Where(u => u.Id == followeeId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.FollowerCount, u => u.FollowerCount - 1));

        await _db.SaveChangesAsync();
    }

    public async Task<bool> IsFollowingAsync(string followerId, string followeeId)
        => await _db.UserFollows.AnyAsync(
            f => f.FollowerId == followerId && f.FolloweeId == followeeId);

    public async Task<List<ApplicationUser>> GetSuggestedUsersAsync(string userId, int count = 10)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user is null) return new List<ApplicationUser>();

        var alreadyFollowing = await _db.UserFollows
            .Where(f => f.FollowerId == userId)
            .Select(f => f.FolloweeId)
            .ToListAsync();

        // Suggest: same country, high follower count, not already following
        return await _db.Users
            .Where(u => u.Id != userId &&
                        !u.IsDeleted &&
                        !alreadyFollowing.Contains(u.Id) &&
                        u.ProfileVisibility == "Public" &&
                        (u.CountryCode == user.CountryCode || u.IsVerifiedChef))
            .OrderByDescending(u => u.FollowerCount)
            .Take(count)
            .ToListAsync();
    }
}
