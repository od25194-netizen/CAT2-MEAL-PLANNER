using System.ComponentModel.DataAnnotations;
using MyMealPlanner.Core.Enums;

namespace MyMealPlanner.Web.ViewModels;

// ═══════════════════════════════════════════════════════════════
// REGISTRATION — multi-step
// ═══════════════════════════════════════════════════════════════
public class RegisterStep1ViewModel
{
    [Required, StringLength(200, MinimumLength = 2)]
    [Display(Name = "Full Name")]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    [StringLength(100, MinimumLength = 8)]
    [RegularExpression(@"^(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z\d]).{8,}$",
        ErrorMessage = "Password needs uppercase, number, and special character.")]
    public string Password { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    [Compare("Password", ErrorMessage = "Passwords do not match.")]
    [Display(Name = "Confirm Password")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required, DataType(DataType.Date)]
    [Display(Name = "Date of Birth")]
    public DateTime DateOfBirth { get; set; } = DateTime.Today.AddYears(-25);

    [Required, Display(Name = "Country")]
    public string CountryCode { get; set; } = string.Empty;

    [Required, Display(Name = "Preferred Language")]
    public string PreferredLanguage { get; set; } = "en";
}

public class RegisterStep2ViewModel
{
    [Display(Name = "Favourite Dish")]
    public string? FavouriteDish { get; set; }

    [Display(Name = "Dietary Restrictions")]
    public List<string> DietaryRestrictions { get; set; } = [];

    [Display(Name = "Food Allergies")]
    public List<string> Allergies { get; set; } = [];

    [Range(1, 50), Display(Name = "How many people do you cook for?")]
    public int NumberOfPeopleICookFor { get; set; } = 1;

    [Display(Name = "Health Goal")]
    public HealthGoal HealthGoal { get; set; }

    [Display(Name = "Cooking Skill Level")]
    public ChefLevel SkillLevel { get; set; } = ChefLevel.Level1_KitchenNewcomer;

    public List<string> FavouriteCuisines { get; set; } = [];
}

public class RegisterStep3ViewModel
{
    [Display(Name = "Hobbies (optional)")]
    public string? Hobbies { get; set; }

    [Display(Name = "Bio (optional)")]
    [StringLength(500)]
    public string? Bio { get; set; }

    // Optional sensitive fields (encrypted at rest)
    [Display(Name = "Blood Type (optional — for personalised meal suggestions)")]
    public BloodType? BloodType { get; set; }

    [Display(Name = "City / Address (optional — for local food discovery)")]
    public string? Address { get; set; }

    // Pet
    [Display(Name = "Do you have a pet?")]
    public bool HasPet { get; set; }

    public List<PetRegistrationViewModel> Pets { get; set; } = [];

    [Display(Name = "I agree to the Terms of Service and Privacy Policy")]
    [Range(typeof(bool), "true", "true", ErrorMessage = "You must accept the terms.")]
    public bool AcceptTerms { get; set; }

    [Display(Name = "I consent to cookie usage")]
    [Range(typeof(bool), "true", "true", ErrorMessage = "Cookie consent is required.")]
    public bool AcceptCookies { get; set; }
}

public class PetRegistrationViewModel
{
    [Required, StringLength(100)]
    public string PetName { get; set; } = string.Empty;

    [Required]
    public PetType PetType { get; set; }

    public string? Breed { get; set; }

    [Range(0, 30)]
    public int AgeYears { get; set; }

    public string? ProfilePhotoUrl { get; set; }
}

// ═══════════════════════════════════════════════════════════════
// LOGIN
// ═══════════════════════════════════════════════════════════════
public class LoginViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Remember me for 14 days")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}

// ═══════════════════════════════════════════════════════════════
// 2FA
// ═══════════════════════════════════════════════════════════════
public class TwoFactorViewModel
{
    [Required, StringLength(7, MinimumLength = 6)]
    [Display(Name = "Authenticator Code")]
    [DataType(DataType.Text)]
    public string Code { get; set; } = string.Empty;

    public bool RememberMachine { get; set; }
    public string? ReturnUrl { get; set; }
}

public class Enable2FAViewModel
{
    public string SharedKey { get; set; } = string.Empty;
    public string AuthenticatorUri { get; set; } = string.Empty;

    [Required, StringLength(7, MinimumLength = 6)]
    [Display(Name = "Verification Code")]
    public string Code { get; set; } = string.Empty;

    public List<string> RecoveryCodes { get; set; } = [];
}

// ═══════════════════════════════════════════════════════════════
// PASSWORD MANAGEMENT
// ═══════════════════════════════════════════════════════════════
public class ForgotPasswordViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;
}

public class ResetPasswordViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Token { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    [StringLength(100, MinimumLength = 8)]
    [RegularExpression(@"^(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z\d]).{8,}$",
        ErrorMessage = "Password needs uppercase, number, and special character.")]
    [Display(Name = "New Password")]
    public string NewPassword { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    [Compare("NewPassword")]
    [Display(Name = "Confirm New Password")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class ChangePasswordViewModel
{
    [Required, DataType(DataType.Password)]
    [Display(Name = "Current Password")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    [StringLength(100, MinimumLength = 8)]
    [Display(Name = "New Password")]
    public string NewPassword { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    [Compare("NewPassword")]
    [Display(Name = "Confirm New Password")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

// ═══════════════════════════════════════════════════════════════
// ACCOUNT MANAGEMENT
// ═══════════════════════════════════════════════════════════════
public class DeleteAccountViewModel
{
    [Required, DataType(DataType.Password)]
    [Display(Name = "Confirm your password to delete your account")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "I understand my data will be permanently deleted after 30 days")]
    [Range(typeof(bool), "true", "true", ErrorMessage = "You must confirm you understand.")]
    public bool Confirmed { get; set; }
}

public class EditProfileViewModel
{
    [Required, StringLength(200, MinimumLength = 2)]
    [Display(Name = "Full Name")]
    public string FullName { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Bio { get; set; }

    public string? ProfilePhotoUrl { get; set; }
    public string? CoverPhotoUrl { get; set; }

    [Display(Name = "Favourite Dish")]
    public string? FavouriteDish { get; set; }

    [Display(Name = "Hobbies")]
    public string? Hobbies { get; set; }

    [Display(Name = "YouTube Channel URL")]
    [Url]
    public string? YoutubeChannelUrl { get; set; }

    [Display(Name = "Instagram Handle")]
    public string? InstagramHandle { get; set; }

    public string? CountryCode { get; set; }
    public string PreferredLanguage { get; set; } = "en";
    public string PreferredCurrency { get; set; } = "USD";
    public string PreferredUnits { get; set; } = "metric";

    [Display(Name = "People I Cook For")]
    [Range(1, 50)]
    public int NumberOfPeopleICookFor { get; set; } = 1;

    public HealthGoal HealthGoal { get; set; }
    public List<string> DietaryRestrictions { get; set; } = [];
    public List<string> Allergies { get; set; } = [];
    public List<string> FavouriteCuisines { get; set; } = [];
}

// ═══════════════════════════════════════════════════════════════
// SETTINGS
// ═══════════════════════════════════════════════════════════════
public class SettingsViewModel
{
    // Appearance
    public bool DarkMode { get; set; }
    public string AccentColor { get; set; } = "#E8630A";
    public string FontSize { get; set; } = "medium";
    public bool HighContrast { get; set; }
    public bool ReduceAnimations { get; set; }

    // Privacy
    public string ProfileVisibility { get; set; } = "Public";
    public string WhoCanMessage { get; set; } = "Everyone";
    public bool ShowLocation { get; set; } = true;
    public bool ShowAge { get; set; } = true;
    public bool ShowPets { get; set; } = true;
    public bool ShowOnlineStatus { get; set; } = true;

    // Notifications
    public bool EmailNotifications { get; set; } = true;
    public bool PushNotifications { get; set; } = true;
    public string NotificationFrequency { get; set; } = "Daily";
    public bool DailyJokeNotification { get; set; } = true;
    public bool MealTimeReminders { get; set; } = true;
    public bool QuizReminders { get; set; } = true;
    public bool SocialAlerts { get; set; } = true;
    public bool TrendingAlerts { get; set; } = true;
    public bool CookingClassReminders { get; set; } = true;

    // Units & Language
    public string PreferredLanguage { get; set; } = "en";
    public string PreferredCurrency { get; set; } = "USD";
    public string PreferredUnits { get; set; } = "metric";
    public string TimeZone { get; set; } = "UTC";
}
