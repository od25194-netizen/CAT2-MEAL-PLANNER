using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using MimeKit.Text;

namespace MyMealPlanner.Services.Email;

public interface IEmailService
{
    Task SendAsync(string toEmail, string toName, string subject, string htmlBody);
    Task SendEmailConfirmationAsync(string toEmail, string toName, string confirmUrl);
    Task SendPasswordResetAsync(string toEmail, string toName, string resetUrl);
    Task SendWelcomeAsync(string toEmail, string toName);
    Task SendRecipeApprovedAsync(string toEmail, string toName, string recipeTitle);
    Task SendNewFollowerAsync(string toEmail, string toName, string followerName);
    Task SendDailyJokeAsync(string toEmail, string toName, string jokeBody);
    Task SendWeeklyMealPlanAsync(string toEmail, string toName, string planUrl);
}

/// <summary>
/// Sends transactional emails via MailKit + Gmail SMTP (free tier).
/// All emails use a branded HTML template with the My Meal Planner design system.
/// Configure SMTP in appsettings.json under "Email".
/// </summary>
public class EmailService : IEmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _logger;

    private string SmtpHost     => _config["Email:SmtpHost"]  ?? "smtp.gmail.com";
    private int    SmtpPort     => int.TryParse(_config["Email:SmtpPort"], out var p) ? p : 587;
    private string SmtpUser     => _config["Email:Username"]  ?? "";
    private string SmtpPass     => _config["Email:Password"]  ?? "";
    private string FromName     => _config["Email:FromName"]  ?? "My Meal Planner";
    private string FromAddress  => _config["Email:Username"]  ?? "noreply@mymealplanner.app";
    private string AppUrl       => _config["App:BaseUrl"]     ?? "https://mymealplanner.app";

    public EmailService(IConfiguration config, ILogger<EmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task SendAsync(string toEmail, string toName, string subject, string htmlBody)
    {
        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(FromName, FromAddress));
            message.To.Add(new MailboxAddress(toName, toEmail));
            message.Subject = subject;
            message.Body    = new TextPart(TextFormat.Html) { Text = WrapInTemplate(htmlBody, subject) };

            using var client = new SmtpClient();
            await client.ConnectAsync(SmtpHost, SmtpPort, SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(SmtpUser, SmtpPass);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            _logger.LogInformation("[Email] Sent '{Subject}' to {Email}", subject, toEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Email] Failed to send '{Subject}' to {Email}", subject, toEmail);
        }
    }

    public async Task SendEmailConfirmationAsync(string toEmail, string toName, string confirmUrl)
    {
        var body = $"""
            <div style="text-align:center;padding:20px 0">
                <div style="font-size:60px">📧</div>
                <h2 style="font-family:'Georgia',serif;color:#1A1A2E;margin:16px 0 8px">
                    Confirm Your Email
                </h2>
                <p style="color:#6B7280;margin-bottom:24px">
                    Welcome to My Meal Planner, {toName}! Click the button below to confirm
                    your email address and start exploring recipes from every culture on Earth.
                </p>
                <a href="{confirmUrl}"
                   style="display:inline-block;background:#E8630A;color:white;
                          padding:14px 32px;border-radius:50px;text-decoration:none;
                          font-weight:600;font-size:16px">
                    Confirm Email Address
                </a>
                <p style="color:#9CA3AF;font-size:13px;margin-top:20px">
                    This link expires in 24 hours. If you didn't create an account,
                    you can safely ignore this email.
                </p>
            </div>
            """;
        await SendAsync(toEmail, toName, "Confirm your My Meal Planner account", body);
    }

    public async Task SendPasswordResetAsync(string toEmail, string toName, string resetUrl)
    {
        var body = $"""
            <div style="text-align:center;padding:20px 0">
                <div style="font-size:60px">🔑</div>
                <h2 style="font-family:'Georgia',serif;color:#1A1A2E;margin:16px 0 8px">
                    Reset Your Password
                </h2>
                <p style="color:#6B7280;margin-bottom:24px">
                    Hi {toName}, we received a request to reset your password.
                    Click below to create a new one.
                </p>
                <a href="{resetUrl}"
                   style="display:inline-block;background:#E8630A;color:white;
                          padding:14px 32px;border-radius:50px;text-decoration:none;
                          font-weight:600;font-size:16px">
                    Reset Password
                </a>
                <p style="color:#E84646;font-size:13px;margin-top:20px;font-weight:600">
                    ⚠️ This link expires in 15 minutes.
                </p>
                <p style="color:#9CA3AF;font-size:13px">
                    If you didn't request this, your account is safe — just ignore this email.
                </p>
            </div>
            """;
        await SendAsync(toEmail, toName, "Reset your My Meal Planner password", body);
    }

    public async Task SendWelcomeAsync(string toEmail, string toName)
    {
        var body = $"""
            <div style="padding:20px 0">
                <div style="text-align:center;font-size:60px">🍽️</div>
                <h2 style="font-family:'Georgia',serif;color:#1A1A2E;text-align:center;
                            margin:16px 0 8px">
                    Welcome to My Meal Planner, {toName}!
                </h2>
                <p style="color:#6B7280;text-align:center;margin-bottom:24px">
                    You're now part of a global community of food lovers.
                    Here's what you can do:
                </p>
                <div style="background:#FFF0E6;border-radius:12px;padding:20px;margin-bottom:16px">
                    <div style="font-weight:700;margin-bottom:12px">🚀 Get Started</div>
                    <div style="margin-bottom:8px">🌍 Explore recipes from 195 countries</div>
                    <div style="margin-bottom:8px">📅 Build your weekly meal plan</div>
                    <div style="margin-bottom:8px">🏆 Take quizzes to level up your chef rank</div>
                    <div style="margin-bottom:8px">💚 Discover how food affects your health</div>
                    <div>🐾 Find meals for your pets too!</div>
                </div>
                <div style="text-align:center">
                    <a href="{AppUrl}"
                       style="display:inline-block;background:#E8630A;color:white;
                              padding:14px 32px;border-radius:50px;text-decoration:none;
                              font-weight:600;font-size:16px">
                        Start Exploring
                    </a>
                </div>
            </div>
            """;
        await SendAsync(toEmail, toName, $"Welcome to My Meal Planner, {toName}! 🍽️", body);
    }

    public async Task SendRecipeApprovedAsync(string toEmail, string toName, string recipeTitle)
    {
        var body = $"""
            <div style="text-align:center;padding:20px 0">
                <div style="font-size:60px">🎉</div>
                <h2 style="font-family:'Georgia',serif;color:#1A1A2E;margin:16px 0 8px">
                    Your Recipe Was Approved!
                </h2>
                <p style="color:#6B7280;margin-bottom:8px">
                    Great news, {toName}! Your recipe submission has been reviewed and published:
                </p>
                <div style="background:#FFF0E6;border-radius:12px;padding:16px;
                            font-weight:700;font-size:18px;color:#E8630A;margin-bottom:24px">
                    📖 {recipeTitle}
                </div>
                <p style="color:#6B7280;margin-bottom:24px">
                    You've earned the <strong>Contributor</strong> badge! The community can now
                    discover and cook your recipe. 🏅
                </p>
                <a href="{AppUrl}/Recipe"
                   style="display:inline-block;background:#E8630A;color:white;
                          padding:14px 32px;border-radius:50px;text-decoration:none;
                          font-weight:600;font-size:16px">
                    View Your Recipe
                </a>
            </div>
            """;
        await SendAsync(toEmail, toName, $"Your recipe '{recipeTitle}' is now live! 🎉", body);
    }

    public async Task SendNewFollowerAsync(string toEmail, string toName, string followerName)
    {
        var body = $"""
            <div style="text-align:center;padding:20px 0">
                <div style="font-size:60px">👥</div>
                <h2 style="font-family:'Georgia',serif;color:#1A1A2E;margin:16px 0 8px">
                    You have a new follower!
                </h2>
                <p style="color:#6B7280;margin-bottom:24px">
                    <strong>{followerName}</strong> is now following your profile on My Meal Planner.
                    They'll see all your recipes, cook logs, and activity.
                </p>
                <a href="{AppUrl}/Profile"
                   style="display:inline-block;background:#E8630A;color:white;
                          padding:14px 32px;border-radius:50px;text-decoration:none;
                          font-weight:600;font-size:16px">
                    View Your Profile
                </a>
            </div>
            """;
        await SendAsync(toEmail, toName, $"{followerName} is now following you on My Meal Planner", body);
    }

    public async Task SendDailyJokeAsync(string toEmail, string toName, string jokeBody)
    {
        var body = $"""
            <div style="text-align:center;padding:20px 0">
                <div style="font-size:60px">😂</div>
                <h2 style="font-family:'Georgia',serif;color:#1A1A2E;margin:16px 0 8px">
                    Your Daily Cooking Joke 🍳
                </h2>
                <div style="background:#FFF0E6;border-radius:12px;padding:20px;
                            font-size:18px;font-family:'Georgia',serif;
                            color:#1A1A2E;margin-bottom:24px;font-style:italic">
                    "{jokeBody}"
                </div>
                <a href="{AppUrl}/Jokes"
                   style="display:inline-block;background:#E8630A;color:white;
                          padding:12px 24px;border-radius:50px;text-decoration:none;
                          font-weight:600">
                    More Jokes 😂
                </a>
            </div>
            """;
        await SendAsync(toEmail, toName, "Your daily cooking joke is ready! 😂", body);
    }

    public async Task SendWeeklyMealPlanAsync(string toEmail, string toName, string planUrl)
    {
        var body = $"""
            <div style="text-align:center;padding:20px 0">
                <div style="font-size:60px">📅</div>
                <h2 style="font-family:'Georgia',serif;color:#1A1A2E;margin:16px 0 8px">
                    Your Weekly Meal Plan is Ready!
                </h2>
                <p style="color:#6B7280;margin-bottom:24px">
                    Hi {toName}! A personalised 7-day meal plan has been generated for you
                    based on your food preferences and health goals.
                </p>
                <a href="{planUrl}"
                   style="display:inline-block;background:#E8630A;color:white;
                          padding:14px 32px;border-radius:50px;text-decoration:none;
                          font-weight:600;font-size:16px">
                    View My Meal Plan
                </a>
            </div>
            """;
        await SendAsync(toEmail, toName, "Your weekly meal plan is ready! 📅", body);
    }

    // ── Branded HTML Template ─────────────────────────────────
    private string WrapInTemplate(string content, string subject) => $"""
        <!DOCTYPE html>
        <html>
        <head>
            <meta charset="utf-8" />
            <meta name="viewport" content="width=device-width, initial-scale=1.0" />
            <title>{subject}</title>
        </head>
        <body style="margin:0;padding:0;background:#F8F4EF;font-family:'DM Sans',Arial,sans-serif">
            <table width="100%" cellpadding="0" cellspacing="0">
                <tr>
                    <td align="center" style="padding:32px 16px">
                        <table width="600" cellpadding="0" cellspacing="0"
                               style="background:white;border-radius:16px;overflow:hidden;
                                      box-shadow:0 4px 24px rgba(0,0,0,.1);max-width:100%">
                            <!-- Header -->
                            <tr>
                                <td style="background:linear-gradient(135deg,#1A1A2E,#2D1B00);
                                           padding:24px 32px;text-align:center">
                                    <div style="font-size:36px">🍽️</div>
                                    <div style="color:#E8630A;font-size:22px;font-weight:700;
                                                font-family:'Georgia',serif;margin-top:8px">
                                        My Meal Planner
                                    </div>
                                    <div style="color:rgba(255,255,255,.6);font-size:13px;margin-top:4px">
                                        Discover recipes from every culture
                                    </div>
                                </td>
                            </tr>
                            <!-- Content -->
                            <tr>
                                <td style="padding:32px">
                                    {content}
                                </td>
                            </tr>
                            <!-- Footer -->
                            <tr>
                                <td style="background:#F8F4EF;padding:20px 32px;text-align:center;
                                           border-top:1px solid #EDE8E2">
                                    <div style="color:#9CA3AF;font-size:12px">
                                        © {DateTime.UtcNow.Year} My Meal Planner · All rights reserved<br />
                                        <a href="{AppUrl}/Account/Settings" style="color:#E8630A">
                                            Manage email preferences
                                        </a>
                                        ·
                                        <a href="{AppUrl}" style="color:#E8630A">Visit website</a>
                                    </div>
                                </td>
                            </tr>
                        </table>
                    </td>
                </tr>
            </table>
        </body>
        </html>
        """;
}
