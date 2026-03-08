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

    private CSVFileDTO? CurrentCSVFile
    {
        get
        {
            var json = HttpContext.Session.GetString("CSVFileDTO");
            return string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<CSVFileDTO>(json);
        }
    }

    [HttpGet]
    public IActionResult Show()
    {
        HttpContext.Session.Remove("TableDTO");
        
        var analystdto = CurrentAnalyst;
        var analystId = analystdto?.ID ?? BaseDTO.DefaultAnalystID;
        
        if (analystdto != null)
        {
            ViewBag.Username = analystdto.Name;
        }
        
        List<CSVFile> sampleCsvs = _analystService.GetCSVFiles(analystId).ToList();
        
        return CurrentAnalyst is null ? 
            View("Home", sampleCsvs) : 
            View("NewHomeSpecific", sampleCsvs);
    }

    [HttpDelete("remove")]
    [IgnoreAntiforgeryToken]
    public IActionResult Remove(string filename)
    {
        var analystdto = CurrentAnalyst;
        if (analystdto is null) return Json("NG");

        try
        {
            _csvService.RemoveCSVFile(analystdto.ID, filename);
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
    public IActionResult RenameFile(string oldFileName, string newFileName)
    {
        var analystdto = CurrentAnalyst;
        if (analystdto is null) return Json("Cannot perform action: analyst not signed in");
        
        try
        {
            _csvService.RenameCSVFile(analystdto.ID, oldFileName, newFileName);
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
    public IActionResult Select(string filename)
    {
        var analystdto = CurrentAnalyst;
        var isGuest = analystdto is null;
        var analystId = isGuest ? BaseDTO.DefaultAnalystID : analystdto!.ID;

        CSVFile? selectedFile = _csvService.GetCSVFile(analystId, filename);
        if (selectedFile is null)
            return NotFound();
        
        HttpContext.Session.SetString("CSVFileDTO", JsonSerializer.Serialize(new CSVFileDTO{currentFileID = selectedFile.ID}));
        HttpContext.Session.Remove("ListValueFilterDTO");
        if (isGuest)
        {
            Table? table = _csvService.GetTableLimited(selectedFile.ID, 200);
            if (table is null) return NotFound();
            return Json(table.GetDisplay());
        }
        
        return Ok();
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Upload(IFormFile file)
    {
        var analystdto = CurrentAnalyst;
        if (analystdto is null) return Unauthorized(new { error = "Sign in required to upload files" });

        Analyst? analyst = await _analystService.FindAnalystById(analystdto.ID);
        if (analyst is null) return NotFound(new { error = "Analyst not found" });

        return await _csvService.ProcessUploadedCSV(analyst, file) 
            ? Json(new { received = file.FileName })
            : Json(new { badcsv = file.FileName });
    }
}