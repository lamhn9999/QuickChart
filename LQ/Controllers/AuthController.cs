using LQ.Models.AnalystModels;
using LQ.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using LQ.Models.DTOs;

namespace LQ.Controllers;

[Route("/auth")]
public class AuthController : Controller
{
    private readonly IAnalystService _analystService;

    public AuthController(IAnalystService analystService)
    {
        _analystService = analystService;
    }

    [HttpPost("in")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> SignIn(string aname, string apassword)
    {
        Console.WriteLine("---------------------------------------------------------");
        Analyst? analyst = await _analystService.FindAnalystByName(aname);
        Console.WriteLine(aname);
        Console.WriteLine(apassword);
        if (analyst is null)
        {
            return Json(new { error = "We do not recognize you!" });
        }

        var signInResult = await _analystService.CheckPasswordSignIn(analyst, apassword);
        Console.WriteLine(analyst.ToString() + signInResult.Succeeded);
        if (signInResult.Succeeded)
        {
            HttpContext.Session.SetString("AnalystDTO", JsonSerializer.Serialize(new AnalystDTO{ID = analyst.Id, Name = analyst.UserName}));
            return Json(new { redirect = Url.Action("Show", "Home") });
        }
        return Json(new { error = "We do not recognize you!" });
    }

    [HttpPost("out")]
    [IgnoreAntiforgeryToken]
    public new IActionResult SignOut()
    {
        HttpContext.Session.Clear();
        return Json(new { redirect = Url.Action("Show", "Home") });
    }
    
}