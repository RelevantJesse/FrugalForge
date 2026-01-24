namespace WowAhPlanner.Web.Pages.Account;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WowAhPlanner.Web.Auth;

public sealed class LogoutModel(SignInManager<AppUser> signInManager) : PageModel
{
    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await signInManager.SignOutAsync();
        return LocalRedirect("/");
    }
}

