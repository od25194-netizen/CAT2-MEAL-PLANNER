using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MyMealPlanner.Core.Models;

namespace MyMealPlanner.Infrastructure.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options) { }

    // ── Core ────────────────────────────────────────────────
    public DbSet<Recipe>                Recipes                 => Set<Recipe>();
    public DbSet<Ingredient>            Ingredients             => Set<Ingredient>();
    public DbSet<AlternativeIngredient> AlternativeIngredients  => Set<AlternativeIngredient>();
    public DbSet<RecipeStep>            RecipeSteps             => Set<RecipeStep>();
    public DbSet<RecipeTranslation>     RecipeTranslations      => Set<RecipeTranslation>();
    public DbSet<RecipeNutrition>       RecipeNutritions        => Set<RecipeNutrition>();
    public DbSet<IngredientCost>        IngredientCosts         => Set<IngredientCost>();
    public DbSet<DishRanking>           DishRankings            => Set<DishRanking>();

    // ── Social ──────────────────────────────────────────────
    public DbSet<Comment>           Comments            => Set<Comment>();
    public DbSet<CommentReaction>   CommentReactions    => Set<CommentReaction>();
    public DbSet<RecipeLike>        RecipeLikes         => Set<RecipeLike>();
    public DbSet<UserFollow>        UserFollows         => Set<UserFollow>();
    public DbSet<UserBlock>         UserBlocks          => Set<UserBlock>();
    public DbSet<SavedCollection>   SavedCollections    => Set<SavedCollection>();
    public DbSet<CollectionItem>    CollectionItems     => Set<CollectionItem>();
    public DbSet<CookLog>           CookLogs            => Set<CookLog>();

    // ── Chat ────────────────────────────────────────────────
    public DbSet<ChatRoom>      ChatRooms    => Set<ChatRoom>();
    public DbSet<ChatMessage>   ChatMessages => Set<ChatMessage>();

    // ── Meal Planner ─────────────────────────────────────────
    public DbSet<MealPlan>         MealPlans        => Set<MealPlan>();
    public DbSet<MealPlanItem>     MealPlanItems    => Set<MealPlanItem>();
    public DbSet<ShoppingList>     ShoppingLists    => Set<ShoppingList>();
    public DbSet<ShoppingListItem> ShoppingListItems => Set<ShoppingListItem>();

    // ── Pets ────────────────────────────────────────────────
    public DbSet<PetProfile> PetProfiles => Set<PetProfile>();

    // ── Content ─────────────────────────────────────────────
    public DbSet<RecipeSuggestion> RecipeSuggestions => Set<RecipeSuggestion>();
    public DbSet<CookingJoke>      CookingJokes      => Set<CookingJoke>();

    // ── Quiz & Badges ────────────────────────────────────────
    public DbSet<QuizQuestion> QuizQuestions => Set<QuizQuestion>();
    public DbSet<QuizAttempt>  QuizAttempts  => Set<QuizAttempt>();
    public DbSet<Badge>        Badges        => Set<Badge>();
    public DbSet<UserBadge>    UserBadges    => Set<UserBadge>();

    // ── Notifications ─────────────────────────────────────────
    public DbSet<Notification> Notifications => Set<Notification>();

    // ── Health ───────────────────────────────────────────────
    public DbSet<NutrientFood>       NutrientFoods       => Set<NutrientFood>();
    public DbSet<AllergyGuide>       AllergyGuides       => Set<AllergyGuide>();
    public DbSet<FoodHealthBenefit>  FoodHealthBenefits  => Set<FoodHealthBenefit>();
    public DbSet<FoodTimingGuide>    FoodTimingGuides    => Set<FoodTimingGuide>();

    // ── Scraper ──────────────────────────────────────────────
    public DbSet<ScrapeJob>   ScrapeJobs  => Set<ScrapeJob>();
    public DbSet<ScrapedRaw>  ScrapedRaws => Set<ScrapedRaw>();

    // ── Inventory & Diet ──────────────────────────────────────
    public DbSet<CookingEquipment> CookingEquipment => Set<CookingEquipment>();
    public DbSet<DietPlan>         DietPlans        => Set<DietPlan>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // ── ApplicationUser ──────────────────────────────────
        builder.Entity<ApplicationUser>(u =>
        {
            u.ToTable("Users");
            u.Property(x => x.FullName).HasMaxLength(200).IsRequired();
            u.Property(x => x.PreferredLanguage).HasMaxLength(10).HasDefaultValue("en");
            u.Property(x => x.PreferredCurrency).HasMaxLength(5).HasDefaultValue("USD");
            u.Property(x => x.AccentColor).HasMaxLength(20).HasDefaultValue("#E8630A");
        });

        // ── Recipe ───────────────────────────────────────────
        builder.Entity<Recipe>(r =>
        {
            r.HasIndex(x => x.Slug).IsUnique();
            r.HasIndex(x => x.OriginCountryCode);
            r.HasIndex(x => x.OriginContinent);
            r.HasIndex(x => x.IsPublished);
            r.HasIndex(x => x.CreatedAt);
            r.Property(x => x.Title).HasMaxLength(300).IsRequired();
            r.Property(x => x.Slug).HasMaxLength(350).IsRequired();
            r.Property(x => x.EstimatedCostUSD).HasPrecision(10, 2);

            r.HasMany(x => x.Ingredients)
             .WithOne(x => x.Recipe)
             .HasForeignKey(x => x.RecipeId)
             .OnDelete(DeleteBehavior.Cascade);

            r.HasMany(x => x.Steps)
             .WithOne(x => x.Recipe)
             .HasForeignKey(x => x.RecipeId)
             .OnDelete(DeleteBehavior.Cascade);

            r.HasMany(x => x.Comments)
             .WithOne(x => x.Recipe)
             .HasForeignKey(x => x.RecipeId)
             .OnDelete(DeleteBehavior.SetNull);

            r.HasOne(x => x.SubmittedByUser)
             .WithMany(x => x.Recipes)
             .HasForeignKey(x => x.SubmittedByUserId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        // ── Comment ──────────────────────────────────────────
        builder.Entity<Comment>(c =>
        {
            c.HasIndex(x => x.RecipeId);
            c.HasIndex(x => x.UserId);
            c.HasIndex(x => x.CreatedAt);

            c.HasMany(x => x.Replies)
             .WithOne(x => x.ParentComment)
             .HasForeignKey(x => x.ParentCommentId)
             .OnDelete(DeleteBehavior.Restrict);

            c.HasOne(x => x.User)
             .WithMany()
             .HasForeignKey(x => x.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── UserFollow (self-referencing m:m) ────────────────
        builder.Entity<UserFollow>(f =>
        {
            f.HasIndex(x => new { x.FollowerId, x.FolloweeId }).IsUnique();

            f.HasOne(x => x.Follower)
             .WithMany(x => x.Following)
             .HasForeignKey(x => x.FollowerId)
             .OnDelete(DeleteBehavior.Restrict);

            f.HasOne(x => x.Followee)
             .WithMany(x => x.Followers)
             .HasForeignKey(x => x.FolloweeId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── UserBlock ────────────────────────────────────────
        builder.Entity<UserBlock>(b =>
        {
            b.HasIndex(x => new { x.BlockerId, x.BlockedId }).IsUnique();

            b.HasOne(x => x.Blocker)
             .WithMany(x => x.BlockedUsers)
             .HasForeignKey(x => x.BlockerId)
             .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(x => x.Blocked)
             .WithMany()
             .HasForeignKey(x => x.BlockedId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── RecipeLike (composite unique) ────────────────────
        builder.Entity<RecipeLike>(l =>
        {
            l.HasIndex(x => new { x.RecipeId, x.UserId }).IsUnique();
        });

        // ── CommentReaction ───────────────────────────────────
        builder.Entity<CommentReaction>(cr =>
        {
            cr.HasIndex(x => new { x.CommentId, x.UserId }).IsUnique();
        });

        // ── DishRanking ───────────────────────────────────────
        builder.Entity<DishRanking>(dr =>
        {
            dr.HasIndex(x => new { x.Scope, x.ScopeValue, x.RankPosition });
            dr.Property(x => x.Score).HasPrecision(10, 4);
        });

        // ── IngredientCost ────────────────────────────────────
        builder.Entity<IngredientCost>(ic =>
        {
            ic.HasIndex(x => new { x.RecipeId, x.CountryCode });
            ic.Property(x => x.TotalCost).HasPrecision(10, 2);
            ic.Property(x => x.CostPerServing).HasPrecision(10, 2);
        });

        // ── PetProfile ────────────────────────────────────────
        builder.Entity<PetProfile>(p =>
        {
            p.HasOne(x => x.Owner)
             .WithMany(x => x.Pets)
             .HasForeignKey(x => x.OwnerId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── ShoppingList ──────────────────────────────────────
        builder.Entity<ShoppingList>(sl =>
        {
            sl.HasOne(x => x.MealPlan)
              .WithOne(x => x.ShoppingList)
              .HasForeignKey<ShoppingList>(x => x.MealPlanId);
        });

        // ── ScrapedRaw ────────────────────────────────────────
        builder.Entity<ScrapedRaw>(sr =>
        {
            sr.HasIndex(x => x.ContentHash).IsUnique();
            sr.HasIndex(x => x.SourceUrl);
        });

        // ── QuizAttempt ───────────────────────────────────────
        builder.Entity<QuizAttempt>(qa =>
        {
            qa.HasIndex(x => new { x.UserId, x.TargetLevel, x.AttemptedAt });
        });

        // ── Notification ──────────────────────────────────────
        builder.Entity<Notification>(n =>
        {
            n.HasIndex(x => new { x.UserId, x.IsRead, x.CreatedAt });
        });

        // ── ChatMessage ───────────────────────────────────────
        builder.Entity<ChatMessage>(cm =>
        {
            cm.HasIndex(x => new { x.RoomId, x.SentAt });
            cm.HasOne(x => x.Sender)
              .WithMany(x => x.ChatMessages)
              .HasForeignKey(x => x.SenderId)
              .OnDelete(DeleteBehavior.Restrict);
        });

        // ── NutrientFood ──────────────────────────────────────
        builder.Entity<NutrientFood>(nf =>
        {
            nf.HasIndex(x => x.NutrientCategory);
            nf.Property(x => x.AmountPer100g).HasPrecision(10, 3);
        });
    }
}
