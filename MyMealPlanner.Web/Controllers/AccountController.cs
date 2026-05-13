using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MyMealPlanner.Core.Enums;
using MyMealPlanner.Core.Models;
using MyMealPlanner.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using MyMealPlanner.Web.ViewModels;
using MyMealPlanner.Services.Email;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace MyMealPlanner.Web.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<AccountController> _logger;
    private readonly IEmailService _email;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ApplicationDbContext db,
        ILogger<AccountController> logger,
        IEmailService email)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _db = db;
        _logger = logger;
        _email = email;
    }

    // ═══════════════════════════════════════════════════════════
    // REGISTRATION — Step 1 (Basic info)
    // ═══════════════════════════════════════════════════════════
    [HttpGet]
    public IActionResult Register(int step = 1)
    {
        if (_signInManager.IsSignedIn(User)) return RedirectToAction("Index", "Home");
        ViewBag.Step = step;
        return View(step switch
        {
            2 => (object)new RegisterStep2ViewModel(),
            3 => new RegisterStep3ViewModel(),
            _ => new RegisterStep1ViewModel()
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RegisterStep1(RegisterStep1ViewModel model)
    {
        if (!ModelState.IsValid) { ViewBag.Step = 1; return View("Register", model); }

        // Store in session for multi-step
        HttpContext.Session.SetString("reg_fullname",  model.FullName);
        HttpContext.Session.SetString("reg_email",     model.Email);
        HttpContext.Session.SetString("reg_password",  model.Password);
        HttpContext.Session.SetString("reg_dob",       model.DateOfBirth.ToString("O"));
        HttpContext.Session.SetString("reg_country",   model.CountryCode);
        HttpContext.Session.SetString("reg_lang",      model.PreferredLanguage);

        return RedirectToAction("Register", new { step = 2 });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult RegisterStep2(RegisterStep2ViewModel model)
    {
        if (!ModelState.IsValid) { ViewBag.Step = 2; return View("Register", model); }

        HttpContext.Session.SetString("reg_dish",     model.FavouriteDish ?? "");
        HttpContext.Session.SetString("reg_diet",     string.Join(",", model.DietaryRestrictions));
        HttpContext.Session.SetString("reg_allergies",string.Join(",", model.Allergies));
        HttpContext.Session.SetString("reg_servings", model.NumberOfPeopleICookFor.ToString());
        HttpContext.Session.SetString("reg_goal",     model.HealthGoal.ToString());
        HttpContext.Session.SetString("reg_level",    ((int)model.SkillLevel).ToString());
        HttpContext.Session.SetString("reg_cuisines", string.Join(",", model.FavouriteCuisines));

        return RedirectToAction("Register", new { step = 3 });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RegisterStep3(RegisterStep3ViewModel model)
    {
        if (!ModelState.IsValid) { ViewBag.Step = 3; return View("Register", model); }

        // Reconstruct user from session
        var email    = HttpContext.Session.GetString("reg_email") ?? "";
        var password = HttpContext.Session.GetString("reg_password") ?? "";
        var dob      = DateTime.Parse(HttpContext.Session.GetString("reg_dob") ?? DateTime.Today.AddYears(-25).ToString("O"));
        var servings = int.TryParse(HttpContext.Session.GetString("reg_servings"), out var sv) ? sv : 1;

        Enum.TryParse<HealthGoal>(HttpContext.Session.GetString("reg_goal"), out var goal);
        Enum.TryParse<ChefLevel>(HttpContext.Session.GetString("reg_level"), out var level);

        var user = new ApplicationUser
        {
            UserName                = email,
            Email                   = email,
            FullName                = HttpContext.Session.GetString("reg_fullname") ?? "",
            DateOfBirth             = dob,
            AgeBracket              = CalculateAgeBracket(dob),
            CountryCode             = HttpContext.Session.GetString("reg_country"),
            PreferredLanguage       = HttpContext.Session.GetString("reg_lang") ?? "en",
            FavouriteDish           = HttpContext.Session.GetString("reg_dish"),
            DietaryRestrictionsJson = HttpContext.Session.GetString("reg_diet"),
            AllergiesJson           = HttpContext.Session.GetString("reg_allergies"),
            FavouriteCuisinesJson   = HttpContext.Session.GetString("reg_cuisines"),
            NumberOfPeopleICookFor  = servings,
            HealthGoal              = goal,
            ChefLevel               = level,
            Hobbies                 = model.Hobbies,
            Bio                     = model.Bio,
            Address                 = model.Address,
            CreatedAt               = DateTime.UtcNow
        };

        if (model.BloodType.HasValue) user.BloodType = model.BloodType.Value;

        var result = await _userManager.CreateAsync(user, password);

        if (!result.Succeeded)
        {
            foreach (var err in result.Errors)
                ModelState.AddModelError("", err.Description);
            ViewBag.Step = 3;
            return View("Register", model);
        }

        // Add to Member role
        await _userManager.AddToRoleAsync(user, "Member");

        // Add ChefLevel claim
        await _userManager.AddClaimAsync(user, new Claim("ChefLevel", ((int)level).ToString()));

        // Add pets
        if (model.HasPet && model.Pets.Count > 0)
        {
            foreach (var pet in model.Pets)
            {
                _db.PetProfiles.Add(new PetProfile
                {
                    OwnerId  = user.Id,
                    PetName  = pet.PetName,
                    Type     = pet.PetType,
                    Breed    = pet.Breed,
                    AgeYears = pet.AgeYears
                });
            }
            await _db.SaveChangesAsync();
        }

        // Send confirmation email
        var token      = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var confirmUrl = Url.Action("ConfirmEmail", "Account",
                             new { userId = user.Id, token }, Request.Scheme)!;

        _logger.LogInformation("[Register] New user {Email}. Confirm link: {Url}", email, confirmUrl);
        _ = _email.SendEmailConfirmationAsync(email, user.FullName, confirmUrl);
        _ = _email.SendWelcomeAsync(email, user.FullName);

        TempData["Success"] = $"Welcome to My Meal Planner, {user.FullName}! 🎉 Check your email to confirm your account.";
        return RedirectToAction("RegisterConfirmation");
    }

    [HttpGet]
    public IActionResult RegisterConfirmation() => View();

    [HttpGet]
    public async Task<IActionResult> ConfirmEmail(string userId, string token)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return NotFound();

        var result = await _userManager.ConfirmEmailAsync(user, token);
        if (!result.Succeeded)
        {
            TempData["Error"] = "Email confirmation link is invalid or expired.";
            return View("Error");
        }

        TempData["Success"] = "Email confirmed! You can now log in. 🍽️";
        return RedirectToAction("Login");
    }

    // ═══════════════════════════════════════════════════════════
    // LOGIN
    // ═══════════════════════════════════════════════════════════
    [HttpGet]
    public async Task<IActionResult> Login(string? returnUrl)
    {
        if (_signInManager.IsSignedIn(User))
            return RedirectToLocal(returnUrl);

        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

        ViewBag.ExternalProviders = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var result = await _signInManager.PasswordSignInAsync(
            model.Email, model.Password, model.RememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            // Update last active
            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user is not null) { user.LastActiveAt = DateTime.UtcNow; await _db.SaveChangesAsync(); }

            _logger.LogInformation("[Login] {Email} signed in", model.Email);
            return RedirectToLocal(model.ReturnUrl);
        }

        if (result.RequiresTwoFactor)
            return RedirectToAction("LoginWith2FA", new { model.ReturnUrl, model.RememberMe });

        if (result.IsLockedOut)
        {
            _logger.LogWarning("[Login] {Email} locked out", model.Email);
            TempData["Error"] = "Account locked after too many failed attempts. Try again in 15 minutes.";
            return View(model);
        }

        ModelState.AddModelError("", "Invalid email or password.");
        return View(model);
    }

    // ═══════════════════════════════════════════════════════════
    // TWO-FACTOR AUTHENTICATION
    // ═══════════════════════════════════════════════════════════
    [HttpGet]
    public async Task<IActionResult> LoginWith2FA(string? returnUrl, bool rememberMe)
    {
        var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
        if (user is null) return RedirectToAction("Login");

        return View(new TwoFactorViewModel { ReturnUrl = returnUrl, RememberMachine = rememberMe });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> LoginWith2FA(TwoFactorViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var code   = model.Code.Replace(" ", "").Replace("-", "");
        var result = await _signInManager.TwoFactorAuthenticatorSignInAsync(
            code, model.RememberMachine, model.RememberMachine);

        if (result.Succeeded) return RedirectToLocal(model.ReturnUrl);

        if (result.IsLockedOut) { TempData["Error"] = "Account locked."; return RedirectToAction("Login"); }

        ModelState.AddModelError("", "Invalid authenticator code.");
        return View(model);
    }

    // ── Enable 2FA ────────────────────────────────────────────
    [Authorize, HttpGet]
    public async Task<IActionResult> Enable2FA()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return NotFound();

        await _userManager.ResetAuthenticatorKeyAsync(user);
        var sharedKey = await _userManager.GetAuthenticatorKeyAsync(user) ?? "";
        var email     = await _userManager.GetEmailAsync(user) ?? "";
        var uri       = GenerateQrCodeUri(email, sharedKey);

        return View(new Enable2FAViewModel
        {
            SharedKey        = FormatKey(sharedKey),
            AuthenticatorUri = uri
        });
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Enable2FA(Enable2FAViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return NotFound();

        if (!ModelState.IsValid)
        {
            var key = await _userManager.GetAuthenticatorKeyAsync(user) ?? "";
            model.SharedKey = FormatKey(key);
            return View(model);
        }

        var code   = model.Code.Replace(" ", "").Replace("-", "");
        var valid  = await _userManager.VerifyTwoFactorTokenAsync(
            user, _userManager.Options.Tokens.AuthenticatorTokenProvider, code);

        if (!valid) { ModelState.AddModelError("Code", "Invalid code."); return View(model); }

        await _userManager.SetTwoFactorEnabledAsync(user, true);
        var recoveryCodes = (await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 8))!.ToList();

        TempData["RecoveryCodes"] = string.Join(",", recoveryCodes);
        TempData["Success"] = "Two-factor authentication is now enabled. Save your recovery codes!";
        return RedirectToAction("TwoFactorRecoveryCodes");
    }

    [Authorize, HttpGet]
    public IActionResult TwoFactorRecoveryCodes()
    {
        var codes = (TempData["RecoveryCodes"] as string)?.Split(',').ToList() ?? [];
        return View(codes);
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Disable2FA()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return NotFound();

        await _userManager.SetTwoFactorEnabledAsync(user, false);
        TempData["Success"] = "Two-factor authentication has been disabled.";
        return RedirectToAction("Settings");
    }

    // ═══════════════════════════════════════════════════════════
    // OAUTH EXTERNAL LOGIN
    // ═══════════════════════════════════════════════════════════
    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult ExternalLogin(string provider, string? returnUrl)
    {
        var redirectUrl  = Url.Action("ExternalLoginCallback", new { returnUrl });
        var properties   = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
        return Challenge(properties, provider);
    }

    [HttpGet]
    public async Task<IActionResult> ExternalLoginCallback(string? returnUrl, string? remoteError)
    {
        if (remoteError is not null) { TempData["Error"] = $"OAuth error: {remoteError}"; return RedirectToAction("Login"); }

        var info = await _signInManager.GetExternalLoginInfoAsync();
        if (info is null) return RedirectToAction("Login");

        var result = await _signInManager.ExternalLoginSignInAsync(
            info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true);

        if (result.Succeeded) return RedirectToLocal(returnUrl);

        // New user — register via OAuth
        var email    = info.Principal.FindFirstValue(ClaimTypes.Email) ?? "";
        var fullName = info.Principal.FindFirstValue(ClaimTypes.Name)  ?? "";
        var photo    = info.Principal.FindFirstValue("picture");

        var user = new ApplicationUser
        {
            UserName          = email,
            Email             = email,
            FullName          = fullName,
            ProfilePhotoUrl   = photo,
            EmailConfirmed    = true,
            CreatedAt         = DateTime.UtcNow
        };

        var createResult = await _userManager.CreateAsync(user);
        if (createResult.Succeeded)
        {
            await _userManager.AddLoginAsync(user, info);
            await _userManager.AddToRoleAsync(user, "Member");
            await _userManager.AddClaimAsync(user, new Claim("ChefLevel", "1"));
            await _signInManager.SignInAsync(user, isPersistent: false, info.LoginProvider);
            return RedirectToAction("Register", new { step = 2 }); // collect food prefs
        }

        foreach (var err in createResult.Errors) ModelState.AddModelError("", err.Description);
        return View("Login");
    }

    // ═══════════════════════════════════════════════════════════
    // FORGOT / RESET PASSWORD
    // ═══════════════════════════════════════════════════════════
    [HttpGet]
    public IActionResult ForgotPassword() => View();

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await _userManager.FindByEmailAsync(model.Email);
        // Always show the same message to prevent email enumeration
        if (user is null || !await _userManager.IsEmailConfirmedAsync(user))
        {
            TempData["Success"] = "If that email exists, you'll receive a reset link shortly.";
            return RedirectToAction("ForgotPasswordConfirmation");
        }

        var token    = await _userManager.GeneratePasswordResetTokenAsync(user);
        var resetUrl = Url.Action("ResetPassword", "Account",
                           new { email = model.Email, token }, Request.Scheme)!;

        _logger.LogInformation("[Password] Reset link for {Email}: {Url}", model.Email, resetUrl);
        _ = _email.SendPasswordResetAsync(model.Email, user.FullName ?? model.Email, resetUrl);

        TempData["Success"] = "If that email exists, you'll receive a reset link shortly.";
        return RedirectToAction("ForgotPasswordConfirmation");
    }

    [HttpGet]
    public IActionResult ForgotPasswordConfirmation() => View();

    [HttpGet]
    public IActionResult ResetPassword(string email, string token)
        => View(new ResetPasswordViewModel { Email = email, Token = token });

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user is null) return RedirectToAction("ResetPasswordConfirmation");

        var result = await _userManager.ResetPasswordAsync(user, model.Token, model.NewPassword);
        if (result.Succeeded)
        {
            TempData["Success"] = "Password reset successfully. You can now log in.";
            return RedirectToAction("Login");
        }

        foreach (var err in result.Errors) ModelState.AddModelError("", err.Description);
        return View(model);
    }

    // ═══════════════════════════════════════════════════════════
    // SETTINGS & PROFILE MANAGEMENT
    // ═══════════════════════════════════════════════════════════
    [Authorize, HttpGet]
    public async Task<IActionResult> Settings()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return NotFound();

        var vm = new SettingsViewModel
        {
            DarkMode              = user.DarkMode,
            AccentColor           = user.AccentColor,
            ProfileVisibility     = user.ProfileVisibility,
            ShowOnlineStatus      = user.ShowOnlineStatus,
            EmailNotifications    = user.EmailNotifications,
            PushNotifications     = user.PushNotifications,
            DailyJokeNotification = user.DailyJokeNotification,
            MealTimeReminders     = user.MealTimeReminders,
            PreferredLanguage     = user.PreferredLanguage,
            PreferredCurrency     = user.PreferredCurrency,
            PreferredUnits        = user.PreferredUnits,
            TimeZone              = user.TimeZone
        };
        return View(vm);
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Settings(SettingsViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return NotFound();

        user.DarkMode              = model.DarkMode;
        user.AccentColor           = model.AccentColor;
        user.ProfileVisibility     = model.ProfileVisibility;
        user.ShowOnlineStatus      = model.ShowOnlineStatus;
        user.EmailNotifications    = model.EmailNotifications;
        user.PushNotifications     = model.PushNotifications;
        user.DailyJokeNotification = model.DailyJokeNotification;
        user.MealTimeReminders     = model.MealTimeReminders;
        user.PreferredLanguage     = model.PreferredLanguage;
        user.PreferredCurrency     = model.PreferredCurrency;
        user.PreferredUnits        = model.PreferredUnits;
        user.TimeZone              = model.TimeZone;

        await _db.SaveChangesAsync();
        TempData["Success"] = "Settings saved ✓";
        return RedirectToAction("Settings");
    }

    // ── Download My Data (GDPR) ───────────────────────────────
    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DownloadMyData()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return NotFound();

        var data = new
        {
            profile = new {
                user.FullName, user.Email, user.DateOfBirth, user.CountryCode,
                user.Bio, user.FavouriteDish, user.Hobbies, user.CreatedAt
            },
            cookLogs    = await _db.CookLogs.Where(c => c.UserId == user.Id).ToListAsync(),
            collections = await _db.SavedCollections.Where(c => c.UserId == user.Id).ToListAsync(),
        };

        var json  = System.Text.Json.JsonSerializer.Serialize(data,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        return File(bytes, "application/json", $"my-mealplanner-data-{DateTime.UtcNow:yyyyMMdd}.json");
    }

    // ── Delete Account ────────────────────────────────────────
    [Authorize, HttpGet]
    public IActionResult DeleteAccount() => View(new DeleteAccountViewModel());

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAccount(DeleteAccountViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await _userManager.GetUserAsync(User);
        if (user is null) return NotFound();

        var check = await _userManager.CheckPasswordAsync(user, model.Password);
        if (!check) { ModelState.AddModelError("Password", "Incorrect password."); return View(model); }

        // Soft delete — 30-day grace period
        user.IsDeleted          = true;
        user.DeleteRequestedAt  = DateTime.UtcNow;
        user.DeleteScheduledAt  = DateTime.UtcNow.AddDays(30);
        await _db.SaveChangesAsync();

        await _signInManager.SignOutAsync();
        _logger.LogInformation("[DeleteAccount] {Email} scheduled for deletion on {Date}",
            user.Email, user.DeleteScheduledAt);

        TempData["Info"] = "Your account has been scheduled for deletion in 30 days. You can cancel by logging in within that window.";
        return RedirectToAction("Index", "Home");
    }

    // ── Logout ────────────────────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    // ═══════════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════════
    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);
        return RedirectToAction("Index", "Home");
    }

    private static AgeBracket CalculateAgeBracket(DateTime dob)
    {
        var age = (int)((DateTime.UtcNow - dob).TotalDays / 365.25);
        return age switch
        {
            <= 2  => AgeBracket.Infants,
            <= 12 => AgeBracket.Children,
            <= 19 => AgeBracket.Teenagers,
            <= 35 => AgeBracket.YoungAdults,
            <= 55 => AgeBracket.Adults,
            <= 70 => AgeBracket.Seniors,
            _     => AgeBracket.Elderly
        };
    }

    private static string FormatKey(string key) =>
        string.Join(" ", Enumerable.Range(0, key.Length / 4)
            .Select(i => key.Substring(i * 4, Math.Min(4, key.Length - i * 4))));

    private static string GenerateQrCodeUri(string email, string key) =>
        $"otpauth://totp/MyMealPlanner:{Uri.EscapeDataString(email)}" +
        $"?secret={key}&issuer=MyMealPlanner&digits=6&algorithm=SHA1&period=30";
}
