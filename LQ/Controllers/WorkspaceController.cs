using System.Diagnostics;
using LQ.Data.Repositories;
using LQ.Models.TableModels;
using LQ.Services.Implementations;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using LQ.Models.DTOs;
using LQ.Services.Interfaces;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LQ.Controllers;

[Route("/workspace")]
public class WorkspaceController : Controller
{
    private readonly ICSVService _csvService;
    private readonly IAutoChartService _autoChartService;
    private readonly TableRepositoryAbstract _repo;
    private readonly ILogger<WorkspaceController> _logger;
    private readonly Stopwatch _stopwatch = new Stopwatch();

    public WorkspaceController(ICSVService csvService, IAnalystService analystService, IAutoChartService autoChartService, TableRepositoryAbstract repo, ILogger<WorkspaceController> logger)
    {
        _csvService = csvService;
        _autoChartService = autoChartService;
        _repo = repo;
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
        ViewBag.Username = CurrentAnalystDTO?.Name;
        bool awaiting = currentFile.Status == UploadStatus.AwaitingListConfirmation;
        ViewBag.AwaitingListConfirmation = awaiting;
        ViewBag.FileToken = currentFile.PublicToken;
        // Re-serialize the stored payload in camelCase so the @Html.Raw bootstrap
        // produces the same key shape the JS expects (fieldName, samples, etc.).
        // CaseInsensitive on the deserialize side accepts any pre-existing PascalCase rows.
        string? suspiciousJson = null;
        if (awaiting && !string.IsNullOrEmpty(currentFile.ProcessingPayload))
        {
            try
            {
                var list = JsonSerializer.Deserialize<List<SuspiciousListDTO>>(
                    currentFile.ProcessingPayload, CSVService.PayloadJson);
                if (list != null)
                    suspiciousJson = JsonSerializer.Serialize(list, CSVService.PayloadJson);
            }
            catch (JsonException) { /* malformed — leave null, modal won't pop */ }
        }
        ViewBag.SuspiciousJson = suspiciousJson;

        var savedCanvas = await _repo.GetCanvasDocument(currentFile.ID);
        var hydrated = tableDisplay with { SavedCanvasJson = savedCanvas };
        return View("Workspace", hydrated);
    }

    [HttpGet("canvas")]
    public async Task<IActionResult> GetCanvas()
    {
        if (CurrentCSVFileDTO is null) return NotFound();
        var doc = await _repo.GetCanvasDocument(CurrentCSVFileDTO.currentFileID);
        return Content(doc ?? "null", "application/json");
    }

    [HttpPost("canvas")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> SaveCanvas([FromBody] CanvasDTO body)
    {
        if (CurrentCSVFileDTO is null) return NotFound();
        if (string.IsNullOrWhiteSpace(body.Document)) return BadRequest("empty document");
        try
        {
            await _repo.UpsertCanvasDocument(CurrentCSVFileDTO.currentFileID, body.Document);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "canvas save failed");
            return StatusCode(StatusCodes.Status500InternalServerError, "save failed");
        }
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> AutoChart([FromBody] List<int> fieldindexes)
    {
        if (CurrentCSVFileDTO is null || CurrentTableDTO is null) return NotFound();
        // var currentFile = await _csvService.GetFile(CurrentCSVFileDTO.currentFileID);
        try {
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
        catch(Exception ex)
        {
            _logger.LogInformation(ex.Message, ex.StackTrace);
            return StatusCode(StatusCodes.Status500InternalServerError, "Our server is having a hard time dealing with your request!");
        }
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

    [HttpGet("sparkline/{fieldIndex:int}")]
    public async Task<IActionResult> SparklineChart(int fieldIndex)
    {
        if (CurrentCSVFileDTO is null || CurrentTableDTO is null) return NotFound();
        if (fieldIndex < 0 || fieldIndex >= CurrentTableDTO.FieldCount) return BadRequest();
        var chart = await _autoChartService.DetectSingleFieldChartAsync(
            CurrentCSVFileDTO.currentFileID, fieldIndex, CurrentFilters);
        if (chart is null) return NoContent();
        return Json(chart);
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