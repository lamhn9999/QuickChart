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
    private ICSVService _csvService;
    private IAnalystService _analystService;
    public HomeController(ICSVService csvService, IAnalystService analystService)
    {
        _csvService = csvService;
        _analystService =  analystService;
    }

    [HttpGet]
    public IActionResult Show()
    {
        HttpContext.Session.Remove("TableDTO");
        var json = HttpContext.Session.GetString("AnalystDTO");
        var analystdto = json is null
            ? null
            : JsonSerializer.Deserialize<AnalystDTO>(json);
        List<string> sampleCsvs = analystdto is null
            ? _analystService.GetCSVFiles(BaseDTO.DefaultAnalystID).Select(x => x.Name.ToString()).ToList()
            : _analystService.GetCSVFiles(analystdto.ID).Select(x => x.Name.ToString()).ToList();
        return analystdto is null
            ? View("Home", sampleCsvs)
            : View("HomeSpecific", sampleCsvs);
    }

    [HttpPost("table")]
    [IgnoreAntiforgeryToken]
    public IActionResult Select(string filename)
    {
        var json = HttpContext.Session.GetString("AnalystDTO");
        var analystdto = json is null
            ? null
            : JsonSerializer.Deserialize<AnalystDTO>(json);
        Table? table = analystdto is null
            ? _csvService.GetTable(BaseDTO.DefaultAnalystID, filename)
            : _csvService.GetTable(analystdto.ID, filename);
        if (table is null) return NotFound();
        
        HttpContext.Session.SetString("TableDTO", JsonSerializer.Serialize(new TableDTO
        {
            FieldIDs = (from f in table.Columns
                select f.Field.ID).ToList(),
            RowCount = table.RowCount,
            CSVFileID = table.CSVFile.ID
        }));
        
        return Json(table.GetDisplay());
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public IActionResult Upload(IFormFile file)
    {
        string? json = HttpContext.Session.GetString("AnalystDTO");
        if (json is null) return NotFound();
        AnalystDTO? analystdto = JsonSerializer.Deserialize<AnalystDTO>(json);
        if (analystdto is null) return NotFound();
        Analyst? analyst = _analystService.GetAnalyst(analystdto.ID);
        if (analyst is null) return NotFound();
        return _csvService.ProcessUploadedCSV(analyst, file) 
            ? Json(new { received = $"{file.FileName}" })
            : Json(new { badcsv = $"{file.FileName}"});
    }
}