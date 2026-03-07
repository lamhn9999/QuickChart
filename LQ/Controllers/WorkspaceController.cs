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
    private ILQDbService _lqDbService;
    
    public WorkspaceController(ICSVService csvService, IAnalystService analystService, IAutoChartService autoChartService, ILQDbService lqDbService)
    {
        _csvService = csvService;
        _analystService =  analystService;
        _autoChartService = autoChartService;
        _lqDbService = lqDbService;
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

    private List<ValueFilterDTO>? CurrentFilters
    {
        get
        {
            var json = HttpContext.Session.GetString("ListValueFilterDTO");
            return string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<List<ValueFilterDTO>>(json);
        }
    }
    
    [HttpGet]
    public IActionResult EnterWorkspace()
    {
        if (CurrentCSVFile is null) return NotFound();
        var table = _csvService.GetTableLimited(CurrentCSVFile.currentFileID, 200);
        if (table is null) return NotFound();
        HttpContext.Session.SetString("TableDTO", JsonSerializer.Serialize(new TableDTO
        {
            FieldIDs = (from f in table.Columns
                select f.Field.ID).ToList(),
            RowCount = table.RowCount,
            CSVFileID = table.CSVFile.ID
        }));
        return CurrentAnalyst is null
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
        return Json(CurrentFilters is null
            ? _autoChartService.DetectChart(tabledto.CSVFileID, fieldindexes)
            : _autoChartService.DetectChart(tabledto.CSVFileID, fieldindexes, CurrentFilters)
        );
    }

    [HttpPost("filter")]
    [IgnoreAntiforgeryToken]
    public IActionResult FilterTable([FromBody] List<ValueFilterDTO> filters)
    {
        string? json = HttpContext.Session.GetString("TableDTO");
        if (json is null) return NotFound();
        TableDTO? tabledto = JsonSerializer.Deserialize<TableDTO>(json);
        if (tabledto is null) return NotFound();
        
        var table = _lqDbService.GetTableWithFilterLimited(tabledto.CSVFileID, filters, 200);
        
        if (table is null) return NotFound();
        HttpContext.Session.SetString("ListValueFilterDTO", JsonSerializer.Serialize(filters));
        
        return Json(table.GetDisplay());
    }
}