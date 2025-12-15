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
    public IActionResult SignIn(string aname)
    {
        Analyst? analyst = _analystService.GetAnalyst(aname);
        if (analyst is null)
        {
            return Json(new { error = "We do not recognize you!" });
        }
        HttpContext.Session.SetString("AnalystDTO", JsonSerializer.Serialize(new AnalystDTO{ID = analyst.ID, Name = analyst.Name}));
        return Json(new { redirect = Url.Action("Show", "Home") });
    }

    [HttpPost("out")]
    [IgnoreAntiforgeryToken]
    public new IActionResult SignOut()
    {
        HttpContext.Session.Clear();
        return Json(new { redirect = Url.Action("Show", "Home") });
    }
}