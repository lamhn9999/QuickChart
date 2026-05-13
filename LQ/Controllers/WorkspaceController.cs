using System.Diagnostics;
using LQ.Data.Repositories;
using LQ.Models.TableModels;
using LQ.Services;
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
    private readonly IAnalystService _analystService;
    private readonly IAutoChartService _autoChartService;
    private readonly TableRepositoryAbstract _repo;
    private readonly SparklineCancelService _sparklineCancel;
    private readonly ILogger<WorkspaceController> _logger;
    private readonly Stopwatch _stopwatch = new Stopwatch();
    private static readonly JsonSerializerOptions _camelCaseJson = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public WorkspaceController(ICSVService csvService, IAnalystService analystService, IAutoChartService autoChartService, TableRepositoryAbstract repo, SparklineCancelService sparklineCancel, ILogger<WorkspaceController> logger)
    {
        _csvService = csvService;
        _analystService = analystService;
        _autoChartService = autoChartService;
        _repo = repo;
        _sparklineCancel = sparklineCancel;
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

    private const string GuestCookieName = "lq_guest";

    private string? GuestToken =>
        Request.Cookies.TryGetValue(GuestCookieName, out var raw) && Guid.TryParse(raw, out _)
            ? raw : null;

    // Resolves a public file token (Guid, safe to expose) to the internal integer
    // CSVFile.ID, but only if the current analyst owns the file or the guest cookie
    // matches its GuestToken. Returns null in all other cases (not found OR not yours)
    // so the caller can respond identically and avoid leaking which Guids exist.
    private async Task<int?> ResolveAccessibleFileId(Guid token)
    {
        var file = await _repo.GetFileByPublicToken(token);
        if (file is null) return null;
        var analystId = CurrentAnalystDTO?.ID;
        if (analystId != null && file.AnalystID == analystId) return file.ID;
        var guest = GuestToken;
        if (guest != null && file.GuestToken == guest) return file.ID;
        return null;
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

        // Reject saves that embed chart blocks pointing at CSVFiles the current
        // user can't access. This is the server-side gate for cross-file charts:
        // any csvFileToken in the document must resolve to an accessible file.
        if (!await ChartTokensAreAccessible(body.Document)) return Forbid();

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

    // Walks the canvas JSON and returns false if any chart block carries a
    // csvFileToken the current user cannot access. Malformed documents are
    // treated as inaccessible (caller will Forbid) — the front-end always
    // serializes a well-formed shape, so anything else is suspect.
    private async Task<bool> ChartTokensAreAccessible(string documentJson)
    {
        JsonDocument doc;
        try { doc = JsonDocument.Parse(documentJson); }
        catch (JsonException) { return false; }

        using (doc)
        {
            if (!doc.RootElement.TryGetProperty("blocks", out var blocks)
                || blocks.ValueKind != JsonValueKind.Array)
                return true; // no blocks → nothing to validate

            foreach (var block in blocks.EnumerateArray())
            {
                if (!block.TryGetProperty("kind", out var kind)
                    || kind.GetString() != "chart") continue;
                if (!block.TryGetProperty("csvFileToken", out var tokenEl)
                    || tokenEl.ValueKind != JsonValueKind.String) continue; // implicit = canvas owner, fine
                if (!Guid.TryParse(tokenEl.GetString(), out var token)) return false;
                if (await ResolveAccessibleFileId(token) is null) return false;
            }
        }
        return true;
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> AutoChart([FromBody] AutoChartRequestDTO body)
    {
        if (body is null || body.FieldIndexes is null) return BadRequest();

        // Resolve the source file. Foreign path: resolve token via ownership check.
        // Native path: fall back to the session's currently-open workspace file.
        int sourceFileId;
        bool isNative;
        if (body.CsvFileToken is Guid token)
        {
            var resolved = await ResolveAccessibleFileId(token);
            if (resolved is null) return Forbid();
            sourceFileId = resolved.Value;
            isNative = CurrentCSVFileDTO?.currentFileID == sourceFileId;
        }
        else
        {
            if (CurrentCSVFileDTO is null || CurrentTableDTO is null) return NotFound();
            sourceFileId = CurrentCSVFileDTO.currentFileID;
            isNative = true;
        }

        // Filters are session-scoped to the active workspace's file. Apply them
        // only when the chart actually targets that file; foreign-file charts
        // would be filtered against a totally unrelated set of rows.
        var filters = isNative ? CurrentFilters : null;

        try
        {
            var sw = Stopwatch.StartNew();
            var returnCharts = filters is null
                ? await _autoChartService.DetectChartAsync(sourceFileId, body.FieldIndexes)
                : await _autoChartService.DetectChartWithFiltersAsync(sourceFileId, body.FieldIndexes, filters);
            sw.Stop();
            var ts = sw.Elapsed;
            string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10);
            _logger.LogInformation($"RunTime {elapsedTime}\n{JsonSerializer.Serialize(body.FieldIndexes)}");
            return Json(returnCharts);
        }
        catch (Exception ex)
        {
            _logger.LogInformation(ex.Message, ex.StackTrace);
            return StatusCode(StatusCodes.Status500InternalServerError, "Our server is having a hard time dealing with your request!");
        }
    }

    // Per-chart filter endpoint. Mirrors AutoChart() but takes its filter list
    // inline from the request body instead of reading the session-scoped
    // ListValueFilterDTO. This lets a single chart card refresh against a
    // request-scoped filter without disturbing the table-wide filter state or
    // any other chart on the canvas. The whole-table filter is intentionally
    // NOT merged in here — per-chart filters are independent.
    [HttpPost("chart")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> ChartWithInlineFilters([FromBody] PerChartRequestDTO body)
    {
        if (body is null || body.FieldIndexes is null) return BadRequest();

        int sourceFileId;
        if (body.CsvFileToken is Guid token)
        {
            var resolved = await ResolveAccessibleFileId(token);
            if (resolved is null) return Forbid();
            sourceFileId = resolved.Value;
        }
        else
        {
            if (CurrentCSVFileDTO is null || CurrentTableDTO is null) return NotFound();
            sourceFileId = CurrentCSVFileDTO.currentFileID;
        }

        var filters = body.InlineFilters;

        try
        {
            var sw = Stopwatch.StartNew();
            var charts = filters is { Count: > 0 }
                ? await _autoChartService.DetectChartWithFiltersAsync(sourceFileId, body.FieldIndexes, filters)
                : await _autoChartService.DetectChartAsync(sourceFileId, body.FieldIndexes);
            sw.Stop();
            _logger.LogInformation(
                "PerChartFilter elapsed={Elapsed}ms fields={Fields} filters={FilterCount}",
                sw.ElapsedMilliseconds, JsonSerializer.Serialize(body.FieldIndexes), filters?.Count ?? 0);
            return Json(charts);
        }
        catch (Exception ex)
        {
            _logger.LogInformation(ex.Message, ex.StackTrace);
            return StatusCode(StatusCodes.Status500InternalServerError,
                "Our server is having a hard time dealing with your request!");
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

    // Per-chart filter support: returns the distinct-value list for a single field
    // when that list is small enough to fit in a button-row UI, plus the total
    // distinct count and whether the field is classified as categorical. The
    // frontend uses this to choose between a button-list filter card variant and
    // a search-bar variant. We never expose Field.ID — fieldIndex is the public
    // handle, identical to the convention used for sparklines / changeFieldType.
    //
    // `csvFileToken` is optional. Without it, the lookup runs against the
    // session's current file (the typical "filter for a chart on my own canvas"
    // case). With it, the lookup runs against the token's file — required when
    // a filter card was copied from another file: its fieldIndex is meaningful
    // only against THAT file's schema, not the canvas owner's. The token must
    // resolve to a file the current user can access.
    [HttpGet("field/{fieldIndex:int}/distinct")]
    public async Task<IActionResult> GetFieldDistinct(int fieldIndex,
        [FromQuery] int limit = 8,
        [FromQuery] Guid? csvFileToken = null)
    {
        int sourceFileId;
        if (csvFileToken is Guid token)
        {
            var resolved = await ResolveAccessibleFileId(token);
            if (resolved is null) return NotFound();
            sourceFileId = resolved.Value;
        }
        else
        {
            if (CurrentCSVFileDTO is null || CurrentTableDTO is null) return NotFound();
            if (fieldIndex < 0 || fieldIndex >= CurrentTableDTO.FieldCount) return BadRequest();
            sourceFileId = CurrentCSVFileDTO.currentFileID;
        }

        var file   = await _csvService.GetFile(sourceFileId);
        var fields = await _repo.GetFields(file, [fieldIndex]);
        if (fields.Count == 0) return NotFound();
        var field = fields[0];

        int distinctCount = await _repo.CountDistinct(field);
        // Materialise the value list if it fits within the limit. For very high limits
        // (e.g. Infinity from unlimited frontend), fetch all values.
        List<string?> values;
        if (limit >= 1000000 || distinctCount <= limit)
        {
            var grouped = await _repo.CountByGroup(field);
            values = grouped.Select(r => r.Key?.ToString()).ToList();
        }
        else
        {
            values = new List<string?>();
        }

        return Json(new
        {
            distinctCount,
            values,
            isCategorical = field.Categories.Contains(Category.Categorical),
            typeName      = field.TypeName,
            fieldName     = field.Name,
        });
    }

    [HttpGet("sparkline/{fieldIndex:int}")]
    public async Task<IActionResult> SparklineChart(int fieldIndex)
    {
        if (CurrentCSVFileDTO is null || CurrentTableDTO is null) return NotFound();
        if (fieldIndex < 0 || fieldIndex >= CurrentTableDTO.FieldCount) return BadRequest();

        var cached = await _repo.GetSparklineJsonAsync(CurrentCSVFileDTO.currentFileID, fieldIndex);
        if (cached is not null && cached.Contains("\"chartType\"")) return Content(cached, "application/json");

        var field = await _repo.GetField(CurrentCSVFileDTO.currentFileID, fieldIndex);
        var ct = _sparklineCancel.Register(CurrentCSVFileDTO.currentFileID, fieldIndex);
        try
        {
            var chart = await _autoChartService.DetectSingleFieldChartAsync(
                CurrentCSVFileDTO.currentFileID, fieldIndex, null, ct);
            if (chart is null) return NoContent();

            var json = JsonSerializer.Serialize(chart, _camelCaseJson);
            await _repo.SetSparklineJsonAsync(
                CurrentCSVFileDTO.currentFileID, fieldIndex, json, field.TypeName ?? "");
            return Content(json, "application/json");
        }
        catch (OperationCanceledException)
        {
            return NoContent();
        }
        finally
        {
            _sparklineCancel.Unregister(CurrentCSVFileDTO.currentFileID, fieldIndex);
        }
    }

    [HttpPost("changeFieldType")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> ChangeFieldType(int fieldIndex, string newType, char? listSplitChar)
    {
        if (CurrentCSVFileDTO is null || CurrentTableDTO is null) return NotFound();
        if (fieldIndex < 0 || fieldIndex >= CurrentTableDTO.FieldCount) return BadRequest("Invalid field index");
        try
        {
            _sparklineCancel.Cancel(CurrentCSVFileDTO.currentFileID, fieldIndex);
            var res = await _csvService.ChangeFieldType(await _csvService.GetFile(CurrentCSVFileDTO.currentFileID), fieldIndex, newType, listSplitChar);
            return Json(res);
        }
        catch (Exception ex)
        {
            return Json(ex.Message);
        }
    }

    public record ChangeCategoriesRequest(int FieldIndex, List<Category> Categories);

    [HttpPost("changeCategories")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> ChangeCategories([FromBody] ChangeCategoriesRequest req)
    {
        if (CurrentCSVFileDTO is null || CurrentTableDTO is null) return NotFound();
        if (req.FieldIndex < 0 || req.FieldIndex >= CurrentTableDTO.FieldCount) return BadRequest("Invalid field index");
        try
        {
            var csv = await _csvService.GetFile(CurrentCSVFileDTO.currentFileID);
            var res = await _csvService.ChangeFieldCategories(csv, req.FieldIndex, req.Categories ?? new List<Category>());
            return Json(res);
        }
        catch (Exception ex)
        {
            return Json(ex.Message);
        }
    }

    // Lists the current user's other CSVFiles (excluding the active workspace's
    // own file) for the "Copy chart to file…" picker. Returns public Guid tokens
    // and display names — the integer CSVFile.ID is never exposed.
    [HttpGet("files-for-copy")]
    public async Task<IActionResult> FilesForCopy()
    {
        if (CurrentCSVFileDTO is null) return NotFound();
        var currentId = CurrentCSVFileDTO.currentFileID;

        List<CSVFile> files;
        var analystdto = CurrentAnalystDTO;
        if (analystdto != null)
        {
            var analyst = await _analystService.FindById(analystdto.ID);
            files = analyst is null ? new List<CSVFile>() : await _repo.GetAllFiles(analyst);
        }
        else
        {
            var guest = GuestToken;
            files = guest is null ? new List<CSVFile>() : await _repo.GetAllFiles(guest);
        }

        var result = files
            .Where(f => f.ID != currentId)
            .OrderBy(f => f.Name)
            .Select(f => new { token = f.PublicToken, name = f.Name })
            .ToList();
        return Json(result);
    }

    // Appends a chart spec to the target file's canvas. The source token is
    // baked into the chart block as `csvFileToken` so the chart still renders
    // against the original data after it lands in the new canvas. Both tokens
    // (target and source) must resolve to files the current user can access.
    [HttpPost("canvas/append-chart")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> AppendChart([FromBody] AppendChartRequestDTO body)
    {
        if (body is null) return BadRequest();
        if (body.ChartSpec is null || body.ChartSpec.FieldIndexes is null
            || body.ChartSpec.FieldIndexes.Count == 0) return BadRequest("invalid chart spec");

        var targetId = await ResolveAccessibleFileId(body.TargetFileToken);
        if (targetId is null) return Forbid();

        var sourceId = await ResolveAccessibleFileId(body.ChartSpec.CsvFileToken);
        if (sourceId is null) return Forbid();

        // Read existing canvas (or start a fresh empty doc), append the block,
        // upsert. We always carry csvFileToken on the appended block, even when
        // source == target, so the block remains self-describing if the canvas
        // is later moved or copied again.
        var existing = await _repo.GetCanvasDocument(targetId.Value);
        var doc = ParseCanvasDocOrEmpty(existing);
        var blocks = doc["blocks"] as List<object> ?? new List<object>();

        blocks.Add(new Dictionary<string, object?>
        {
            ["kind"] = "chart",
            ["csvFileToken"] = body.ChartSpec.CsvFileToken.ToString(),
            ["fieldIndexes"] = body.ChartSpec.FieldIndexes,
            ["x"] = body.ChartSpec.X ?? 24,
            ["y"] = body.ChartSpec.Y ?? 24,
            ["width"] = body.ChartSpec.Width ?? 480,
            ["height"] = body.ChartSpec.Height ?? 361,
        });
        doc["blocks"] = blocks;

        var json = JsonSerializer.Serialize(doc);
        await _repo.UpsertCanvasDocument(targetId.Value, json);
        return Ok();
    }

    // Appends a filter spec to the target file's canvas. Mirrors AppendChart:
    // both the target and the source must resolve to files the current user can
    // access. The source token is baked into the filter block as `csvFileToken`
    // so the filter's `fieldIndex` is always interpreted against its OWN
    // schema, never the canvas owner's — that prevents the "fieldIndex 1 means
    // 'year' on source but 'date' on destination" silent-wrong-column bug.
    //
    // Per v1 the appliedTo set is emptied: source-canvas group ids don't
    // survive a copy, and the hydrate path recomputes appliedTo from chart
    // cards on the destination canvas that share the filter's csvFileToken.
    [HttpPost("canvas/append-filter")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> AppendFilter([FromBody] AppendFilterRequestDTO body)
    {
        if (body is null) return BadRequest();
        if (body.FilterSpec is null) return BadRequest("invalid filter spec");

        var targetId = await ResolveAccessibleFileId(body.TargetFileToken);
        if (targetId is null) return Forbid();

        var sourceId = await ResolveAccessibleFileId(body.FilterSpec.CsvFileToken);
        if (sourceId is null) return Forbid();

        var existing = await _repo.GetCanvasDocument(targetId.Value);
        var doc = ParseCanvasDocOrEmpty(existing);
        var blocks = doc["blocks"] as List<object> ?? new List<object>();

        blocks.Add(new Dictionary<string, object?>
        {
            ["kind"] = "filter",
            ["csvFileToken"] = body.FilterSpec.CsvFileToken.ToString(),
            ["fieldIndex"] = body.FilterSpec.FieldIndex,
            ["selectedValues"] = body.FilterSpec.SelectedValues ?? new List<string?>(),
            ["appliedTo"] = new List<int>(),
            ["x"] = body.FilterSpec.X ?? 24,
            ["y"] = body.FilterSpec.Y ?? 24,
            ["width"] = body.FilterSpec.Width ?? 320,
            ["height"] = body.FilterSpec.Height ?? 200,
        });
        doc["blocks"] = blocks;

        var json = JsonSerializer.Serialize(doc);
        await _repo.UpsertCanvasDocument(targetId.Value, json);
        return Ok();
    }

    private static Dictionary<string, object?> ParseCanvasDocOrEmpty(string? existing)
    {
        if (string.IsNullOrWhiteSpace(existing) || existing == "null")
            return new Dictionary<string, object?> { ["title"] = "", ["blocks"] = new List<object>() };

        try
        {
            using var parsed = JsonDocument.Parse(existing);
            var root = parsed.RootElement;
            var title = root.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String
                ? t.GetString() ?? "" : "";
            var blocks = new List<object>();
            if (root.TryGetProperty("blocks", out var b) && b.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in b.EnumerateArray())
                    blocks.Add(JsonSerializer.Deserialize<object>(el.GetRawText())!);
            }
            return new Dictionary<string, object?> { ["title"] = title, ["blocks"] = blocks };
        }
        catch (JsonException)
        {
            return new Dictionary<string, object?> { ["title"] = "", ["blocks"] = new List<object>() };
        }
    }
}