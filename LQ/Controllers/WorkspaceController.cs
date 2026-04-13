using System.Diagnostics;
using LQ.Data.Repositories;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using LQ.Models.DTOs;
using LQ.Services.Interfaces;

namespace LQ.Controllers;

[Route("/workspace")]
public class WorkspaceController : Controller
{
    private readonly ICSVService _csvService;
    private readonly IAutoChartService _autoChartService;
    private readonly ILogger<WorkspaceController> _logger;
    private readonly Stopwatch _stopwatch = new Stopwatch();

    public WorkspaceController(ICSVService csvService, IAnalystService analystService, IAutoChartService autoChartService, TableRepositoryAbstract repo, ILogger<WorkspaceController> logger)
    {
        _csvService = csvService;
        _autoChartService = autoChartService;
        _logger = logger;
    }

    private void WriteDto(object dto)
    {
        var csdto = JsonSerializer.Serialize(dto);
        Console.WriteLine(csdto);
    }
    
    private AnalystDTO? CurrentAnalystDTO
    {
        get
        {
            var json = HttpContext.Session.GetString("AnalystDTO");
            return string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<AnalystDTO>(json);
        }
    }

    private CSVFileDTO? CurrentCSVFileDTO
    {
        get
        {
            var json = HttpContext.Session.GetString("CSVFileDTO");
            return string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<CSVFileDTO>(json);
        }
    }

    private TableDTO? CurrentTableDTO
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
    public async Task<IActionResult> EnterWorkspace()
    {
        if (CurrentCSVFileDTO is null) return NotFound();
        var currentFile = await _csvService.GetFile(CurrentCSVFileDTO.currentFileID);
        var tableDisplay = await _csvService.GetTableDisplayLimited(currentFile, 200);
        HttpContext.Session.SetString("TableDTO", JsonSerializer.Serialize(new TableDTO
        {
            RowCount = tableDisplay.RowCount,
            FieldCount = tableDisplay.Fields.Count,
        }));
        HttpContext.Session.Remove("ListValueFilterDTO");
        return CurrentAnalystDTO is null
            ? View("Workspace", tableDisplay)
            : View("WorkspaceSpecific", tableDisplay);
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> AutoChart([FromBody] List<int> fieldindexes)
    {
        if (CurrentCSVFileDTO is null || CurrentTableDTO is null) return NotFound();
        var currentFile = await _csvService.GetFile(CurrentCSVFileDTO.currentFileID);
        _stopwatch.Start();
        var returnCharts = CurrentFilters is null
            ? await _autoChartService.DetectChartAsync(CurrentCSVFileDTO.currentFileID, fieldindexes)
            : await _autoChartService.DetectChartWithFiltersAsync(CurrentCSVFileDTO.currentFileID, fieldindexes, CurrentFilters);
        _stopwatch.Stop();
        TimeSpan ts = _stopwatch.Elapsed;
        string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
            ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10);
        _logger.LogInformation($"RunTime {elapsedTime}\n{JsonSerializer.Serialize(fieldindexes)}");
        return Json(returnCharts);
    }

    [HttpPost("filter")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> FilterTable([FromBody] List<ValueFilterDTO> filters)
    {
        if (CurrentCSVFileDTO is null || CurrentTableDTO is null) return NotFound();
        var csvFile = await _csvService.GetFile(CurrentCSVFileDTO.currentFileID);
        var tableDisplay   = await _csvService.GetTableDisplayWithFilterLimited(csvFile, filters, 200);

        WriteDto(filters);

        HttpContext.Session.SetString("ListValueFilterDTO", JsonSerializer.Serialize(filters));
        return Json(tableDisplay);
    }

    [HttpPost("changeFieldType")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> ChangeFieldType(int fieldIndex, string newType, char? listSplitChar)
    {
        if (CurrentCSVFileDTO is null || CurrentTableDTO is null) return NotFound();
        if (fieldIndex < 0 || fieldIndex >= CurrentTableDTO.FieldCount) return BadRequest("Invalid field index");
        try
        {
            var res = await _csvService.ChangeFieldType(await _csvService.GetFile(CurrentCSVFileDTO.currentFileID), fieldIndex, newType, listSplitChar);
            return Json(res);
        }
        catch (Exception ex)
        {
            return Json(ex.Message);
        }
    }
}