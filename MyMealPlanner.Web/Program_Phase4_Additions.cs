// ── ADD THESE REGISTRATIONS to Program.cs after existing service registrations ──

// ── Phase 4 Service Registrations (add to existing Program.cs) ──
//
// builder.Services.AddScoped<IAIChatAssistantService, AIChatAssistantService>();
// builder.Services.AddScoped<IAITaggerService,        AITaggerService>();
// builder.Services.AddScoped<IImageSearchService,     ImageSearchService>();
// builder.Services.AddScoped<ITranslationService,     LibreTranslationService>();
// builder.Services.AddScoped<IIngredientCostService,  IngredientCostService>();
// builder.Services.AddScoped<PdfExportService>();
// builder.Services.AddScoped<NearbyFoodService>();
//
// ── Add Mia hub to SignalR endpoint mapping ──
// app.MapHub<MiaHub>("/hubs/mia");
//
// ── Add to appsettings.json ──
// "AI": {
//   "GroqApiKey":       "YOUR_GROQ_API_KEY",        // free at console.groq.com
//   "OllamaBaseUrl":    "http://localhost:11434",    // free self-hosted
//   "OllamaModel":      "llama3",
//   "OpenRouterApiKey": "YOUR_OPENROUTER_KEY",       // cheap multi-model
//   "ClaudeApiKey":     "YOUR_ANTHROPIC_KEY"         // most capable
// },
// "GoogleVision": {
//   "ApiKey": "YOUR_GOOGLE_VISION_KEY"   // free: 1000 units/month
// },
// "LibreTranslate": {
//   "BaseUrl": "http://localhost:5000",  // docker run -p 5000:5000 libretranslate/libretranslate
//   "ApiKey":  ""                        // optional for self-hosted
// }
//
// ── Add to _Layout.cshtml before </body> ──
// <partial name="_MiaChat" />
//
// ── Hangfire recurring jobs to add ──
// RecurringJob.AddOrUpdate<INotificationService>(
//     "daily-meal-suggestions",
//     svc => svc.ScheduleDailyMealSuggestionsAsync(),
//     "0 8 * * *",    // every day at 8am
//     new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc, QueueName = "notifications" });
//
// RecurringJob.AddOrUpdate<INotificationService>(
//     "weekly-meal-plans",
//     svc => svc.ScheduleWeeklyMealPlansAsync(),
//     "0 7 * * 1",    // every Monday at 7am
//     new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc, QueueName = "notifications" });
//
// RecurringJob.AddOrUpdate<INotificationService>(
//     "re-engagement",
//     svc => svc.SendReEngagementAsync(),
//     "0 18 * * *",   // every day at 6pm
//     new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc, QueueName = "notifications" });
//
// RecurringJob.AddOrUpdate<IJokeService>(
//     "scrape-jokes",
//     svc => svc.ScrapeNewJokesAsync(),
//     "0 6 * * *",    // every day at 6am
//     new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc, QueueName = "default" });
//
// RecurringJob.AddOrUpdate<IJokeService>(
//     "generate-ai-jokes",
//     svc => svc.GenerateAIJokesAsync(5),
//     "0 2 * * *",    // every day at 2am
//     new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc, QueueName = "default" });

// This file is a reference patch — apply these changes to Program.cs
