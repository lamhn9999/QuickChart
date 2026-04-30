using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using LQ.Models.AnalystModels;
using LQ.Models.DTOs;
using LQ.Models.TableModels;
using LQ.Services.Interfaces;

namespace LQ.Controllers;

[Route("/")]
public class HomeController : Controller
{
    private readonly ICSVService _csvService;
    private readonly IAnalystService _analystService;

    public HomeController(ICSVService csvService, IAnalystService analystService)
    {
        _csvService = csvService;
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

    private CSVFileDTO? CurrentFile
    {
        get
        {
            var json = HttpContext.Session.GetString("CSVFileDTO");
            return string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<CSVFileDTO>(json);
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
    public async Task<IActionResult> Show()
    {
        HttpContext.Session.Remove("TableDTO");
        HttpContext.Session.Remove("ListValueFilterDTO");

        var analystdto = CurrentAnalyst;
        if (analystdto != null)
        {
            var analyst = await _analystService.FindById(analystdto.ID);
            ViewBag.Username = analystdto.Name;
            return View("NewHomeSpecific", await _csvService.GetAllFiles(analyst!));
        }

        var token = EnsureGuestToken();
        return View("Home", await _csvService.GetAllFilesGuest(token));
    }

    [HttpDelete("remove")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Remove(string filename)
    {
        var analystdto = CurrentAnalyst;
        if (analystdto is null) return Json("NG");
        var analyst = await _analystService.FindById(analystdto.ID);

        try
        {
            await _csvService.RemoveFile(analyst!, filename);
            return Json("OK");
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return Json("NG");
        }
    }

    [HttpPut("renamefile")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> RenameFile(string oldFileName, string newFileName)
    {
        var analystdto = CurrentAnalyst;
        if (analystdto is null) return Json("Cannot perform action: analyst not signed in");
        var analyst = await _analystService.FindById(analystdto.ID);
        
        try
        {
            await _csvService.RenameFile(analyst!, oldFileName, newFileName);
            return Json("OK");
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return Json("NG");
        }
    }

    [HttpPost("table")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Select(string filename)
    {
        CSVFile selectedFile;
        var analystdto = CurrentAnalyst;
        if (analystdto is not null)
        {
            var analyst = await _analystService.FindById(analystdto.ID);
            if (analyst is null) return NotFound();
            selectedFile = await _csvService.GetFile(analyst, filename);
        }
        else
        {
            var token = GuestToken;
            if (token is null) return Unauthorized();
            selectedFile = await _csvService.GetFileGuest(token, filename);
        }
        if (selectedFile is null) return NotFound();

        HttpContext.Session.SetString("CSVFileDTO", JsonSerializer.Serialize(new CSVFileDTO { currentFileID = selectedFile.ID }));
        HttpContext.Session.Remove("ListValueFilterDTO");
        return Ok();
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    [RequestSizeLimit(209715200)]
    public async Task<IActionResult> Upload(IFormFile file)
    {
        var analystdto = CurrentAnalyst;
        if (analystdto is null) return Unauthorized(new { error = "Sign in required to upload files" });

        Analyst? analyst = await _analystService.FindById(analystdto.ID);
        if (analyst is null) return NotFound(new { error = "Analyst not found" });

        var result = await _csvService.ProcessUploaded(analyst, file);
        if (result is null) return Json(new { badcsv = file.FileName });
        return Json(new { received = result.FileName, token = result.Token, suspicious = result.SuspiciousLists });
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

    [HttpDelete("guest-remove")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> GuestRemove(string filename)
    {
        var token = GuestToken;
        if (token is null) return Unauthorized();
        try
        {
            await _csvService.RemoveFileGuest(token, filename);
            return Json("OK");
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return Json("NG");
        }
    }
}