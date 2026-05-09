using Hangfire;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MyMealPlanner.Core.Enums;
using MyMealPlanner.Core.Interfaces;
using MyMealPlanner.Core.Models;
using MyMealPlanner.Infrastructure.Data;
using MyMealPlanner.Infrastructure.Migrations;
using MyMealPlanner.Services.AI;
using MyMealPlanner.Services.Cost;
using MyMealPlanner.Services.Email;
using MyMealPlanner.Services.Health;
using MyMealPlanner.Services.Localization;
using MyMealPlanner.Services.Maps;
using MyMealPlanner.Services.Notifications;
using MyMealPlanner.Services.PDF;
using MyMealPlanner.Services.Ranking;
using MyMealPlanner.Services.Scraper;
using MyMealPlanner.Services.Social;
using MyMealPlanner.Services.YouTube;
using MyMealPlanner.Web.Hubs;
using Serilog;
using System.Security.Claims;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/mymealplanner-.log", rollingInterval: RollingInterval.Day)
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, lc) => lc
        .ReadFrom.Configuration(ctx.Configuration)
        .WriteTo.Console()
        .WriteTo.File("logs/mymealplanner-.log", rollingInterval: RollingInterval.Day));

    // Database
    builder.Services.AddDbContext<ApplicationDbContext>(o =>
        o.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"),
            sql => sql.MigrationsAssembly("MyMealPlanner.Infrastructure")));

    // Identity
    builder.Services.AddIdentity<ApplicationUser, IdentityRole>(o =>
    {
        o.Password.RequiredLength = 8; o.Password.RequireUppercase = true;
        o.Password.RequireDigit = true; o.Password.RequireNonAlphanumeric = true;
        o.Lockout.MaxFailedAccessAttempts = 5;
        o.Lockout.DefaultLockoutTimeSpan  = TimeSpan.FromMinutes(15);
        o.SignIn.RequireConfirmedEmail = true;
        o.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

    // OAuth
    builder.Services.AddAuthentication()
        .AddGoogle(g => { g.ClientId = builder.Configuration["Auth:Google:ClientId"]!; g.ClientSecret = builder.Configuration["Auth:Google:ClientSecret"]!; })
        .AddFacebook(f => { f.AppId  = builder.Configuration["Auth:Facebook:AppId"]!; f.AppSecret  = builder.Configuration["Auth:Facebook:AppSecret"]!; });

    // Authorization
    builder.Services.AddAuthorization(opts =>
    {
        foreach (ChefLevel l in Enum.GetValues<ChefLevel>())
            opts.AddPolicy($"MinLevel{(int)l}", p => p.RequireAssertion(ctx =>
            {
                var c = ctx.User.FindFirst("ChefLevel")?.Value;
                return c != null && int.TryParse(c, out var ul) && ul >= (int)l;
            }));
        opts.AddPolicy("AdminOnly",     p => p.RequireRole("Admin"));
        opts.AddPolicy("ModeratorOnly", p => p.RequireRole("Moderator","Admin"));
        opts.AddPolicy("VerifiedChef",  p => p.RequireClaim("IsVerifiedChef","true"));
    });

    // Cookie
    builder.Services.ConfigureApplicationCookie(o =>
    {
        o.LoginPath = "/Account/Login"; o.LogoutPath = "/Account/Logout";
        o.ExpireTimeSpan = TimeSpan.FromDays(14); o.SlidingExpiration = true;
        o.Cookie.HttpOnly = true; o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    });
    builder.Services.AddSession(o => { o.IdleTimeout = TimeSpan.FromMinutes(30); o.Cookie.HttpOnly = true; o.Cookie.IsEssential = true; });

    // Redis
    builder.Services.AddStackExchangeRedisCache(o => { o.Configuration = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379"; o.InstanceName = "MyMealPlanner_"; });

    // HTTP Clients
    builder.Services.AddHttpClient("ScraperClient",     c => c.Timeout = TimeSpan.FromSeconds(30));
    builder.Services.AddHttpClient("YouTubeClient",     c => c.Timeout = TimeSpan.FromSeconds(10));
    builder.Services.AddHttpClient("TranslationClient", c => c.Timeout = TimeSpan.FromSeconds(15));
    builder.Services.AddHttpClient("AIClient",          c => c.Timeout = TimeSpan.FromSeconds(30));

    // Hangfire
    builder.Services.AddHangfire(c => c.SetDataCompatibilityLevel(CompatibilityLevel.Version_180).UseSimpleAssemblyNameTypeSerializer().UseRecommendedSerializerSettings().UseSqlServerStorage(builder.Configuration.GetConnectionString("DefaultConnection"), new SqlServerStorageOptions { QueuePollInterval = TimeSpan.Zero, UseRecommendedIsolationLevel = true, DisableGlobalLocks = true }));
    builder.Services.AddHangfireServer(o => { o.WorkerCount = 4; o.Queues = ["scraper","ranking","notifications","email","default"]; });

    // SignalR
    builder.Services.AddSignalR(o => { o.EnableDetailedErrors = builder.Environment.IsDevelopment(); o.MaximumReceiveMessageSize = 64*1024; });

    // Localisation
    var cultures = new[]{"en","fr","es","pt","ar","zh","hi","sw","de","it","ja","ko"};
    builder.Services.AddLocalization(o => o.ResourcesPath = "Resources");
    builder.Services.Configure<RequestLocalizationOptions>(o => { o.SetDefaultCulture("en"); o.AddSupportedCultures(cultures); o.AddSupportedUICultures(cultures); });

    // Rate limiting
    builder.Services.AddRateLimiter(o => { o.AddFixedWindowLimiter("Api",a=>{a.Window=TimeSpan.FromMinutes(1);a.PermitLimit=60;a.QueueLimit=5;}); o.AddFixedWindowLimiter("Auth",a=>{a.Window=TimeSpan.FromMinutes(15);a.PermitLimit=10;}); });

    // MVC
    builder.Services.AddControllersWithViews().AddViewLocalization().AddDataAnnotationsLocalization();
    builder.Services.AddRazorPages();

    // ── All Application Services ─────────────────────────────
    builder.Services.AddScoped<IRecipeScraperService,   RecipeScraperService>();
    builder.Services.AddScoped<IContentNormalizerService, ContentNormalizerService>();
    builder.Services.AddScoped<IRankingService,           RankingService>();
    builder.Services.AddScoped<IYouTubeService,           YouTubeService>();
    builder.Services.AddScoped<IAIChatAssistantService,   AIChatAssistantService>();
    builder.Services.AddScoped<IAITaggerService,          AITaggerService>();
    builder.Services.AddScoped<IImageSearchService,       ImageSearchService>();
    builder.Services.AddScoped<ITranslationService,       LibreTranslationService>();
    builder.Services.AddScoped<INotificationService,      NotificationService>();
    builder.Services.AddScoped<IAllergyService,           AllergyService>();
    builder.Services.AddScoped<IJokeService,              JokeService>();
    builder.Services.AddScoped<IFollowService,            FollowService>();
    builder.Services.AddScoped<IPersonalisationService,   PersonalisationService>();
    builder.Services.AddScoped<IQuizService,              QuizService>();
    builder.Services.AddScoped<IIngredientCostService,    IngredientCostService>();
    builder.Services.AddScoped<PdfExportService>();
    builder.Services.AddScoped<NearbyFoodService>();
    builder.Services.AddScoped<IEmailService,             EmailService>();

    var app = builder.Build();

    // Migrate + Seed
    using (var scope = app.Services.CreateScope())
    {
        var db    = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        await db.Database.MigrateAsync();
        await SeedRolesAsync(roles);
        await SeedAdminAsync(users, app.Configuration);
        await DatabaseSeeder.SeedReferenceDataAsync(db);
    }

    // Middleware
    if (app.Environment.IsDevelopment()) app.UseDeveloperExceptionPage();
    else { app.UseExceptionHandler("/Home/Error"); app.UseHsts(); }

    app.UseHttpsRedirection();
    app.UseStaticFiles();
    app.UseRequestLocalization();
    app.UseRateLimiter();
    app.UseRouting();
    app.UseSession();
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseSerilogRequestLogging();

    app.MapGet("/health", () => Results.Ok(new { status = "healthy", time = DateTime.UtcNow }));
    app.UseHangfireDashboard("/admin/jobs", new DashboardOptions { Authorization = [new HangfireAuthFilter()] });

    app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
    app.MapRazorPages();
    app.MapHub<RecipeHub>("/hubs/recipe");
    app.MapHub<ChatHub>("/hubs/chat");
    app.MapHub<NotificationHub>("/hubs/notifications");
    app.MapHub<MiaHub>("/hubs/mia");

    // Recurring jobs
    RecurringJob.AddOrUpdate<IRecipeScraperService>("scrape-all-sources", s => s.ScrapeAllSourcesAsync(CancellationToken.None), "0 */6 * * *", new RecurringJobOptions { QueueName = "scraper" });
    RecurringJob.AddOrUpdate<IRankingService>("recalculate-rankings", s => s.RecalculateAllRankingsAsync(CancellationToken.None), "0 */3 * * *", new RecurringJobOptions { QueueName = "ranking" });
    RecurringJob.AddOrUpdate<INotificationService>("daily-meal-suggestions", s => s.ScheduleDailyMealSuggestionsAsync(), "0 8 * * *", new RecurringJobOptions { QueueName = "notifications" });
    RecurringJob.AddOrUpdate<INotificationService>("weekly-meal-plans", s => s.ScheduleWeeklyMealPlansAsync(), "0 7 * * 1", new RecurringJobOptions { QueueName = "notifications" });
    RecurringJob.AddOrUpdate<INotificationService>("re-engagement", s => s.SendReEngagementAsync(), "0 18 * * *", new RecurringJobOptions { QueueName = "notifications" });
    RecurringJob.AddOrUpdate<IJokeService>("scrape-jokes", s => s.ScrapeNewJokesAsync(), "0 6 * * *");
    RecurringJob.AddOrUpdate<IJokeService>("generate-ai-jokes", s => s.GenerateAIJokesAsync(5), "0 2 * * *");

    await app.RunAsync();
}
catch (Exception ex) { Log.Fatal(ex, "MyMealPlanner startup failed"); }
finally { Log.CloseAndFlush(); }

static async Task SeedRolesAsync(RoleManager<IdentityRole> rm)
{
    foreach (var r in new[]{"Admin","Moderator","VerifiedChef","Contributor","Member","Guest"})
        if (!await rm.RoleExistsAsync(r)) await rm.CreateAsync(new IdentityRole(r));
}

static async Task SeedAdminAsync(UserManager<ApplicationUser> um, IConfiguration cfg)
{
    var email = cfg["Seed:AdminEmail"] ?? "admin@mymealplanner.app";
    if (await um.FindByEmailAsync(email) != null) return;
    var u = new ApplicationUser { UserName = email, Email = email, FullName = "Platform Admin", EmailConfirmed = true, IsVerifiedChef = true, ChefLevel = ChefLevel.Level8_GrandChef, VerificationTick = VerificationTick.Gold };
    var r = await um.CreateAsync(u, cfg["Seed:AdminPassword"] ?? "Admin@MyMealPlanner2024!");
    if (r.Succeeded) { await um.AddToRoleAsync(u, "Admin"); await um.AddClaimAsync(u, new Claim("ChefLevel","8")); await um.AddClaimAsync(u, new Claim("IsVerifiedChef","true")); }
}

public class HangfireAuthFilter : Hangfire.Dashboard.IDashboardAuthorizationFilter
{
    public bool Authorize(Hangfire.Dashboard.DashboardContext ctx)
        => ctx.GetHttpContext().User.IsInRole("Admin");
}
