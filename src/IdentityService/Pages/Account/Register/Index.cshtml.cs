using System.Security.Claims;
using Duende.IdentityModel;
using IdentityService.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IdentityService.Pages.Account.Register;

[SecurityHeaders]
[AllowAnonymous]
public class Index(UserManager<ApplicationUser> userManager, IConfiguration config) : PageModel
{
    [BindProperty] public InputModel Input { get; set; } = null!;
    [BindProperty] public bool RegisterSuccess { get; set; }
    
    public string ClientAppUrl => config["ClientAppUrl"]!;

    public IActionResult OnGet(string? returnUrl)
    {
        Input = new InputModel
        {
            ReturnUrl = returnUrl
        };

        return Page();
    }

    public async Task<IActionResult> OnPost()
    {
        if (Input.Button != "register") return Redirect("~/");

        if (!ModelState.IsValid) return Page();

        var user = new ApplicationUser
        {
            UserName = Input.Username,
            Email = Input.Email,
            EmailConfirmed = true,
        };

        var result = await userManager.CreateAsync(user, Input.Password!);

        if (!result.Succeeded)
        {
            // Identity hatalarini (parola politikasi, kullanici adi zaten var vb.)
            // ekrana bas. Aksi halde kayit basarisiz olsa da "Successfully registered!"
            // gorunuyor ve kullanici login'de "invalid credentials" aliyor.
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            RegisterSuccess = false;

            return Page();
        }

        await userManager.AddClaimsAsync(user,
            [new Claim(JwtClaimTypes.Name, Input.FullName!)]);

        RegisterSuccess = true;

        return Page();
    }
}