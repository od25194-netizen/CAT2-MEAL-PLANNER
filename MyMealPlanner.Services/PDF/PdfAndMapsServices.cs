using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MyMealPlanner.Core.Models;
using MyMealPlanner.Infrastructure.Data;
using System.Text.Json;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MyMealPlanner.Services.PDF;

/// <summary>
/// Generates PDF exports for:
///   - Weekly meal plans (grid layout with recipes)
///   - Shopping lists (grouped by store section)
///   - Individual recipes (print-friendly)
/// Uses QuestPDF (open source, free for non-commercial use).
/// </summary>
public class PdfExportService
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<PdfExportService> _logger;

    public PdfExportService(ApplicationDbContext db, ILogger<PdfExportService> logger)
    {
        _db     = db;
        _logger = logger;
        QuestPDF.Settings.License = LicenseType.Community;
    }

    // ── Meal Plan PDF ─────────────────────────────────────────
    public async Task<byte[]> GenerateMealPlanPdfAsync(int planId, string userId)
    {
        var plan = await _db.MealPlans
            .Include(p => p.Items)
                .ThenInclude(i => i.Recipe)
            .Include(p => p.ShoppingList)
                .ThenInclude(s => s!.Items)
            .FirstOrDefaultAsync(p => p.Id == planId && p.UserId == userId);

        if (plan is null) return Array.Empty<byte>();

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(10));

                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text("🍽️ My Meal Planner")
                           .FontSize(22).Bold().FontColor(Color.FromHex("E8630A"));
                        row.ConstantItem(120).AlignRight()
                           .Text($"Week of {plan.WeekStartDate:MMMM d, yyyy}")
                           .FontSize(9).FontColor(Colors.Grey.Darken2);
                    });
                    col.Item().PaddingVertical(4).LineHorizontal(1).LineColor(Color.FromHex("E8630A"));
                });

                page.Content().Column(col =>
                {
                    col.Item().PaddingBottom(10)
                       .Text("WEEKLY MEAL PLAN").Bold().FontSize(12)
                       .FontColor(Color.FromHex("1A1A2E"));

                    // Meal grid
                    var days      = new[] { "Mon","Tue","Wed","Thu","Fri","Sat","Sun" };
                    var mealTypes = new[] { "Breakfast","Lunch","Dinner" };

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(55);
                            for (int i = 0; i < 7; i++) cols.RelativeColumn();
                        });

                        // Header
                        table.Header(h =>
                        {
                            h.Cell().Background(Color.FromHex("E8630A")).Padding(4)
                             .Text("Meal").FontColor(Colors.White).Bold().FontSize(9);
                            foreach (var d in days)
                                h.Cell().Background(Color.FromHex("E8630A")).Padding(4)
                                 .AlignCenter().Text(d).FontColor(Colors.White).Bold().FontSize(9);
                        });

                        // Rows
                        foreach (var mealType in mealTypes)
                        {
                            table.Cell().Background(Color.FromHex("FFF0E6")).Padding(4)
                                 .Text(mealType).Bold().FontSize(8);

                            for (int day = 1; day <= 7; day++)
                            {
                                var item = plan.Items.FirstOrDefault(
                                    i => i.DayOfWeek == day &&
                                         i.MealType.ToString() == mealType);

                                table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten2)
                                     .Padding(3).Text(item?.Recipe?.Title ?? "—")
                                     .FontSize(7.5f)
                                     .FontColor(item != null ? Colors.Black : Colors.Grey.Medium);
                            }
                        }
                    });

                    // Shopping list
                    if (plan.ShoppingList?.Items?.Any() == true)
                    {
                        col.Item().PaddingTop(16).Text("SHOPPING LIST").Bold().FontSize(12)
                           .FontColor(Color.FromHex("1A1A2E"));

                        var bySection = plan.ShoppingList.Items
                            .GroupBy(i => i.StoreSection ?? "Other")
                            .OrderBy(g => g.Key);

                        col.Item().Row(row =>
                        {
                            foreach (var section in bySection)
                            {
                                row.RelativeItem().Column(sectionCol =>
                                {
                                    sectionCol.Item().PaddingTop(8)
                                              .Text(section.Key.ToUpperInvariant())
                                              .Bold().FontSize(8)
                                              .FontColor(Color.FromHex("E8630A"));

                                    foreach (var si in section.OrderBy(s => s.IngredientName))
                                    {
                                        sectionCol.Item().Row(r =>
                                        {
                                            r.ConstantItem(10).Height(10).Width(10)
                                             .Border(1).BorderColor(Colors.Grey.Lighten2);
                                            r.RelativeItem().PaddingLeft(5)
                                             .Text($"{si.Quantity} {si.Unit} {si.IngredientName}")
                                             .FontSize(8);
                                        });
                                    }
                                });
                            }
                        });
                    }
                });

                page.Footer().AlignCenter()
                    .Text(txt =>
                    {
                        txt.Span("Generated by My Meal Planner · ").FontSize(8).FontColor(Colors.Grey.Medium);
                        txt.Span(DateTime.UtcNow.ToString("MMMM d, yyyy")).FontSize(8).FontColor(Colors.Grey.Medium);
                    });
            });
        }).GeneratePdf();
    }

    // ── Recipe PDF ────────────────────────────────────────────
    public async Task<byte[]> GenerateRecipePdfAsync(int recipeId)
    {
        var recipe = await _db.Recipes
            .Include(r => r.Ingredients.OrderBy(i => i.SortOrder))
            .Include(r => r.Steps.OrderBy(s => s.StepOrder))
            .FirstOrDefaultAsync(r => r.Id == recipeId && r.IsPublished);

        if (recipe is null) return Array.Empty<byte>();

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(11));

                page.Header().Column(col =>
                {
                    col.Item().Text("🍽️ My Meal Planner")
                       .FontSize(14).Bold().FontColor(Color.FromHex("E8630A"));
                    col.Item().PaddingVertical(4).LineHorizontal(1).LineColor(Color.FromHex("E8630A"));
                });

                page.Content().Column(col =>
                {
                    col.Item().Text(recipe.Title).FontSize(24).Bold()
                       .FontColor(Color.FromHex("1A1A2E"));

                    if (!string.IsNullOrEmpty(recipe.OriginCountry))
                        col.Item().PaddingTop(4)
                           .Text($"🌍 {recipe.OriginCountry} · {recipe.MealType} · {recipe.DifficultyLevel}")
                           .FontColor(Colors.Grey.Darken2);

                    col.Item().PaddingTop(4).Row(row =>
                    {
                        row.RelativeItem().Text($"⏱️ Prep: {recipe.PrepTimeMinutes} min");
                        row.RelativeItem().Text($"🔥 Cook: {recipe.CookTimeMinutes} min");
                        row.RelativeItem().Text($"👥 Serves: {recipe.Servings}");
                    });

                    if (!string.IsNullOrEmpty(recipe.Description))
                        col.Item().PaddingTop(10).Text(recipe.Description)
                           .FontColor(Colors.Grey.Darken2).Italic();

                    if (!string.IsNullOrEmpty(recipe.CulturalStory))
                    {
                        col.Item().PaddingTop(10)
                           .Background(Color.FromHex("FFF0E6")).Padding(8).Column(c =>
                           {
                               c.Item().Text("Cultural Story").Bold().FontColor(Color.FromHex("E8630A"));
                               c.Item().PaddingTop(4).Text(recipe.CulturalStory);
                           });
                    }

                    col.Item().PaddingTop(16)
                       .Text("INGREDIENTS").Bold().FontSize(13).FontColor(Color.FromHex("E8630A"));

                    col.Item().PaddingTop(6).Column(ingCol =>
                    {
                        foreach (var ing in recipe.Ingredients)
                        {
                            ingCol.Item().Row(r =>
                            {
                                r.ConstantItem(8).PaddingTop(4)
                                 .Width(5).Height(5).Background(Color.FromHex("E8630A"));
                                r.RelativeItem().PaddingLeft(8)
                                 .Text($"{ing.Quantity} {ing.Unit} {ing.Name}")
                                 .FontSize(10.5f);
                            });
                        }
                    });

                    col.Item().PaddingTop(16)
                       .Text("METHOD").Bold().FontSize(13).FontColor(Color.FromHex("E8630A"));

                    col.Item().PaddingTop(6).Column(stepsCol =>
                    {
                        foreach (var step in recipe.Steps)
                        {
                            stepsCol.Item().PaddingBottom(8).Row(r =>
                            {
                                r.ConstantItem(28).Height(28)
                                 .Background(Color.FromHex("E8630A")).AlignCenter()
                                 .Text(step.StepOrder.ToString()).FontColor(Colors.White).Bold();
                                r.RelativeItem().PaddingLeft(10).Text(step.Instruction);
                            });

                            if (!string.IsNullOrEmpty(step.ChefTip))
                                stepsCol.Item().PaddingLeft(38).PaddingBottom(8)
                                        .Background(Color.FromHex("FFF8F0")).Padding(6)
                                        .Text($"💡 Chef tip: {step.ChefTip}")
                                        .Italic().FontSize(9.5f)
                                        .FontColor(Color.FromHex("C05008"));
                        }
                    });
                });

                page.Footer().AlignCenter()
                    .Text(txt =>
                    {
                        txt.Span("My Meal Planner · ").FontSize(8).FontColor(Colors.Grey.Medium);
                        txt.Span(recipe.Title).FontSize(8).Bold().FontColor(Color.FromHex("E8630A"));
                    });
            });
        }).GeneratePdf();
    }
}



/// <summary>
/// Finds restaurants, food markets, and grocery stores near the user
/// using the Google Maps Places API.
/// Free tier: $200/month credit (covers ~6,700 nearby searches).
/// </summary>
public class NearbyFoodService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<NearbyFoodService> _logger;

    public NearbyFoodService(
        IHttpClientFactory httpFactory,
        IConfiguration config,
        ILogger<NearbyFoodService> logger)
    {
        _http   = httpFactory.CreateClient("ScraperClient");
        _config = config;
        _logger = logger;
    }

    public record PlaceResult(
        string PlaceId,
        string Name,
        string? Address,
        double? Rating,
        int? PriceLevel,
        string? PhotoUrl,
        double Lat,
        double Lng,
        bool IsOpen,
        string? PhoneNumber,
        string? Website);

    public async Task<List<PlaceResult>> GetNearbyRestaurantsAsync(
        double lat, double lng, string? cuisineType = null, int radius = 2000)
        => await SearchNearbyAsync(lat, lng, "restaurant", cuisineType, radius);

    public async Task<List<PlaceResult>> GetNearbyFoodMarketsAsync(
        double lat, double lng, int radius = 3000)
        => await SearchNearbyAsync(lat, lng, "supermarket|grocery_or_supermarket|food", null, radius);

    public async Task<List<PlaceResult>> GetNearbyHotelsWithDiningAsync(
        double lat, double lng, int radius = 5000)
        => await SearchNearbyAsync(lat, lng, "lodging|hotel", null, radius);

    private async Task<List<PlaceResult>> SearchNearbyAsync(
        double lat, double lng, string type, string? keyword, int radius)
    {
        var apiKey = _config["GoogleMaps:ApiKey"];
        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogWarning("[Maps] Google Maps API key not configured");
            return new List<PlaceResult>();
        }

        try
        {
            var keywordParam = keyword is not null ? $"&keyword={Uri.EscapeDataString(keyword)}" : "";
            var url = $"https://maps.googleapis.com/maps/api/place/nearbysearch/json" +
                      $"?location={lat},{lng}" +
                      $"&radius={radius}" +
                      $"&type={type}" +
                      $"{keywordParam}" +
                      $"&key={apiKey}";

            var response = await _http.GetAsync(url);
            if (!response.IsSuccessStatusCode) return new List<PlaceResult>();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            var results = doc.RootElement.GetProperty("results")
                .EnumerateArray()
                .Take(10)
                .Select(place =>
                {
                    var geometry = place.GetProperty("geometry").GetProperty("location");
                    var photos   = place.TryGetProperty("photos", out var ph) && ph.GetArrayLength() > 0
                        ? $"https://maps.googleapis.com/maps/api/place/photo?maxwidth=400" +
                          $"&photoreference={ph[0].GetProperty("photo_reference").GetString()}" +
                          $"&key={apiKey}"
                        : null;

                    return new PlaceResult(
                        PlaceId:    place.GetProperty("place_id").GetString() ?? "",
                        Name:       place.GetProperty("name").GetString() ?? "",
                        Address:    place.TryGetProperty("vicinity", out var a) ? a.GetString() : null,
                        Rating:     place.TryGetProperty("rating", out var r) ? r.GetDouble() : null,
                        PriceLevel: place.TryGetProperty("price_level", out var p) ? p.GetInt32() : null,
                        PhotoUrl:   photos,
                        Lat:        geometry.GetProperty("lat").GetDouble(),
                        Lng:        geometry.GetProperty("lng").GetDouble(),
                        IsOpen:     place.TryGetProperty("opening_hours", out var oh) &&
                                    oh.TryGetProperty("open_now", out var on) && on.GetBoolean(),
                        PhoneNumber: null,
                        Website:    null
                    );
                })
                .ToList();

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Maps] Places search failed at {Lat},{Lng}", lat, lng);
            return new List<PlaceResult>();
        }
    }

    public async Task<string?> GetUserLocationFromIpAsync(string ipAddress)
    {
        try
        {
            if (ipAddress == "::1" || ipAddress == "127.0.0.1") return null;
            var response = await _http.GetStringAsync($"http://ip-api.com/json/{ipAddress}");
            using var doc = JsonDocument.Parse(response);
            var root = doc.RootElement;
            if (root.GetProperty("status").GetString() == "fail") return null;

            return JsonSerializer.Serialize(new
            {
                countryCode = root.GetProperty("countryCode").GetString(),
                country     = root.GetProperty("country").GetString(),
                city        = root.GetProperty("city").GetString(),
                lat         = root.GetProperty("lat").GetDouble(),
                lon         = root.GetProperty("lon").GetDouble(),
            });
        }
        catch { return null; }
    }
}
