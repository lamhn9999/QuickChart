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

    [HttpGet]
    public async Task<IActionResult> Show()
    {
        HttpContext.Session.Remove("TableDTO");
        HttpContext.Session.Remove("ListValueFilterDTO");
        
        var analystdto = CurrentAnalyst;
        var analystId = analystdto?.ID ?? BaseDTO.DefaultAnalystID;
        var analyst = await _analystService.FindById(analystId);
        
        if (analystdto != null)
        {
            ViewBag.Username = analystdto.Name;
        }

        List<CSVFile> sampleCsvs = await _csvService.GetAllFiles(analyst!);
        
        return CurrentAnalyst is null ? 
            View("Home", sampleCsvs) : 
            View("NewHomeSpecific", sampleCsvs);
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
        var analystdto = CurrentAnalyst;
        var isGuest = analystdto is null;
        var analystId = isGuest ? BaseDTO.DefaultAnalystID : analystdto!.ID;
        var analyst = await _analystService.FindById(analystId);
        if (analyst is null) return NotFound();

        CSVFile selectedFile = await _csvService.GetFile(analyst, filename);
        if (selectedFile is null)
            return NotFound();

        HttpContext.Session.SetString("CSVFileDTO", JsonSerializer.Serialize(new CSVFileDTO{currentFileID = selectedFile.ID}));
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

        return await _csvService.ProcessUploaded(analyst, file)
            ? Json(new { received = file.FileName })
            : Json(new { badcsv = file.FileName });
    }
}