using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using LQ.Models.AnalystModels;
using LQ.Models.DTOs;
using LQ.Models.TableModels;
using LQ.Services.Interfaces;

namespace LQ.Controllers;

[Route("/workspace")]
public class WorkspaceController : Controller
{
    private ICSVService _csvService;
    private IAnalystService _analystService;
    private IAutoChartService _autoChartService;
    
    public WorkspaceController(ICSVService csvService, IAnalystService analystService, IAutoChartService autoChartService)
    {
        _csvService = csvService;
        _analystService =  analystService;
        _autoChartService = autoChartService;
    }
    
    [HttpGet]
    public IActionResult EnterWorkspace()
    {
        string? json = HttpContext.Session.GetString("AnalystDTO");
        AnalystDTO? analystdto = json is null
            ? null
            : JsonSerializer.Deserialize<AnalystDTO>(json);
    
        // json = HttpContext.Session.GetString("TableDTO");
        // if (json is null) return NotFound();
        if (analystdto is null) return NotFound();
        
        Table? table = analystdto is null
            ? _csvService.GetTable(BaseDTO.DefaultAnalystID, analystdto.currentFileID ?? -1)
            : _csvService.GetTable(analystdto.ID, analystdto.currentFileID ?? -1);
        if (table is null) return NotFound();
        HttpContext.Session.SetString("TableDTO", JsonSerializer.Serialize(new TableDTO
        {
            FieldIDs = (from f in table.Columns
                select f.Field.ID).ToList(),
            RowCount = table.RowCount,
            CSVFileID = table.CSVFile.ID
        }));
        return analystdto is null
            ? View("Workspace", table)
            : View("WorkspaceSpecific", table);
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public IActionResult AutoChart([FromBody] List<int> fieldindexes)
    {
        string? json = HttpContext.Session.GetString("TableDTO");
        if (json is null) return NotFound();
        TableDTO? tabledto = JsonSerializer.Deserialize<TableDTO>(json);
        if (tabledto is null) return NotFound();
        return Json(_autoChartService.DetectChart(tabledto.CSVFileID, fieldindexes));
    }
}