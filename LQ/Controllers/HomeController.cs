using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using LQ.Models.DTOs;
using LQ.Services.Interfaces;

namespace LQ.Controllers;

[Route("/")]
public class HomeController : Controller
{
    private readonly ICSVService _csvService;

    public HomeController(ICSVService csvService)
    {
        _csvService = csvService;
    }

    private AnalystDTO? CurrentAnalyst
    {
        get
        {
            var json = HttpContext.Session.GetString("AnalystDTO");
            return string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<AnalystDTO>(json);
        }
    }

    private const string GuestCookieName = "lq_guest";

    private string? GuestToken =>
        Request.Cookies.TryGetValue(GuestCookieName, out var raw) && Guid.TryParse(raw, out _)
            ? raw : null;

    private string EnsureGuestToken()
    {
        var existing = GuestToken;
        if (existing is not null) return existing;
        var token = Guid.NewGuid().ToString();
        Response.Cookies.Append(GuestCookieName, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = false,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddDays(30),
            IsEssential = true
        });
        return token;
    }

    [HttpGet]
    public IActionResult Show()
    {
        // Signed-in users skip the marketing page and go straight to their
        // projects (the new home — Phase 3). Anonymous visitors still need
        // somewhere to land that exposes Sign In / Register, so we render the
        // existing public landing view for them. The empty model lets us
        // skip seeding a guest cookie just by visiting "/".
        HttpContext.Session.Remove("TableDTO");
        HttpContext.Session.Remove("ListValueFilterDTO");
        if (CurrentAnalyst is not null) return Redirect("/projects");
        return View("Home", new List<LQ.Models.TableModels.CSVFile>());
    }

    [HttpPost("guest-upload")]
    [IgnoreAntiforgeryToken]
    [RequestSizeLimit(209715200)]
    public async Task<IActionResult> GuestUpload(IFormFile file)
    {
        if (CurrentAnalyst is not null) return BadRequest();
        var token = EnsureGuestToken();
        var result = await _csvService.ProcessUploadedGuest(token, file);
        if (result is null) return Json(new { badcsv = file.FileName });
        return Json(new { received = result.FileName, token = result.Token, suspicious = result.SuspiciousLists });
    }

    [HttpPost("confirm-list-fields")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> ConfirmListFields([FromBody] ConfirmListRequestDTO dto)
    {
        var ok = await _csvService.ConfirmListFields(
            dto.Token, CurrentAnalyst?.ID, GuestToken, dto.Confirmed);
        return ok ? Ok() : Forbid();
    }

    [HttpGet("pending-uploads")]
    public async Task<IActionResult> PendingUploads()
    {
        var pending = await _csvService.GetPendingUploads(CurrentAnalyst?.ID, GuestToken);
        return Json(pending);
    }
}