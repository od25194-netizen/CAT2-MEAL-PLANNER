# 🍽️ My Meal Planner

> A culturally intelligent, AI-assisted, globally connected meal discovery and planning platform.
> Built entirely in **ASP.NET Core 8 MVC + C#**.

---

## 🚀 Quick Start

```bash
# 1. Clone and configure
git clone https://github.com/yourorg/mymealplanner.git
cd mymealplanner
cp .env.example .env   # fill in your API keys

# 2. Database setup
dotnet ef migrations add InitialCreate --project MyMealPlanner.Infrastructure --startup-project MyMealPlanner.Web
dotnet ef database update --project MyMealPlanner.Infrastructure --startup-project MyMealPlanner.Web

# 3. Run
dotnet run --project MyMealPlanner.Web
# Opens at https://localhost:7042

# OR with Docker (full stack — app + SQL Server + Redis + LibreTranslate)
docker compose up -d
# Opens at http://localhost:8080
```

**Default admin:** `admin@mymealplanner.app` / `Admin@MyMealPlanner2024!`

---

## 🔑 Minimum Required Keys

| Key | Where | Cost |
|---|---|---|
| `AI__GROQAPIKEY` | [console.groq.com](https://console.groq.com) | **FREE** |
| `YOUTUBE__APIKEY` | Google Cloud Console → YouTube Data API v3 | Free 10k/day |
| `AUTH__GOOGLE__CLIENTID/SECRET` | Google Cloud Console → OAuth 2.0 | Free |

Everything else is optional and degrades gracefully.

---

## 🏗️ Project Structure

```
MyMealPlanner/
├── MyMealPlanner.Core/           Models, interfaces, DTOs, enums
├── MyMealPlanner.Infrastructure/ EF Core, migrations, seeder
├── MyMealPlanner.Services/       16 service implementations
├── MyMealPlanner.Web/            Controllers, views, hubs, assets
│   ├── Controllers/   14 controllers
│   ├── Hubs/          4 SignalR hubs (Recipe, Chat, Notifications, Mia)
│   ├── Views/         50+ Razor views
│   └── wwwroot/       Bootstrap 5, PWA manifest, service worker
└── MyMealPlanner.Tests/          Unit + integration tests
```

---

## ✨ Features

| Area | What's Built |
|---|---|
| **Discovery** | Text / voice / image search · Local→Country→World toggle · 30+ scraped sources daily |
| **AI** | "Mia" chat assistant (Groq/Ollama/Claude) · Auto-tagger · Image food ID |
| **Rankings** | Global / Continent / Country / Weekly — recalculated every 3 hours |
| **Health Hub** | Nutrient navigator · Allergy guide · Food as medicine · When to eat · Age plans · Pets |
| **Meal Planning** | Drag-drop weekly grid · Auto-generation · Shopping list · WhatsApp share · PDF export |
| **Social** | Follow · Chat (SignalR) · Comments (emoji+images) · Community rooms · Live sessions |
| **Chef Levels** | 8 levels · Personalised quizzes · White⚪ / Green🟢 / Gold🌟 ticks |
| **YouTube** | In-app video player — views credited to creators · Live stats |
| **Security** | Identity + 2FA + OAuth · Rate limiting · GDPR · 30-day deletion grace period |
| **PWA** | Install on phone · Offline recipe access via service worker |
| **Infrastructure** | Docker · GitHub Actions CI/CD · Redis · Hangfire background jobs · Serilog |

---

## 🧪 Tests

```bash
dotnet test MyMealPlanner.Tests --configuration Release
```

25+ unit tests + integration tests covering ranking, scraper, AI tagger, allergy detection, cost service, models, and HTTP endpoints.

---

## 📦 Tech Stack

| Layer | Technology |
|---|---|
| Backend | ASP.NET Core 8 MVC, C# |
| Database | SQL Server + EF Core (PostgreSQL-ready) |
| Auth | Identity + 2FA + Google/Facebook OAuth |
| Real-time | SignalR |
| Background | Hangfire |
| AI | Groq/Ollama/OpenRouter/Claude (configurable) |
| Translation | LibreTranslate (self-hosted Docker, free) |
| Video | YouTube Data API v3 |
| Scraping | HtmlAgilityPack + AngleSharp + Polly |
| Email | MailKit + Gmail SMTP |
| PDF | QuestPDF (community license) |
| Cache | Redis |
| Storage | Cloudinary (free 25GB) |
| Styling | Bootstrap 5 + custom CSS design system |
| CI/CD | GitHub Actions + Docker |
| Deploy | Railway / Render / Azure App Service |

**Estimated monthly cost at launch: $0–$15** — entirely open-source tooling.

---

## 🌍 Languages

English · Français · Español · Português · العربية · 中文 · हिन्दी · Kiswahili · Deutsch · Italiano · 日本語 · 한국어

---

*MIT License · Built with ❤️ — My Meal Planner*
