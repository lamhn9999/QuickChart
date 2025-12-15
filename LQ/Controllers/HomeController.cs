using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using LQ.Models.DTOs;
using LQ.Models.TableModels;
using LQ.Services.Implementations;
using LQ.Services.Interfaces;

namespace LQ.Controllers;

[ApiController]
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
        var json = HttpContext.Session.GetString("AnalystDTO");
        var analystdto = json is null
            ? null
            : JsonSerializer.Deserialize<AnalystDTO>(json);
        List<string> SampleCSVs = analystdto is null
            ? _analystService.GetCSVFiles(BaseDTO.DefaultAnalystID).Select(x => x.Name.ToString()).ToList()
            : _analystService.GetCSVFiles(analystdto.ID).Select(x => x.Name.ToString()).ToList();
        return analystdto is null
            ? View("Home", SampleCSVs)
            : View("HomeSpecific", SampleCSVs);
    }

    [HttpPost("work")]
    public IActionResult EnterWorkspace(string filename)
    {
        var json = HttpContext.Session.GetString("AnalystDTO");
        if (json is null) return NotFound();
        var analystdto = JsonSerializer.Deserialize<AnalystDTO>(json);
        if (analystdto is null) return NotFound();
        Table? table = _csvService.GetTable(analystdto.ID, filename);
        if (table is null) return NotFound();
        else
        {
            HttpContext.Session.SetString("TableDTO", JsonSerializer.Serialize(new TableDTO
            {
                FieldIDs = (from f in table.Columns
                    select f.Field.ID).ToList(),
                RowCount = table.RowCount,
                CSVFileID = table.CSVFile.ID
            }));
            return Ok(JsonSerializer.Serialize(table.GetDisplay()));
        }
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public IActionResult Upload(IFormFile file)
    {
        return Json(new { received = $"{file.FileName}" });
    }
}