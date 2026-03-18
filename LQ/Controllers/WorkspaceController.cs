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
    private readonly ILogger<WorkspaceController> _logger;
    private Stopwatch _stopwatch = new Stopwatch();
    
    public WorkspaceController(ICSVService csvService, IAnalystService analystService, IAutoChartService autoChartService, ILQDbService lqDbService, ILogger<WorkspaceController> logger)
    {
        _csvService = csvService;
        _analystService =  analystService;
        _autoChartService = autoChartService;
        _lqDbService = lqDbService;
        _logger = logger;
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
        HttpContext.Session.Remove("ListValueFilterDTO");
        return CurrentAnalyst is null
            ? View("Workspace", table)
            : View("WorkspaceSpecific", table);
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> AutoChart([FromBody] List<int> fieldindexes)
    {
        string? json = HttpContext.Session.GetString("TableDTO");
        if (json is null) return NotFound();
        TableDTO? tabledto = JsonSerializer.Deserialize<TableDTO>(json);
        if (tabledto is null) return NotFound();
        
        _stopwatch.Start();
        var returnCharts = CurrentFilters is null
            ? await _autoChartService.DetectChartAsync(tabledto.CSVFileID, fieldindexes)
            : _autoChartService.DetectChart(tabledto.CSVFileID, fieldindexes, CurrentFilters);
        _stopwatch.Stop();
        TimeSpan ts = _stopwatch.Elapsed;
        string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
            ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10);
        _logger.LogInformation($"RunTime {elapsedTime}\n{JsonSerializer.Serialize(fieldindexes)}");
        return Json(returnCharts);
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

        // var filtersjson = JsonSerializer.Serialize(filters);
        // Console.WriteLine(filtersjson);
        // var tablejson = JsonSerializer.Serialize(table.GetDisplay());
        // Console.WriteLine(tablejson);
        return Json(table.GetDisplay());
    }

    [HttpPost("changeFieldType")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> ChangeFieldType(int fieldIndex, string newType)
    {
        if (CurrentCSVFile is null) return NotFound();
        var res = await _csvService.ChangeFieldType(CurrentCSVFile.currentFileID, fieldIndex, newType);
        if (res) return Json("Field updated");
        return Json("Field not updated");
    }
}