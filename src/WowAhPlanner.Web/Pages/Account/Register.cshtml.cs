namespace WowAhPlanner.Web.Pages.Account;

using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WowAhPlanner.Web.Auth;

public sealed class RegisterModel(
    UserManager<AppUser> userManager,
    SignInManager<AppUser> signInManager) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ErrorMessage { get; private set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        if (!ModelState.IsValid)
        {
            ErrorMessage = "Invalid input.";
            return Page();
        }

        if (!string.Equals(Input.Password, Input.ConfirmPassword, StringComparison.Ordinal))
        {
            ErrorMessage = "Passwords do not match.";
            return Page();
        }

        var user = new AppUser
        {
            UserName = Input.Username,
        };

        var result = await userManager.CreateAsync(user, Input.Password);
        if (!result.Succeeded)
        {
            ErrorMessage = string.Join("; ", result.Errors.Select(e => e.Description));
            return Page();
        }

        await signInManager.SignInAsync(user, isPersistent: true);
        return LocalRedirect(returnUrl ?? "/");
    }

    public sealed class InputModel
    {
        [Required]
        public string Username { get; set; } = "";

        [Required]
        public string Password { get; set; } = "";

        [Required]
        public string ConfirmPassword { get; set; } = "";
    }
}

