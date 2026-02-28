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
        Analyst? analyst = await _analystService.FindAnalystByName(aname);
        if (analyst is null)
        {
            return Json(new { error = "We do not recognize you!" });
        }

        var signInResult = await _analystService.CheckPasswordSignIn(analyst, apassword);
        Console.WriteLine(analyst.ToString() + signInResult.Succeeded);
        if (signInResult.Succeeded && analyst.Id != BaseDTO.DefaultAnalystID)
        {
            Console.WriteLine(analyst.Id);
            
            ViewBag.Username = analyst.UserName;
            HttpContext.Session.SetString("AnalystDTO", JsonSerializer.Serialize(new AnalystDTO{ID = analyst.Id, Name = analyst.UserName}));
            return Json(new { redirect = Url.Action("Show", "Home") });
        }
        return Json(new { error = "We do not recognize you!" });
    }

    [HttpPost("register")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Register([FromForm] RegisterDTO model)
    {
        if (!ModelState.IsValid)
        {
            var validationErrors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage);
            return Json(new { error = "Requirements not met!", details = validationErrors });
        }

        var newAnalyst = Analyst.GiveBirth(model.Name);
        if (newAnalyst == null)
        {
            return Json(new { error = "Invalid user generation." });
        }

        newAnalyst.Email = model.Email;

        var creationResult = await _analystService.AddAnalyst(newAnalyst, model.Password);
        if (creationResult.Succeeded)
        {
            await _analystService.CheckPasswordSignIn(newAnalyst, model.Password);
            HttpContext.Session.SetString("AnalystDTO", JsonSerializer.Serialize(new AnalystDTO { ID = newAnalyst.Id, Name = newAnalyst.UserName }));
            return Json(new { redirect = Url.Action("Show", "Home") });
        }
        var identityErrors = creationResult.Errors.Select(e => e.Description);
        return Json(new { error = "Registration failed.", details = identityErrors });
    }

    [HttpPost("out")]
    [IgnoreAntiforgeryToken]
    public new IActionResult SignOut()
    {
        HttpContext.Session.Clear();
        ViewBag.Username = null;
        return Json(new { redirect = Url.Action("Show", "Home") });
    }
}