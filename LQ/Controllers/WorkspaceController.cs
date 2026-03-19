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

    private void WriteDto(object dto)
    {
        var csdto = JsonSerializer.Serialize(dto);
        Console.WriteLine(csdto);
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

    private TableDTO? CurrentTable
    {
        get
        {
            var json = HttpContext.Session.GetString("TableDTO");
            return string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<TableDTO>(json);
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
            FieldIDs = table.Columns.OrderBy(f => f.Field.Index).Select(f => f.Field.ID).ToList(),
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
        if (CurrentTable is null) return NotFound();
        _stopwatch.Start();
        var returnCharts = CurrentFilters is null
            ? await _autoChartService.DetectChartAsync(CurrentTable.CSVFileID, fieldindexes)
            : _autoChartService.DetectChart(CurrentTable.CSVFileID, fieldindexes, CurrentFilters);
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
        if (CurrentTable is null) return NotFound();
        var table = _lqDbService.GetTableWithFilterLimited(CurrentTable.CSVFileID, filters, 200);
        
        WriteDto(filters);
        
        if (table is null) return BadRequest("Bad filter request.");
        HttpContext.Session.SetString("ListValueFilterDTO", JsonSerializer.Serialize(filters));

        return Json(table.GetDisplay());
    }

    [HttpPost("changeFieldType")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> ChangeFieldType(int fieldIndex, string newType)
    {
        if (CurrentCSVFile is null || CurrentTable is null) return NotFound();
        
        if (fieldIndex < 0 || fieldIndex >= CurrentTable.FieldIDs.Count) return BadRequest("Invalid field index");
        int fieldId = CurrentTable.FieldIDs[fieldIndex];

        var res = await _csvService.ChangeFieldType(CurrentCSVFile.currentFileID, fieldId, newType);
        if (res) return Json("Field updated");
        return Json("Field not updated");
    }
}