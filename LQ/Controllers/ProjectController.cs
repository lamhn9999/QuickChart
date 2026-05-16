using System.Text.Json;
using LQ.Models.DTOs;
using LQ.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace LQ.Controllers;

[Route("/projects")]
public class ProjectController : Controller
{
    private readonly IProjectService _projects;
    private readonly IAnalystService _analystService;

    public ProjectController(IProjectService projects, IAnalystService analystService)
    {
        _projects = projects;
        _analystService = analystService;
    }

    private AnalystDTO? CurrentAnalyst
    {
        get
        {
            var json = HttpContext.Session.GetString("AnalystDTO");
            return string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<AnalystDTO>(json);
        }
    }

    // Must match HomeController so a single guest stays the same identity
    // across the home redirect and the project pages.
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

    // Owner identity — never bind these two together: analyst beats guest if
    // both happen to be present (the cookie may still be lying around after a
    // sign-in). The service only needs one source of truth.
    private (string? analystId, string? guestToken) OwnerIdentity()
    {
        var aid = CurrentAnalyst?.ID;
        if (aid is not null) return (aid, null);
        return (null, EnsureGuestToken());
    }

    // ─── List ─────────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var (analystId, guestToken) = OwnerIdentity();
        ViewBag.Username = CurrentAnalyst?.Name;
        var items = await _projects.List(analystId, guestToken);
        return View("Index", items);
    }

    [HttpGet("api")]
    public async Task<IActionResult> ListJson()
    {
        var (analystId, guestToken) = OwnerIdentity();
        return Json(await _projects.List(analystId, guestToken));
    }

    // ─── Detail ───────────────────────────────────────────────────────────────

    [HttpGet("{token:guid}")]
    public async Task<IActionResult> Detail(Guid token)
    {
        var (analystId, guestToken) = OwnerIdentity();
        var detail = await _projects.GetByToken(token, analystId, guestToken);
        if (detail is null) return NotFound();
        ViewBag.Username = CurrentAnalyst?.Name;
        return View("Detail", detail);
    }

    [HttpGet("{token:guid}/api")]
    public async Task<IActionResult> DetailJson(Guid token)
    {
        var (analystId, guestToken) = OwnerIdentity();
        var detail = await _projects.GetByToken(token, analystId, guestToken);
        return detail is null ? NotFound() : Json(detail);
    }

    // ─── Create / Rename / Delete project ─────────────────────────────────────

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Create([FromBody] CreateProjectRequestDTO body)
    {
        var (analystId, guestToken) = OwnerIdentity();
        var dto = await _projects.Create(body?.Name ?? "Untitled project", analystId, guestToken);
        return Json(dto);
    }

    [HttpPut("{token:guid}")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Rename(Guid token, [FromBody] RenameProjectRequestDTO body)
    {
        var (analystId, guestToken) = OwnerIdentity();
        return await _projects.Rename(token, body?.Name ?? "", analystId, guestToken)
            ? Ok() : NotFound();
    }

    [HttpDelete("{token:guid}")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Delete(Guid token)
    {
        var (analystId, guestToken) = OwnerIdentity();
        return await _projects.Delete(token, analystId, guestToken) ? Ok() : NotFound();
    }

    // ─── Upload CSV into project ──────────────────────────────────────────────

    [HttpPost("{token:guid}/upload")]
    [IgnoreAntiforgeryToken]
    [RequestSizeLimit(209715200)]
    public async Task<IActionResult> UploadCSV(Guid token, IFormFile file)
    {
        if (file is null) return BadRequest(new { error = "Missing file" });
        var (analystId, guestToken) = OwnerIdentity();
        var result = await _projects.UploadCSV(token, file, analystId, guestToken);
        if (result is null) return Json(new { badcsv = file.FileName });
        return Json(new { received = result.FileName, token = result.Token, suspicious = result.SuspiciousLists });
    }

    // ─── Create / Rename / Delete canvas inside a project ─────────────────────

    [HttpPost("{token:guid}/canvases")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> CreateCanvas(Guid token, [FromBody] CreateCanvasRequestDTO? body)
    {
        var (analystId, guestToken) = OwnerIdentity();
        var dto = await _projects.CreateCanvas(token, body?.Name, analystId, guestToken);
        return dto is null ? NotFound() : Json(dto);
    }

    [HttpPut("canvases/{canvasToken:guid}")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> RenameCanvas(Guid canvasToken, [FromBody] RenameCanvasRequestDTO body)
    {
        var (analystId, guestToken) = OwnerIdentity();
        return await _projects.RenameCanvas(canvasToken, body?.Name ?? "", analystId, guestToken)
            ? Ok() : NotFound();
    }

    [HttpDelete("canvases/{canvasToken:guid}")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> DeleteCanvas(Guid canvasToken)
    {
        var (analystId, guestToken) = OwnerIdentity();
        return await _projects.DeleteCanvas(canvasToken, analystId, guestToken) ? Ok() : NotFound();
    }
}
