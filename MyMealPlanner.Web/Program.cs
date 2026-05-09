using Hangfire;
using Hangfire.MemoryStorage;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using MyMealPlanner.Services.Localization;
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
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, lc) => lc
        .ReadFrom.Configuration(ctx.Configuration)
        .WriteTo.Console());

    // ── Database (SQLite for dev, SQL Server for prod) ──────
    var rawConnStr = builder.Configuration.GetConnectionString("DefaultConnection")!;
    Log.Information("Initializing database with connection string starting with: {Prefix}...", 
        string.IsNullOrEmpty(rawConnStr) ? "NULL" : rawConnStr.Split(':')[0]);
    var connStr = ParsePostgresUri(rawConnStr);

    if (connStr.Contains(".db") || connStr.StartsWith("Data Source"))
    {
        builder.Services.AddDbContext<ApplicationDbContext>(o =>
            o.UseSqlite(connStr,
                sql => sql.MigrationsAssembly("MyMealPlanner.Infrastructure")));
    }
    else if (connStr.Contains("Host=") || connStr.StartsWith("postgres", StringComparison.OrdinalIgnoreCase) || connStr.Contains("Username=") || connStr.Contains("User Id="))
    {
        // PostgreSQL (Render managed DB)
        builder.Services.AddDbContext<ApplicationDbContext>(o =>
            o.UseNpgsql(connStr,
                sql => sql.MigrationsAssembly("MyMealPlanner.Infrastructure")));
    }
    else
    {
        builder.Services.AddDbContext<ApplicationDbContext>(o =>
            o.UseSqlServer(connStr,
                sql => sql.MigrationsAssembly("MyMealPlanner.Infrastructure")));
    }

    // ── Identity ─────────────────────────────────────────────
    builder.Services.AddIdentity<ApplicationUser, IdentityRole>(o =>
    {
        o.Password.RequiredLength = 8; o.Password.RequireUppercase = true;
        o.Password.RequireDigit = true; o.Password.RequireNonAlphanumeric = true;
        o.Lockout.MaxFailedAccessAttempts = 5;
        o.Lockout.DefaultLockoutTimeSpan  = TimeSpan.FromMinutes(15);
        o.SignIn.RequireConfirmedEmail = false; // easier for local dev
        o.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

    // ── OAuth (optional for dev) ──────────────────────────────
    var googleId = builder.Configuration["Auth:Google:ClientId"];
    var auth = builder.Services.AddAuthentication();
    if (!string.IsNullOrWhiteSpace(googleId) && googleId != "YOUR_GOOGLE_CLIENT_ID")
    {
        auth.AddGoogle(g =>
        {
            g.ClientId     = googleId!;
            g.ClientSecret = builder.Configuration["Auth:Google:ClientSecret"]!;
        });
    }

    // ── Authorization ─────────────────────────────────────────
    builder.Services.AddAuthorization(opts =>
    {
        foreach (ChefLevel l in Enum.GetValues<ChefLevel>())
            opts.AddPolicy($"MinLevel{(int)l}", p => p.RequireAssertion(ctx =>
            {
                var c = ctx.User.FindFirst("ChefLevel")?.Value;
                return c != null && int.TryParse(c, out var ul) && ul >= (int)l;
            }));
        opts.AddPolicy("AdminOnly",     p => p.RequireRole("Admin"));
        opts.AddPolicy("ModeratorOnly", p => p.RequireRole("Moderator", "Admin"));
        opts.AddPolicy("VerifiedChef",  p => p.RequireClaim("IsVerifiedChef", "true"));
    });

    // ── Cookie ────────────────────────────────────────────────
    builder.Services.ConfigureApplicationCookie(o =>
    {
        o.LoginPath  = "/Account/Login";
        o.LogoutPath = "/Account/Logout";
        o.ExpireTimeSpan   = TimeSpan.FromDays(14);
        o.SlidingExpiration = true;
        o.Cookie.HttpOnly  = true;
        o.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.None
            : CookieSecurePolicy.Always;
    });

    builder.Services.AddSession(o =>
    {
        o.IdleTimeout = TimeSpan.FromMinutes(30);
        o.Cookie.HttpOnly  = true;
        o.Cookie.IsEssential = true;
    });

    // ── Cache (in-memory for dev, Redis for prod) ─────────────
    var redisConn = builder.Configuration.GetConnectionString("Redis");
    if (!string.IsNullOrWhiteSpace(redisConn) && redisConn != "localhost:6379" && !builder.Environment.IsDevelopment())
    {
        builder.Services.AddStackExchangeRedisCache(o =>
        {
            o.Configuration = redisConn;
            o.InstanceName  = "MyMealPlanner_";
        });
    }
    else
    {
        builder.Services.AddDistributedMemoryCache();
    }

    // ── HTTP Clients ──────────────────────────────────────────
    builder.Services.AddHttpClient("ScraperClient",     c => c.Timeout = TimeSpan.FromSeconds(30));
    builder.Services.AddHttpClient("YouTubeClient",     c => c.Timeout = TimeSpan.FromSeconds(10));
    builder.Services.AddHttpClient("TranslationClient", c => c.Timeout = TimeSpan.FromSeconds(15));
    builder.Services.AddHttpClient("AIClient",          c => c.Timeout = TimeSpan.FromSeconds(30));

    // ── Hangfire (in-memory for dev) ──────────────────────────
    builder.Services.AddHangfire(c => c
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UseMemoryStorage());
    builder.Services.AddHangfireServer(o =>
    {
        o.WorkerCount = 2;
        o.Queues = new[] { "scraper", "ranking", "notifications", "email", "default" };
    });

    // ── SignalR ───────────────────────────────────────────────
    builder.Services.AddSignalR(o =>
    {
        o.EnableDetailedErrors = builder.Environment.IsDevelopment();
        o.MaximumReceiveMessageSize = 64 * 1024;
    });

    // ── Localisation ──────────────────────────────────────────
    var cultures = new[] { "en", "fr", "es", "pt", "ar", "zh", "hi", "sw", "de", "it", "ja", "ko" };
    builder.Services.AddLocalization(o => o.ResourcesPath = "Resources");
    builder.Services.Configure<RequestLocalizationOptions>(o =>
    {
        o.SetDefaultCulture("en");
        o.AddSupportedCultures(cultures);
        o.AddSupportedUICultures(cultures);
    });

    // ── Rate Limiting ─────────────────────────────────────────
    builder.Services.AddRateLimiter(o =>
    {
        o.AddFixedWindowLimiter("Api",  a => { a.Window = TimeSpan.FromMinutes(1);  a.PermitLimit = 60; a.QueueLimit = 5; });
        o.AddFixedWindowLimiter("Auth", a => { a.Window = TimeSpan.FromMinutes(15); a.PermitLimit = 10; });
    });

    // ── MVC ───────────────────────────────────────────────────
    builder.Services.AddControllersWithViews().AddViewLocalization().AddDataAnnotationsLocalization();
    builder.Services.AddRazorPages();

    // ── Application Services ──────────────────────────────────
    builder.Services.AddScoped<IRecipeScraperService,    RecipeScraperService>();
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

    // ── Migrate + Seed ────────────────────────────────────────
    using (var scope = app.Services.CreateScope())
    {
        var db    = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        await db.Database.EnsureCreatedAsync();
        await SeedRolesAsync(roles);
        await SeedAdminAsync(users, app.Configuration);
        await DatabaseSeeder.SeedReferenceDataAsync(db);
    }

    // ── Middleware ────────────────────────────────────────────
    var forwardedOptions = new ForwardedHeadersOptions
    {
        ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto
    };
    forwardedOptions.KnownNetworks.Clear();
    forwardedOptions.KnownProxies.Clear();
    app.UseForwardedHeaders(forwardedOptions);

    if (app.Environment.IsDevelopment()) app.UseDeveloperExceptionPage();
    else { app.UseExceptionHandler("/Home/Error"); app.UseHsts(); }

    if (!app.Environment.IsDevelopment()) app.UseHttpsRedirection();
    app.UseStaticFiles();
    app.UseRequestLocalization();
    app.UseRateLimiter();
    app.UseRouting();
    app.UseSession();
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseSerilogRequestLogging();

    app.MapGet("/health", () => Results.Ok(new { status = "healthy", time = DateTime.UtcNow }));
    app.UseHangfireDashboard("/admin/jobs", new DashboardOptions { Authorization = new[] { new HangfireAuthFilter() } });

    app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
    app.MapRazorPages();
    app.MapHub<RecipeHub>("/hubs/recipe");
    app.MapHub<ChatHub>("/hubs/chat");
    app.MapHub<NotificationHub>("/hubs/notifications");
    app.MapHub<MiaHub>("/hubs/mia");

    await app.RunAsync();
}
catch (Exception ex) { Log.Fatal(ex, "MyMealPlanner startup failed"); }
finally { Log.CloseAndFlush(); }

// ── Helper Methods ────────────────────────────────────────────
static async Task SeedRolesAsync(RoleManager<IdentityRole> rm)
{
    foreach (var r in new[] { "Admin", "Moderator", "VerifiedChef", "Contributor", "Member", "Guest" })
        if (!await rm.RoleExistsAsync(r)) await rm.CreateAsync(new IdentityRole(r));
}

static async Task SeedAdminAsync(UserManager<ApplicationUser> um, IConfiguration cfg)
{
    var email = cfg["Seed:AdminEmail"] ?? "danmclaston@gmail.com";
    if (await um.FindByEmailAsync(email) != null) return;
    var u = new ApplicationUser
    {
        UserName        = email,
        Email           = email,
        FullName        = "Platform Admin",
        EmailConfirmed  = true,
        IsVerifiedChef  = true,
        ChefLevel       = ChefLevel.Level8_GrandChef,
        VerificationTick = VerificationTick.Gold
    };
    var r = await um.CreateAsync(u, cfg["Seed:AdminPassword"] ?? "passwords1233");
    if (r.Succeeded)
    {
        await um.AddToRoleAsync(u, "Admin");
        await um.AddClaimAsync(u, new Claim("ChefLevel", "8"));
        await um.AddClaimAsync(u, new Claim("IsVerifiedChef", "true"));
    }
}

public class HangfireAuthFilter : Hangfire.Dashboard.IDashboardAuthorizationFilter
{
    public bool Authorize(Hangfire.Dashboard.DashboardContext ctx)
    {
        var httpContext = ((Hangfire.Dashboard.AspNetCoreDashboardContext)ctx).HttpContext;
        return httpContext.User.IsInRole("Admin");
    }
}

public partial class Program 
{
    public static string ParsePostgresUri(string uri)
    {
        if (string.IsNullOrEmpty(uri)) return uri;
        if (!uri.StartsWith("postgres://") && !uri.StartsWith("postgresql://")) return uri;

        try
        {
            // Handle postgresql:// by standardizing to postgres:// for Uri parser if needed, 
            // but Uri handles both. We just need to catch the prefix.
            var databaseUri = new Uri(uri);
            var userInfo = databaseUri.UserInfo.Split(':');
            var user = userInfo[0];
            var password = userInfo.Length > 1 ? userInfo[1] : "";
            var host = databaseUri.Host;
            var port = databaseUri.Port == -1 ? 5432 : databaseUri.Port;
            var database = databaseUri.AbsolutePath.TrimStart('/');

            return $"Host={host};Port={port};Database={database};Username={user};Password={password};SSL Mode=Require;Trust Server Certificate=true;";
        }
        catch { return uri; }
    }
}
