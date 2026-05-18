using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using MyMealPlanner.Core.Enums;
using MyMealPlanner.Core.Models;
using MyMealPlanner.Infrastructure.Data;

namespace MyMealPlanner.Infrastructure.Migrations;

/// <summary>
/// Run: dotnet ef migrations add InitialCreate --project MyMealPlanner.Infrastructure --startup-project MyMealPlanner.Web
/// Then: dotnet ef database update --project MyMealPlanner.Infrastructure --startup-project MyMealPlanner.Web
/// </summary>
public static class MigrationInstructions
{
    public const string Commands = @"
# From solution root:

# 1. Create migration
dotnet ef migrations add InitialCreate \
    --project MyMealPlanner.Infrastructure \
    --startup-project MyMealPlanner.Web \
    --output-dir Migrations

# 2. Apply to database
dotnet ef database update \
    --project MyMealPlanner.Infrastructure \
    --startup-project MyMealPlanner.Web

# 3. On subsequent schema changes:
dotnet ef migrations add [MigrationName] \
    --project MyMealPlanner.Infrastructure \
    --startup-project MyMealPlanner.Web

# For SQL Server LocalDB (development):
# Connection string in appsettings.json is already configured.

# For PostgreSQL (production/free tier):
# Replace SqlServer with Npgsql in Infrastructure .csproj and DbContext options:
# services.AddDbContext<ApplicationDbContext>(o => o.UseNpgsql(connStr));
";
}

/// <summary>
/// Seeds the database with reference data — nutrient foods, allergy guides,
/// health benefits, food timing, scrape jobs, default badges, and sample jokes.
/// Called from Program.cs on startup.
/// </summary>
public static class DatabaseSeeder
{
    public static async Task SeedReferenceDataAsync(ApplicationDbContext db)
    {
        await SeedNutrientFoodsAsync(db);
        await SeedAllergyGuidesAsync(db);
        await SeedHealthBenefitsAsync(db);
        await SeedFoodTimingAsync(db);
        await SeedBadgesAsync(db);
        await SeedScrapeJobsAsync(db);
        await SeedJokesAsync(db);
        await SeedRecipesAsync(db);
        await SeedQuizQuestionsAsync(db);
        await SeedDietPlansAsync(db);
        await SeedEquipmentAsync(db);
        await db.SaveChangesAsync();
    }

    private static async Task SeedNutrientFoodsAsync(ApplicationDbContext db)
    {
        if (await db.NutrientFoods.AnyAsync()) return;

        var foods = new List<NutrientFood>
        {
            // Vitamin C
            new() { FoodName = "Kakadu Plum",    NutrientCategory = NutrientCategory.VitaminC, AmountPer100g = 5300, Unit = "mg", SortRank = 1, RegionAvailability = "Australia" },
            new() { FoodName = "Acerola Cherry", NutrientCategory = NutrientCategory.VitaminC, AmountPer100g = 1677, Unit = "mg", SortRank = 2, RegionAvailability = "Americas" },
            new() { FoodName = "Guava",          NutrientCategory = NutrientCategory.VitaminC, AmountPer100g = 228,  Unit = "mg", SortRank = 3, RegionAvailability = "Global" },
            new() { FoodName = "Bell Pepper",    NutrientCategory = NutrientCategory.VitaminC, AmountPer100g = 183,  Unit = "mg", SortRank = 4, RegionAvailability = "Global" },
            new() { FoodName = "Kiwi",           NutrientCategory = NutrientCategory.VitaminC, AmountPer100g = 93,   Unit = "mg", SortRank = 5, RegionAvailability = "Global" },
            new() { FoodName = "Orange",         NutrientCategory = NutrientCategory.VitaminC, AmountPer100g = 53,   Unit = "mg", SortRank = 6, RegionAvailability = "Global" },
            new() { FoodName = "Lemon",          NutrientCategory = NutrientCategory.VitaminC, AmountPer100g = 53,   Unit = "mg", SortRank = 7, RegionAvailability = "Global" },
            new() { FoodName = "Baobab Fruit",   NutrientCategory = NutrientCategory.VitaminC, AmountPer100g = 280,  Unit = "mg", SortRank = 3, RegionAvailability = "Africa" },

            // Iron
            new() { FoodName = "Dried Thyme",    NutrientCategory = NutrientCategory.Iron, AmountPer100g = 123, Unit = "mg", SortRank = 1 },
            new() { FoodName = "Dried Spirulina",NutrientCategory = NutrientCategory.Iron, AmountPer100g = 28,  Unit = "mg", SortRank = 2 },
            new() { FoodName = "Dark Chocolate", NutrientCategory = NutrientCategory.Iron, AmountPer100g = 17,  Unit = "mg", SortRank = 3 },
            new() { FoodName = "Lentils",        NutrientCategory = NutrientCategory.Iron, AmountPer100g = 6.5m,Unit = "mg", SortRank = 4, RegionAvailability = "Global" },
            new() { FoodName = "Spinach",        NutrientCategory = NutrientCategory.Iron, AmountPer100g = 2.7m,Unit = "mg", SortRank = 5, RegionAvailability = "Global" },
            new() { FoodName = "Beef (lean)",    NutrientCategory = NutrientCategory.Iron, AmountPer100g = 2.6m,Unit = "mg", SortRank = 6, RegionAvailability = "Global" },

            // Protein
            new() { FoodName = "Spirulina",      NutrientCategory = NutrientCategory.Protein, AmountPer100g = 57, Unit = "g", SortRank = 1 },
            new() { FoodName = "Dried Pumpkin Seeds", NutrientCategory = NutrientCategory.Protein, AmountPer100g = 30, Unit = "g", SortRank = 2 },
            new() { FoodName = "Chicken Breast", NutrientCategory = NutrientCategory.Protein, AmountPer100g = 31, Unit = "g", SortRank = 3, RegionAvailability = "Global" },
            new() { FoodName = "Tuna (canned)",  NutrientCategory = NutrientCategory.Protein, AmountPer100g = 29, Unit = "g", SortRank = 4, RegionAvailability = "Global" },
            new() { FoodName = "Greek Yoghurt",  NutrientCategory = NutrientCategory.Protein, AmountPer100g = 10, Unit = "g", SortRank = 5, RegionAvailability = "Global" },
            new() { FoodName = "Eggs",           NutrientCategory = NutrientCategory.Protein, AmountPer100g = 13, Unit = "g", SortRank = 6, RegionAvailability = "Global" },
            new() { FoodName = "Black Beans",    NutrientCategory = NutrientCategory.Protein, AmountPer100g = 8.9m,Unit = "g", SortRank = 7, RegionAvailability = "Global" },

            // Calcium
            new() { FoodName = "Cheese (Parmesan)", NutrientCategory = NutrientCategory.Calcium, AmountPer100g = 1184, Unit = "mg", SortRank = 1 },
            new() { FoodName = "Sesame Seeds",   NutrientCategory = NutrientCategory.Calcium, AmountPer100g = 975,  Unit = "mg", SortRank = 2 },
            new() { FoodName = "Sardines",       NutrientCategory = NutrientCategory.Calcium, AmountPer100g = 382,  Unit = "mg", SortRank = 3 },
            new() { FoodName = "Milk",           NutrientCategory = NutrientCategory.Calcium, AmountPer100g = 125,  Unit = "mg", SortRank = 4, RegionAvailability = "Global" },
            new() { FoodName = "Kale",           NutrientCategory = NutrientCategory.Calcium, AmountPer100g = 135,  Unit = "mg", SortRank = 5, RegionAvailability = "Global" },
            new() { FoodName = "Tofu",           NutrientCategory = NutrientCategory.Calcium, AmountPer100g = 350,  Unit = "mg", SortRank = 4, RegionAvailability = "Asia" },

            // Omega-3
            new() { FoodName = "Flaxseed Oil",   NutrientCategory = NutrientCategory.Omega3, AmountPer100g = 53, Unit = "g", SortRank = 1 },
            new() { FoodName = "Chia Seeds",     NutrientCategory = NutrientCategory.Omega3, AmountPer100g = 18, Unit = "g", SortRank = 2, RegionAvailability = "Americas" },
            new() { FoodName = "Mackerel",       NutrientCategory = NutrientCategory.Omega3, AmountPer100g = 4.9m,Unit = "g", SortRank = 3, RegionAvailability = "Global" },
            new() { FoodName = "Salmon",         NutrientCategory = NutrientCategory.Omega3, AmountPer100g = 2.3m,Unit = "g", SortRank = 4, RegionAvailability = "Global" },
            new() { FoodName = "Walnuts",        NutrientCategory = NutrientCategory.Omega3, AmountPer100g = 9.1m,Unit = "g", SortRank = 3, RegionAvailability = "Global" },

            // Fibre
            new() { FoodName = "Dried Chicory",  NutrientCategory = NutrientCategory.Fibre, AmountPer100g = 41, Unit = "g", SortRank = 1 },
            new() { FoodName = "Dried Acacia",   NutrientCategory = NutrientCategory.Fibre, AmountPer100g = 86, Unit = "g", SortRank = 0, RegionAvailability = "Africa" },
            new() { FoodName = "Black Beans",    NutrientCategory = NutrientCategory.Fibre, AmountPer100g = 8.7m,Unit = "g", SortRank = 2, RegionAvailability = "Global" },
            new() { FoodName = "Oats",           NutrientCategory = NutrientCategory.Fibre, AmountPer100g = 10.6m,Unit="g", SortRank = 3, RegionAvailability = "Global" },
            new() { FoodName = "Avocado",        NutrientCategory = NutrientCategory.Fibre, AmountPer100g = 6.7m,Unit = "g", SortRank = 4, RegionAvailability = "Global" },
            new() { FoodName = "Lentils",        NutrientCategory = NutrientCategory.Fibre, AmountPer100g = 7.9m,Unit = "g", SortRank = 5, RegionAvailability = "Global" },
        };

        db.NutrientFoods.AddRange(foods);
    }

    private static async Task SeedAllergyGuidesAsync(ApplicationDbContext db)
    {
        if (await db.AllergyGuides.AnyAsync()) return;

        db.AllergyGuides.AddRange(
            new AllergyGuide { AllergenType = AllergenType.Peanuts,  DefaultRisk = AllergenRisk.Critical, MildSymptoms = "Hives, itchy mouth, runny nose", SevereSymptoms = "Throat swelling, difficulty breathing, anaphylaxis", FirstResponse = "Use EpiPen if available. Call emergency services immediately.", HiddenSourcesJson = "[\"peanut oil\",\"groundnut\",\"satay sauce\",\"mixed nuts\"]", SafeSubstitutesJson = "[\"sunflower seed butter\",\"pumpkin seed paste\",\"soy nut butter\"]" },
            new AllergyGuide { AllergenType = AllergenType.Gluten,   DefaultRisk = AllergenRisk.Moderate, MildSymptoms = "Bloating, cramps, diarrhea, fatigue", SevereSymptoms = "Severe celiac flare, intestinal damage over time", FirstResponse = "Remove gluten from diet. Consult gastroenterologist.", HiddenSourcesJson = "[\"soy sauce\",\"malt\",\"beer\",\"barley\",\"spelt\",\"seitan\"]", SafeSubstitutesJson = "[\"tamari\",\"rice flour\",\"gluten-free oats\",\"quinoa\"]" },
            new AllergyGuide { AllergenType = AllergenType.Milk,     DefaultRisk = AllergenRisk.Moderate, MildSymptoms = "Bloating, gas, diarrhea, stomach cramps", SevereSymptoms = "Rare anaphylaxis in severe allergy (not lactose intolerance)", FirstResponse = "Antihistamine for mild. EpiPen if anaphylaxis.", HiddenSourcesJson = "[\"butter\",\"ghee\",\"cream\",\"casein\",\"whey\",\"curd\"]", SafeSubstitutesJson = "[\"oat milk\",\"almond milk\",\"coconut cream\",\"cashew cheese\"]" },
            new AllergyGuide { AllergenType = AllergenType.Eggs,     DefaultRisk = AllergenRisk.Moderate, MildSymptoms = "Skin rash, runny nose, stomach pain", SevereSymptoms = "Anaphylaxis in rare cases", FirstResponse = "Antihistamine. EpiPen if severe.", HiddenSourcesJson = "[\"mayonnaise\",\"meringue\",\"albumin\",\"caesar dressing\",\"some pasta\"]", SafeSubstitutesJson = "[\"flax egg (1 tbsp flax + 3 tbsp water)\",\"chia egg\",\"applesauce (baking)\",\"aquafaba\"]" },
            new AllergyGuide { AllergenType = AllergenType.Shellfish, DefaultRisk = AllergenRisk.Critical, MildSymptoms = "Stomach cramps, vomiting, rash", SevereSymptoms = "Anaphylaxis", FirstResponse = "EpiPen + call emergency services immediately.", HiddenSourcesJson = "[\"worcestershire sauce\",\"some fish sauces\",\"seafood stock\"]", SafeSubstitutesJson = "[\"king oyster mushrooms (texture)\",\"jackfruit\",\"hearts of palm\"]" }
        );
    }

    private static async Task SeedHealthBenefitsAsync(ApplicationDbContext db)
    {
        if (await db.FoodHealthBenefits.AnyAsync()) return;

        db.FoodHealthBenefits.AddRange(
            new FoodHealthBenefit { Condition = "Headache", FoodRemedy = "Watermelon, almonds, ginger tea, coffee (in moderation)", HowToUse = "Stay hydrated. Magnesium in almonds relaxes blood vessels.", FoodsToAvoid = "Alcohol, MSG, processed meats", IconEmoji = "🤕" },
            new FoodHealthBenefit { Condition = "Low Energy", FoodRemedy = "Banana, oats, dark chocolate, spinach, eggs", HowToUse = "Complex carbs + iron for sustained energy without crashes.", FoodsToAvoid = "Sugary drinks, white bread, energy drinks", IconEmoji = "😴" },
            new FoodHealthBenefit { Condition = "Anxiety & Stress", FoodRemedy = "Chamomile tea, salmon, blueberries, dark chocolate, avocado", HowToUse = "Omega-3 and magnesium reduce cortisol. Chamomile before bed.", FoodsToAvoid = "Excess caffeine, alcohol, sugar", IconEmoji = "😰" },
            new FoodHealthBenefit { Condition = "Cold & Flu", FoodRemedy = "Garlic, ginger, honey, lemon, turmeric, bone broth", HowToUse = "Garlic has allicin — anti-viral. Ginger reduces inflammation. Honey soothes throat.", FoodsToAvoid = "Dairy (thickens mucus), sugar (weakens immunity)", IconEmoji = "🤒" },
            new FoodHealthBenefit { Condition = "Poor Sleep", FoodRemedy = "Tart cherry juice, kiwi, warm milk, banana, chamomile, almonds", HowToUse = "Tart cherries contain melatonin. Kiwi improves sleep quality. Eat 2hrs before bed.", FoodsToAvoid = "Caffeine after 2pm, heavy meals, alcohol", IconEmoji = "😴" },
            new FoodHealthBenefit { Condition = "Digestive Issues", FoodRemedy = "Ginger, yoghurt with probiotics, papaya, kefir, fennel, peppermint tea", HowToUse = "Probiotics restore gut flora. Papain enzyme in papaya aids digestion.", FoodsToAvoid = "Fried food, excess fibre if inflamed, gas-causing legumes", IconEmoji = "😣" },
            new FoodHealthBenefit { Condition = "Inflammation", FoodRemedy = "Turmeric (with black pepper), ginger, olive oil, berries, fatty fish, leafy greens", HowToUse = "Curcumin in turmeric blocks inflammatory enzymes. Take with black pepper for absorption.", FoodsToAvoid = "Refined sugar, trans fats, processed foods, excess alcohol", IconEmoji = "🔥" },
            new FoodHealthBenefit { Condition = "Weight Management", FoodRemedy = "Eggs, oats, avocado, green tea, chilli, legumes, leafy greens", HowToUse = "Protein and fibre increase satiety. Capsaicin in chilli boosts metabolism.", FoodsToAvoid = "Ultra-processed foods, sugary drinks, refined carbs", IconEmoji = "⚖️" },
            new FoodHealthBenefit { Condition = "High Blood Pressure", FoodRemedy = "Beetroot juice, bananas, oats, dark chocolate, garlic, leafy greens", HowToUse = "Nitrates in beetroot relax blood vessels. Potassium in bananas reduces sodium effect.", FoodsToAvoid = "Excess salt, processed meats, alcohol, caffeine", IconEmoji = "🫀" },
            new FoodHealthBenefit { Condition = "Weak Immunity", FoodRemedy = "Garlic, citrus fruits, turmeric, ginger, yoghurt, bell peppers, broccoli", HowToUse = "Vitamin C from citrus supports immune cells. Garlic activates white blood cells.", FoodsToAvoid = "Sugar (suppresses white blood cells for hours), alcohol", IconEmoji = "🛡️" }
        );
    }

    private static async Task SeedFoodTimingAsync(ApplicationDbContext db)
    {
        if (await db.FoodTimingGuides.AnyAsync()) return;

        db.FoodTimingGuides.AddRange(
            new FoodTimingGuide { TimingType = MealTimingType.WakeUp,          TimeRange = "Before breakfast (6–7AM)", BestFoodsJson = "[\"Warm lemon water\",\"Apple cider vinegar\",\"Plain water\"]", FoodsToAvoidJson = "[\"Coffee on empty stomach\",\"Sugary drinks\"]", Reason = "Kickstarts digestion, rehydrates after sleep, gentle on stomach." },
            new FoodTimingGuide { TimingType = MealTimingType.Breakfast,        TimeRange = "7AM–9AM", BestFoodsJson = "[\"Eggs\",\"Oats\",\"Fruits\",\"Yoghurt\",\"Whole grain toast\",\"Avocado\"]", FoodsToAvoidJson = "[\"Sugary cereals\",\"Pastries\",\"Processed juice\"]", Reason = "Fuel metabolism for the day. Stable blood sugar = steady energy." },
            new FoodTimingGuide { TimingType = MealTimingType.MidMorning,       TimeRange = "10AM–11AM", BestFoodsJson = "[\"Handful of nuts\",\"Fruit\",\"Greek yoghurt\",\"Hard-boiled egg\"]", FoodsToAvoidJson = "[\"Crisps\",\"Chocolate bars\",\"Biscuits\"]", Reason = "Prevents energy crash before lunch without overeating." },
            new FoodTimingGuide { TimingType = MealTimingType.Lunch,            TimeRange = "12PM–2PM", BestFoodsJson = "[\"Complex carbs + protein\",\"Rice + chicken\",\"Pasta + vegetables\",\"Legumes\"]", FoodsToAvoidJson = "[\"Heavy fried food\",\"Alcohol\"]", Reason = "Peak digestive capacity. Largest meal of the day is ideal here." },
            new FoodTimingGuide { TimingType = MealTimingType.AfternoonSnack,   TimeRange = "3PM–4PM", BestFoodsJson = "[\"Hummus + vegetables\",\"Banana\",\"Small handful of almonds\",\"Apple\"]", FoodsToAvoidJson = "[\"Energy drinks\",\"Sugary snacks\"]", Reason = "Prevents overeating at dinner. Maintains blood sugar." },
            new FoodTimingGuide { TimingType = MealTimingType.Dinner,           TimeRange = "6PM–8PM", BestFoodsJson = "[\"Light protein + vegetables\",\"Soup\",\"Salad + fish\",\"Lentils\"]", FoodsToAvoidJson = "[\"Heavy carbs after 7PM\",\"Fried food\",\"Alcohol before bed\"]", Reason = "Lower metabolic rate in evening. Light meals digest better and improve sleep." },
            new FoodTimingGuide { TimingType = MealTimingType.BeforeBed,        TimeRange = "After 9PM (if hungry)", BestFoodsJson = "[\"Warm milk\",\"Tart cherry juice\",\"Small banana\",\"Chamomile tea\"]", FoodsToAvoidJson = "[\"Heavy meals\",\"Spicy food\",\"Caffeine\",\"Alcohol\",\"Sugary snacks\"]", Reason = "Support sleep quality. Tart cherries contain melatonin." },
            new FoodTimingGuide { TimingType = MealTimingType.PreWorkout,       TimeRange = "1–2 hours before", BestFoodsJson = "[\"Banana + peanut butter\",\"Oats + honey\",\"Rice cakes\",\"Toast + eggs\"]", FoodsToAvoidJson = "[\"High fat meals\",\"Dairy-heavy meals\",\"High fibre (causes cramps)\"]", Reason = "Carbs provide quick energy. Avoid anything that causes digestive discomfort during exercise." },
            new FoodTimingGuide { TimingType = MealTimingType.PostWorkout,      TimeRange = "Within 45 minutes", BestFoodsJson = "[\"Chicken + rice\",\"Protein shake + banana\",\"Greek yoghurt + fruit\",\"Eggs + wholegrain\"]", FoodsToAvoidJson = "[\"High sugar processed foods\",\"Skipping this meal\"]", Reason = "Protein repairs muscle. Carbs replenish glycogen stores. This window is critical for recovery." }
        );
    }

    private static async Task SeedBadgesAsync(ApplicationDbContext db)
    {
        if (await db.Badges.AnyAsync()) return;

        db.Badges.AddRange(
            new Badge { Name = "First Cook",      Description = "Logged your first cook",       IconUrl = "🍳", Category = "Cooking",   TriggerCondition = "CookLogCount >= 1" },
            new Badge { Name = "World Explorer",  Description = "Cooked from 10 countries",     IconUrl = "🌍", Category = "Discovery", TriggerCondition = "UniqueCountriesCooked >= 10" },
            new Badge { Name = "Pasta Master",    Description = "Cooked 5 Italian recipes",     IconUrl = "🍝", Category = "Culture",   TriggerCondition = "ItalianRecipesCooked >= 5" },
            new Badge { Name = "Spice Lover",     Description = "Cooked 10 spicy dishes",       IconUrl = "🌶️", Category = "Cooking",   TriggerCondition = "SpicyRecipesCooked >= 10" },
            new Badge { Name = "Health Hero",     Description = "Logged 30 healthy meals",      IconUrl = "💚", Category = "Health",    TriggerCondition = "HealthyMealsLogged >= 30" },
            new Badge { Name = "Community Star",  Description = "Received 50 likes on your recipes", IconUrl = "⭐", Category = "Social", TriggerCondition = "RecipeLikesReceived >= 50" },
            new Badge { Name = "Streak Master",   Description = "7-day cooking streak",         IconUrl = "🔥", Category = "Cooking",   TriggerCondition = "CookStreak >= 7" },
            new Badge { Name = "Jokester",        Description = "Liked 20 cooking jokes",       IconUrl = "😂", Category = "Fun",       TriggerCondition = "JokesLiked >= 20" },
            new Badge { Name = "Pet Chef",        Description = "Added a pet and their meals",  IconUrl = "🐾", Category = "Pets",      TriggerCondition = "HasPetProfile == true" },
            new Badge { Name = "Contributor",     Description = "Had a recipe approved",        IconUrl = "✅", Category = "Community", TriggerCondition = "ApprovedSuggestions >= 1" },
            new Badge { Name = "Quiz Champion",   Description = "Passed 5 quizzes",             IconUrl = "🏆", Category = "Skills",    TriggerCondition = "PassedQuizzes >= 5" },
            new Badge { Name = "Grand Explorer",  Description = "Recipes from all 6 continents",IconUrl = "🗺️", Category = "Discovery", TriggerCondition = "ContinentsCooked >= 6" }
        );
    }

    private static async Task SeedScrapeJobsAsync(ApplicationDbContext db)
    {
        if (await db.ScrapeJobs.AnyAsync()) return;

        var jobs = new[]
        {
            ("AllRecipes",      "https://www.allrecipes.com/recipes/",         "Website"),
            ("BBC Good Food",   "https://www.bbcgoodfood.com/recipes",          "Website"),
            ("Serious Eats",    "https://www.seriouseats.com/recipes",           "Website"),
            ("196 Flavors",     "https://www.196flavors.com/",                   "Website"),
            ("TasteAtlas",      "https://www.tasteatlas.com/",                   "Website"),
            ("Reddit Recipes",  "https://reddit.com/r/recipes",                  "Reddit"),
            ("Reddit Cooking",  "https://reddit.com/r/Cooking",                  "Reddit"),
            ("Reddit WorldFood","https://reddit.com/r/worldcuisine",             "Reddit"),
            ("Healthline Nutrition", "https://www.healthline.com/nutrition",      "Health"),
            ("Medical News Today",   "https://www.medicalnewstoday.com/categories/food-nutrition", "Health"),
            ("NutritionFacts",       "https://nutritionfacts.org/topics/",        "Health"),
            ("Joke Source 1",   "https://www.punpedia.com/food-puns/",            "Jokes"),
            ("Joke Source 2",   "https://jokes4us.com/foodjokes/",               "Jokes"),
        };

        foreach (var (source, url, platform) in jobs)
        {
            db.ScrapeJobs.Add(new ScrapeJob
            {
                Source    = source,
                Url       = url,
                Platform  = platform,
                Status    = ScrapeStatus.Pending,
                IsActive  = true,
                LastRunAt = DateTime.UtcNow,
                NextRunAt = DateTime.UtcNow
            });
        }
    }

    private static async Task SeedJokesAsync(ApplicationDbContext db)
    {
        if (await db.CookingJokes.AnyAsync()) return;

        var jokes = new[]
        {
            ("Why did the chef get arrested? He was caught beating an egg! 🥚", "Chef Jokes"),
            ("What do you call a fake noodle? An impasta! 🍝", "Food Puns"),
            ("Why don't eggs tell jokes? They'd crack each other up! 🐣", "Food Puns"),
            ("What's a chef's favourite music? Heavy metal — they love beating eggs! 🥁", "Chef Jokes"),
            ("Why did the banana go to the doctor? It wasn't peeling well. 🍌", "Fruit Jokes"),
            ("I told my wife she should embrace her mistakes. She gave me a hug. Then made me a sandwich wrong. 🥪", "Kitchen Jokes"),
            ("What did the ocean say to the chef? Nothing, it just waved. 🌊", "Food Puns"),
            ("Why do chefs always carry a pen? In case they want to write a recipe! ✍️", "Chef Jokes"),
            ("What do you call cheese that isn't yours? Nacho cheese! 🧀", "Food Puns"),
            ("I'm on a seafood diet. I see food, and I eat it. 🦐", "Diet Jokes"),
        };

        foreach (var (body, cat) in jokes)
        {
            db.CookingJokes.Add(new CookingJoke
            {
                Body       = body,
                Category   = cat,
                Source     = "Seed",
                IsApproved = true,
                CreatedAt  = DateTime.UtcNow
            });
        }
    }

    private static async Task SeedRecipesAsync(ApplicationDbContext db)
    {
        if (await db.Recipes.AnyAsync()) return;

        var recipes = new List<Recipe>
        {
            new() {
                Title = "Nigerian Jollof Rice",
                Slug = "nigerian-jollof-rice",
                Description = "A vibrant and flavorful one-pot rice dish that is a staple in West Africa.",
                CulturalStory = "Jollof rice is more than just food in Nigeria; it's a centerpiece of celebrations and the subject of friendly 'Jollof wars' between West African nations.",
                OriginCountry = "Nigeria",
                OriginCountryCode = "NG",
                OriginContinent = "Africa",
                MealType = MealType.Dinner,
                DifficultyLevel = DifficultyLevel.Intermediate,
                PrepTimeMinutes = 15,
                CookTimeMinutes = 45,
                IsPublished = true,
                IsApproved = true,
                IsFeatured = true,
                CreatedAt = DateTime.UtcNow
            },
            new() {
                Title = "Japanese Ramen (Shoyu Style)",
                Slug = "japanese-ramen-shoyu",
                Description = "Rich and savory soy sauce-based noodle soup with various toppings.",
                CulturalStory = "Ramen has evolved from a simple street food to a culinary art form in Japan, with countless regional variations.",
                OriginCountry = "Japan",
                OriginCountryCode = "JP",
                OriginContinent = "Asia",
                MealType = MealType.Dinner,
                DifficultyLevel = DifficultyLevel.Advanced,
                PrepTimeMinutes = 30,
                CookTimeMinutes = 120,
                IsPublished = true,
                IsApproved = true,
                IsFeatured = true,
                CreatedAt = DateTime.UtcNow
            },
            new() {
                Title = "Classic Italian Margherita Pizza",
                Slug = "classic-margherita-pizza",
                Description = "The iconic pizza with tomato sauce, fresh mozzarella, and basil.",
                CulturalStory = "Legend has it that the Margherita pizza was created in 1889 to honor the Queen of Italy, Margherita of Savoy.",
                OriginCountry = "Italy",
                OriginCountryCode = "IT",
                OriginContinent = "Europe",
                MealType = MealType.Dinner,
                DifficultyLevel = DifficultyLevel.Intermediate,
                PrepTimeMinutes = 20,
                CookTimeMinutes = 15,
                IsPublished = true,
                IsApproved = true,
                CreatedAt = DateTime.UtcNow
            },
            new() {
                Title = "Mexican Street Tacos",
                Slug = "mexican-street-tacos",
                Description = "Simple yet delicious corn tortillas filled with grilled meat, onions, and cilantro.",
                CulturalStory = "Tacos are the soul of Mexican street food culture, bringing people together at all hours of the day.",
                OriginCountry = "Mexico",
                OriginCountryCode = "MX",
                OriginContinent = "Americas",
                MealType = MealType.Dinner,
                DifficultyLevel = DifficultyLevel.Easy,
                PrepTimeMinutes = 20,
                CookTimeMinutes = 10,
                IsPublished = true,
                IsApproved = true,
                CreatedAt = DateTime.UtcNow
            },

            // ── Africa (additional) ─────────────────────────────
            new() {
                Title = "Ethiopian Injera with Doro Wat",
                Slug = "ethiopian-injera-doro-wat",
                Description = "Spongy sourdough flatbread served with a deeply spiced slow-cooked chicken stew.",
                CulturalStory = "Injera is not just a food in Ethiopia — it's a communal plate. Eating from the same injera symbolises unity and love among Ethiopians.",
                OriginCountry = "Ethiopia",
                OriginCountryCode = "ET",
                OriginContinent = "Africa",
                MealType = MealType.Dinner,
                DifficultyLevel = DifficultyLevel.Advanced,
                PrepTimeMinutes = 60,
                CookTimeMinutes = 90,
                IsPublished = true,
                IsApproved = true,
                IsFeatured = true,
                CreatedAt = DateTime.UtcNow
            },
            new() {
                Title = "South African Bobotie",
                Slug = "south-african-bobotie",
                Description = "A fragrant Cape Malay dish of spiced minced meat baked with a savoury egg custard topping.",
                CulturalStory = "Bobotie is South Africa's national dish — a beautiful blend of Dutch, Malay, and indigenous culinary traditions brought together at the Cape.",
                OriginCountry = "South Africa",
                OriginCountryCode = "ZA",
                OriginContinent = "Africa",
                MealType = MealType.Dinner,
                DifficultyLevel = DifficultyLevel.Intermediate,
                PrepTimeMinutes = 20,
                CookTimeMinutes = 50,
                IsPublished = true,
                IsApproved = true,
                CreatedAt = DateTime.UtcNow
            },
            new() {
                Title = "Ugandan Groundnut Stew (Katogo)",
                Slug = "ugandan-groundnut-stew-katogo",
                Description = "A rich peanut-based stew cooked with plantain or chicken — a Ugandan household staple.",
                CulturalStory = "Katogo is Uganda's beloved breakfast and dinner dish — an aromatic groundnut-based stew that has been feeding families for generations.",
                OriginCountry = "Uganda",
                OriginCountryCode = "UG",
                OriginContinent = "Africa",
                MealType = MealType.Dinner,
                DifficultyLevel = DifficultyLevel.Beginner,
                PrepTimeMinutes = 15,
                CookTimeMinutes = 35,
                IsPublished = true,
                IsApproved = true,
                IsFeatured = true,
                CreatedAt = DateTime.UtcNow
            },
            new() {
                Title = "Moroccan Chicken Tagine",
                Slug = "moroccan-chicken-tagine",
                Description = "Slow-braised chicken with preserved lemons, olives, and aromatic Moroccan spices.",
                CulturalStory = "The tagine — both the cooking vessel and the dish — is the heart of Moroccan hospitality. Every family has their own cherished recipe passed down through generations.",
                OriginCountry = "Morocco",
                OriginCountryCode = "MA",
                OriginContinent = "Africa",
                MealType = MealType.Dinner,
                DifficultyLevel = DifficultyLevel.Intermediate,
                PrepTimeMinutes = 25,
                CookTimeMinutes = 75,
                IsPublished = true,
                IsApproved = true,
                CreatedAt = DateTime.UtcNow
            },
            new() {
                Title = "Ghanaian Waakye",
                Slug = "ghanaian-waakye",
                Description = "A popular Ghanaian street food of rice and black-eyed beans cooked together with dried sorghum leaves for a deep red colour.",
                CulturalStory = "Waakye originated from Northern Ghana and spread across the country as a beloved street food. The sorghum leaves give it its distinctive red-purple colour and earthy flavour.",
                OriginCountry = "Ghana",
                OriginCountryCode = "GH",
                OriginContinent = "Africa",
                MealType = MealType.Lunch,
                DifficultyLevel = DifficultyLevel.Easy,
                PrepTimeMinutes = 10,
                CookTimeMinutes = 40,
                IsPublished = true,
                IsApproved = true,
                CreatedAt = DateTime.UtcNow
            },

            // ── Asia (additional) ────────────────────────────────
            new() {
                Title = "Indian Chicken Biryani",
                Slug = "indian-chicken-biryani",
                Description = "Fragrant basmati rice layered with spiced chicken, caramelised onions, and saffron.",
                CulturalStory = "Biryani traces its roots to the royal kitchens of the Mughal Empire. Today it is India's most loved dish, with over 26 distinct regional variations.",
                OriginCountry = "India",
                OriginCountryCode = "IN",
                OriginContinent = "Asia",
                MealType = MealType.Dinner,
                DifficultyLevel = DifficultyLevel.Advanced,
                PrepTimeMinutes = 40,
                CookTimeMinutes = 60,
                IsPublished = true,
                IsApproved = true,
                IsFeatured = true,
                CreatedAt = DateTime.UtcNow
            },
            new() {
                Title = "Thai Pad Thai",
                Slug = "thai-pad-thai",
                Description = "Stir-fried rice noodles with tofu or shrimp, egg, bean sprouts, and tangy tamarind-based sauce.",
                CulturalStory = "Pad Thai was popularised in the 1930s by the Thai government as part of a national identity campaign. It quickly became Thailand's most iconic street food.",
                OriginCountry = "Thailand",
                OriginCountryCode = "TH",
                OriginContinent = "Asia",
                MealType = MealType.Dinner,
                DifficultyLevel = DifficultyLevel.Intermediate,
                PrepTimeMinutes = 20,
                CookTimeMinutes = 15,
                IsPublished = true,
                IsApproved = true,
                IsFeatured = true,
                CreatedAt = DateTime.UtcNow
            },
            new() {
                Title = "Vietnamese Pho Bo",
                Slug = "vietnamese-pho-bo",
                Description = "Deeply aromatic beef noodle soup simmered for hours with star anise, cinnamon, and charred ginger.",
                CulturalStory = "Pho is Vietnam's national comfort food. Originating in northern Vietnam in the early 20th century, a true pho broth takes 12 hours to perfect.",
                OriginCountry = "Vietnam",
                OriginCountryCode = "VN",
                OriginContinent = "Asia",
                MealType = MealType.Breakfast,
                DifficultyLevel = DifficultyLevel.Advanced,
                PrepTimeMinutes = 30,
                CookTimeMinutes = 180,
                IsPublished = true,
                IsApproved = true,
                CreatedAt = DateTime.UtcNow
            },
            new() {
                Title = "Korean Bibimbap",
                Slug = "korean-bibimbap",
                Description = "A vibrant rice bowl topped with seasoned vegetables, a fried egg, and spicy gochujang paste.",
                CulturalStory = "Bibimbap literally means 'mixed rice'. Historically made from leftover vegetables offered at court, today it is a beloved symbol of Korean cuisine.",
                OriginCountry = "South Korea",
                OriginCountryCode = "KR",
                OriginContinent = "Asia",
                MealType = MealType.Lunch,
                DifficultyLevel = DifficultyLevel.Easy,
                PrepTimeMinutes = 30,
                CookTimeMinutes = 20,
                IsPublished = true,
                IsApproved = true,
                CreatedAt = DateTime.UtcNow
            },
            new() {
                Title = "Malaysian Nasi Lemak",
                Slug = "malaysian-nasi-lemak",
                Description = "Fragrant coconut rice served with crispy anchovies, peanuts, boiled egg, cucumber, and fiery sambal.",
                CulturalStory = "Nasi Lemak is Malaysia's national dish. The name means 'rich rice' — a reference to the creamy coconut milk it's cooked in. It's eaten morning, noon, and night.",
                OriginCountry = "Malaysia",
                OriginCountryCode = "MY",
                OriginContinent = "Asia",
                MealType = MealType.Breakfast,
                DifficultyLevel = DifficultyLevel.Intermediate,
                PrepTimeMinutes = 20,
                CookTimeMinutes = 30,
                IsPublished = true,
                IsApproved = true,
                CreatedAt = DateTime.UtcNow
            },
            new() {
                Title = "Indonesian Beef Rendang",
                Slug = "indonesian-beef-rendang",
                Description = "Meltingly tender slow-cooked beef in a rich dry coconut and chilli curry — voted the world's best food.",
                CulturalStory = "Rendang originated with the Minangkabau people of West Sumatra. It was traditionally cooked for feasts and as a preservation method — the longer it cooks, the drier it becomes, the longer it keeps.",
                OriginCountry = "Indonesia",
                OriginCountryCode = "ID",
                OriginContinent = "Asia",
                MealType = MealType.Dinner,
                DifficultyLevel = DifficultyLevel.Advanced,
                PrepTimeMinutes = 30,
                CookTimeMinutes = 240,
                IsPublished = true,
                IsApproved = true,
                IsFeatured = true,
                CreatedAt = DateTime.UtcNow
            },

            // ── Europe (additional) ──────────────────────────────
            new() {
                Title = "French Ratatouille",
                Slug = "french-ratatouille",
                Description = "A beautiful Provençal vegetable stew of courgette, aubergine, tomatoes, and peppers in olive oil and herbs.",
                CulturalStory = "Ratatouille is the humble peasant dish from Nice that became a global icon. Once simple farm cooking, it now appears on fine dining menus worldwide.",
                OriginCountry = "France",
                OriginCountryCode = "FR",
                OriginContinent = "Europe",
                MealType = MealType.Dinner,
                DifficultyLevel = DifficultyLevel.Easy,
                PrepTimeMinutes = 20,
                CookTimeMinutes = 45,
                IsPublished = true,
                IsApproved = true,
                CreatedAt = DateTime.UtcNow
            },
            new() {
                Title = "Spanish Chicken Paella",
                Slug = "spanish-chicken-paella",
                Description = "Saffron-infused Spanish rice cooked with chicken, chorizo, and vegetables in a wide shallow pan.",
                CulturalStory = "Paella originated in the rice fields of Valencia in the 19th century. Farmers cooked it over an open fire using rabbit, snails, and green beans.",
                OriginCountry = "Spain",
                OriginCountryCode = "ES",
                OriginContinent = "Europe",
                MealType = MealType.Dinner,
                DifficultyLevel = DifficultyLevel.Advanced,
                PrepTimeMinutes = 30,
                CookTimeMinutes = 40,
                IsPublished = true,
                IsApproved = true,
                IsFeatured = true,
                CreatedAt = DateTime.UtcNow
            },
            new() {
                Title = "Polish Pierogi",
                Slug = "polish-pierogi-potato-cheese",
                Description = "Soft boiled dumplings filled with creamy potato and farmer's cheese, served with fried onions and sour cream.",
                CulturalStory = "Pierogi are Poland's national dish. Making them together is a cherished cultural tradition — every family has their own beloved recipe.",
                OriginCountry = "Poland",
                OriginCountryCode = "PL",
                OriginContinent = "Europe",
                MealType = MealType.Dinner,
                DifficultyLevel = DifficultyLevel.Intermediate,
                PrepTimeMinutes = 60,
                CookTimeMinutes = 20,
                IsPublished = true,
                IsApproved = true,
                CreatedAt = DateTime.UtcNow
            },

            // ── Middle East ──────────────────────────────────────
            new() {
                Title = "Lebanese Hummus from Scratch",
                Slug = "lebanese-hummus-from-scratch",
                Description = "Ultra-smooth and creamy chickpea dip with lemon, garlic, and tahini — the authentic Lebanese way.",
                CulturalStory = "Hummus has been eaten across the Levant for centuries. The debate over who makes it best — Lebanon, Israel, Palestine — is as heated as any in food history!",
                OriginCountry = "Lebanon",
                OriginCountryCode = "LB",
                OriginContinent = "Middle East",
                MealType = MealType.Snack,
                DifficultyLevel = DifficultyLevel.Easy,
                PrepTimeMinutes = 20,
                CookTimeMinutes = 0,
                IsPublished = true,
                IsApproved = true,
                IsFeatured = true,
                CreatedAt = DateTime.UtcNow
            },
            new() {
                Title = "Shakshuka",
                Slug = "shakshuka",
                Description = "Eggs poached in a spiced tomato and pepper sauce, finished with feta and fresh herbs.",
                CulturalStory = "Shakshuka is popular across North Africa and the Middle East. The word means 'all mixed up' in Arabic — reflecting its beautifully chaotic nature.",
                OriginCountry = "Israel",
                OriginCountryCode = "IL",
                OriginContinent = "Middle East",
                MealType = MealType.Breakfast,
                DifficultyLevel = DifficultyLevel.Easy,
                PrepTimeMinutes = 10,
                CookTimeMinutes = 20,
                IsPublished = true,
                IsApproved = true,
                IsFeatured = true,
                CreatedAt = DateTime.UtcNow
            },
            new() {
                Title = "Persian Lamb Koobideh Kebab",
                Slug = "persian-lamb-koobideh",
                Description = "Juicy minced lamb and beef kebabs seasoned with onion and saffron, grilled and served with saffron rice.",
                CulturalStory = "Koobideh is the king of Persian barbecue. Cooking it is an art — the thin kofta must cling to the flat skewer without falling off. A national pride.",
                OriginCountry = "Iran",
                OriginCountryCode = "IR",
                OriginContinent = "Middle East",
                MealType = MealType.Dinner,
                DifficultyLevel = DifficultyLevel.Intermediate,
                PrepTimeMinutes = 30,
                CookTimeMinutes = 20,
                IsPublished = true,
                IsApproved = true,
                CreatedAt = DateTime.UtcNow
            },

            // ── Americas (additional) ────────────────────────────
            new() {
                Title = "Brazilian Feijoada",
                Slug = "brazilian-feijoada",
                Description = "Brazil's national dish — a hearty slow-cooked black bean and smoked pork stew with rice and farofa.",
                CulturalStory = "Feijoada has a complex history tied to colonial Brazil and African enslaved people who created it from leftover cuts of pork. Today it unifies all of Brazil.",
                OriginCountry = "Brazil",
                OriginCountryCode = "BR",
                OriginContinent = "Americas",
                MealType = MealType.Dinner,
                DifficultyLevel = DifficultyLevel.Advanced,
                PrepTimeMinutes = 30,
                CookTimeMinutes = 180,
                IsPublished = true,
                IsApproved = true,
                IsFeatured = true,
                CreatedAt = DateTime.UtcNow
            },
            new() {
                Title = "Peruvian Ceviche",
                Slug = "peruvian-ceviche",
                Description = "Fresh fish 'cooked' in lime juice with red onion, aji amarillo chilli, and coriander — Peru's iconic dish.",
                CulturalStory = "Ceviche is Peru's national dish and a UNESCO cultural heritage. The acid 'cooks' the fish without heat, preserving its delicate, silky texture.",
                OriginCountry = "Peru",
                OriginCountryCode = "PE",
                OriginContinent = "Americas",
                MealType = MealType.Lunch,
                DifficultyLevel = DifficultyLevel.Easy,
                PrepTimeMinutes = 20,
                CookTimeMinutes = 0,
                IsPublished = true,
                IsApproved = true,
                CreatedAt = DateTime.UtcNow
            },
            new() {
                Title = "Jamaican Jerk Chicken",
                Slug = "jamaican-jerk-chicken",
                Description = "Deeply marinated chicken in Scotch bonnet, allspice, and thyme, slow-grilled over pimento wood.",
                CulturalStory = "Jerk cooking originates from the Maroons — formerly enslaved Africans who escaped into Jamaica's Blue Mountains, where they perfected smoking wild boar over pimento wood.",
                OriginCountry = "Jamaica",
                OriginCountryCode = "JM",
                OriginContinent = "Americas",
                MealType = MealType.Dinner,
                DifficultyLevel = DifficultyLevel.Intermediate,
                PrepTimeMinutes = 30,
                CookTimeMinutes = 60,
                IsPublished = true,
                IsApproved = true,
                IsFeatured = true,
                CreatedAt = DateTime.UtcNow
            },

            // ── Oceania ──────────────────────────────────────────
            new() {
                Title = "Australian Pavlova",
                Slug = "australian-pavlova",
                Description = "A crisp meringue shell with a soft marshmallow centre, topped with whipped cream and fresh tropical fruits.",
                CulturalStory = "The Pavlova was created to honour the Russian ballerina Anna Pavlova during her 1920s tours. Both Australia and New Zealand fiercely claim ownership of this dessert!",
                OriginCountry = "Australia",
                OriginCountryCode = "AU",
                OriginContinent = "Oceania",
                MealType = MealType.Dessert,
                DifficultyLevel = DifficultyLevel.Intermediate,
                PrepTimeMinutes = 20,
                CookTimeMinutes = 90,
                IsPublished = true,
                IsApproved = true,
                CreatedAt = DateTime.UtcNow
            },
        };

        db.Recipes.AddRange(recipes);
        await db.SaveChangesAsync();
    }

    private static async Task SeedQuizQuestionsAsync(ApplicationDbContext db)
    {
        if (await db.QuizQuestions.AnyAsync()) return;

        db.QuizQuestions.AddRange(
            new QuizQuestion {
                QuestionText = "Which nutrient is primarily responsible for building and repairing tissues in the body?",
                AnswersJson = "[\"Carbohydrates\", \"Fats\", \"Protein\", \"Vitamin C\"]",
                CorrectAnswer = "Protein",
                Difficulty = DifficultyLevel.Easy,
                MinLevel = ChefLevel.Level1_KitchenNewcomer,
                NutrientCategory = NutrientCategory.Protein
            },
            new QuizQuestion {
                QuestionText = "Which of these is a rich source of Vitamin C?",
                AnswersJson = "[\"Beef\", \"Eggs\", \"Bell Pepper\", \"Milk\"]",
                CorrectAnswer = "Bell Pepper",
                Difficulty = DifficultyLevel.Easy,
                MinLevel = ChefLevel.Level1_KitchenNewcomer,
                NutrientCategory = NutrientCategory.VitaminC
            },
            new QuizQuestion {
                QuestionText = "In which country did Margherita Pizza originate?",
                AnswersJson = "[\"France\", \"Spain\", \"Italy\", \"Greece\"]",
                CorrectAnswer = "Italy",
                Difficulty = DifficultyLevel.Easy,
                MinLevel = ChefLevel.Level1_KitchenNewcomer,
                CultureTag = "Italian"
            }
        );
        await db.SaveChangesAsync();
    }

    private static async Task SeedDietPlansAsync(ApplicationDbContext db)
    {
        if (await db.DietPlans.AnyAsync()) return;

        db.DietPlans.AddRange(
            new DietPlan {
                PlanName = "Vegan Discovery",
                Description = "A vibrant plant-based journey exploring global flavors without any animal products.",
                TargetCalories = 1800,
                HealthBenefits = "Improved heart health, weight management, and environmental impact.",
                MealsJson = "[\"Tofu & Broccoli Stir-fry\",\"Red Lentil Dahl\",\"Quinoa Salad with Roasted Chickpeas\"]"
            },
            new DietPlan {
                PlanName = "Low Carb / Keto",
                Description = "Focus on healthy fats and protein while minimizing sugars and starches.",
                TargetCalories = 2000,
                HealthBenefits = "Stable blood sugar, increased focus, and efficient fat burning.",
                MealsJson = "[\"Grilled Salmon with Asparagus\",\"Chicken Caesar Salad (no croutons)\",\"Avocado & Egg Breakfast Bowl\"]"
            },
            new DietPlan {
                PlanName = "Diabetic Friendly",
                Description = "Balanced meals with slow-releasing carbohydrates to maintain steady glucose levels.",
                TargetCalories = 1900,
                HealthBenefits = "Glucose stability and long-term metabolic health.",
                MealsJson = "[\"Steel-cut Oats with Berries\",\"Baked Cod with Steamed Greens\",\"Turkey & Lentil Soup\"]"
            }
        );
    }

    private static async Task SeedEquipmentAsync(ApplicationDbContext db)
    {
        if (await db.CookingEquipment.AnyAsync()) return;

        db.CookingEquipment.AddRange(
            new CookingEquipment {
                Name = "Chef's Knife",
                Category = "Cutlery",
                Description = "The most versatile tool in the kitchen for chopping, slicing, and dicing.",
                CleaningSteps = "Hand wash with warm soapy water immediately after use. Never put in dishwasher.",
                MaintenanceTips = "Hone regularly with a steel and sharpen every 6-12 months."
            },
            new CookingEquipment {
                Name = "Cast Iron Skillet",
                Category = "Cookware",
                Description = "Excellent heat retention for searing and even cooking.",
                CleaningSteps = "Rinse with hot water and a brush. Dry completely over heat.",
                MaintenanceTips = "Apply a thin layer of oil after each use to maintain the 'seasoning'."
            },
            new CookingEquipment {
                Name = "High-Speed Blender",
                Category = "Appliance",
                Description = "Perfect for smoothies, soups, and sauces.",
                CleaningSteps = "Fill halfway with warm water and a drop of soap, run for 30 seconds, then rinse.",
                MaintenanceTips = "Check blade sharpness and motor base for spills regularly."
            }
        );
    }
}
