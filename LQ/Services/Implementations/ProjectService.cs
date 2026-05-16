using System.Text.Json;
using LQ.Data.Database;
using LQ.Data.Repositories;
using LQ.Models.AnalystModels;
using LQ.Models.DTOs;
using LQ.Models.TableModels;
using LQ.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LQ.Services.Implementations;

public class ProjectService : TableRepository, IProjectService
{
    private readonly ICSVService _csvService;
    private readonly IAnalystService _analystService;
    private readonly CanvasPreviewService _previewService;
    private readonly ILogger<ProjectService>? _logger;

    public ProjectService(
        LQDbContext lqDbContext,
        ICSVService csvService,
        IAnalystService analystService,
        CanvasPreviewService previewService,
        ILogger<ProjectService>? logger = null)
        : base(lqDbContext)
    {
        _csvService = csvService;
        _analystService = analystService;
        _previewService = previewService;
        _logger = logger;
    }

    public async Task<List<ProjectListItemDTO>> List(string? analystId, string? guestToken)
    {
        if (analystId is null && guestToken is null) return new List<ProjectListItemDTO>();

        // Make sure the caller always has at least their "My Workspace" so the
        // grid is never empty for a fresh account.
        await EnsureDefaultProject(analystId, guestToken);

        var projects = await GetProjects(analystId, guestToken);
        if (projects.Count == 0) return new List<ProjectListItemDTO>();
        var ids = projects.Select(p => p.ID).ToList();

        // Two cheap aggregate hits beat N project-card queries.
        var csvCounts = await _lqDbContext.CSVFiles
            .AsNoTracking()
            .Where(f => ids.Contains(f.ProjectID))
            .GroupBy(f => f.ProjectID)
            .Select(g => new { ProjectId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ProjectId, x => x.Count);

        var canvasCounts = await _lqDbContext.Canvases
            .AsNoTracking()
            .Where(c => ids.Contains(c.ProjectID))
            .GroupBy(c => c.ProjectID)
            .Select(g => new { ProjectId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ProjectId, x => x.Count);

        return projects.Select(p => new ProjectListItemDTO(
            p.PublicToken, p.Name, p.CreatedTime, p.ModifiedTime,
            csvCounts.GetValueOrDefault(p.ID), canvasCounts.GetValueOrDefault(p.ID))).ToList();
    }

    public async Task<ProjectDetailDTO?> GetByToken(Guid token, string? analystId, string? guestToken)
    {
        var project = await GetProjectByToken(token);
        if (project is null || !OwnedBy(project, analystId, guestToken)) return null;

        var csvFiles = await GetProjectCSVFiles(project.ID);
        var canvases = await GetProjectCanvases(project.ID);

        // Row + field counts for CSV cards — one grouped count query per metric,
        // joined by FieldID. We only show counts for Ready files because
        // Processing files' field/row totals aren't stable yet.
        var fileIds = csvFiles.Select(f => f.ID).ToList();
        Dictionary<int, int> fieldCounts = new();
        Dictionary<int, int> rowCounts = new();
        if (fileIds.Count > 0)
        {
            fieldCounts = await _lqDbContext.Fields
                .AsNoTracking()
                .Where(f => fileIds.Contains(f.CSVFile.ID))
                .GroupBy(f => f.CSVFile.ID)
                .Select(g => new { FileId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.FileId, x => x.Count);

            // Max(RowIndex)+1 across all fields. Cheaper than COUNT DISTINCT —
            // the CSV import guarantees every row has a value per field.
            rowCounts = await _lqDbContext.Values
                .AsNoTracking()
                .Where(v => _lqDbContext.Fields.Any(f => f.ID == v.FieldID && fileIds.Contains(f.CSVFile.ID)))
                .Join(_lqDbContext.Fields,
                    v => v.FieldID,
                    f => f.ID,
                    (v, f) => new { FileId = f.CSVFile.ID, v.RowIndex })
                .GroupBy(x => x.FileId)
                .Select(g => new { FileId = g.Key, MaxRow = g.Max(x => (int?)x.RowIndex) })
                .ToDictionaryAsync(x => x.FileId, x => (x.MaxRow ?? -1) + 1);
        }

        var csvDtos = csvFiles.Select(f => new ProjectChildDTO(
            ProjectChildKind.CSV, f.PublicToken, f.Name, f.ModifiedTime,
            rowCounts.GetValueOrDefault(f.ID),
            fieldCounts.GetValueOrDefault(f.ID),
            f.Status.ToString(),
            PreviewSvg: null)).ToList();

        var canvasDtos = canvases.Select(c => new ProjectChildDTO(
            ProjectChildKind.Canvas, c.PublicToken, c.Name, c.UpdatedAt,
            RowCount: null, FieldCount: null, Status: null,
            PreviewSvg: c.PreviewJson ?? _previewService.BuildSvg(c.Document))).ToList();

        return new ProjectDetailDTO(
            project.PublicToken, project.Name, project.CreatedTime, project.ModifiedTime,
            csvDtos, canvasDtos);
    }

    public async Task<ProjectListItemDTO> Create(string name, string? analystId, string? guestToken)
    {
        var clean = string.IsNullOrWhiteSpace(name) ? "Untitled project" : name.Trim();
        Project project = analystId is not null
            ? new Project(analystId, clean)
            : new Project(guestToken!, clean, isGuest: true);
        await InsertProject(project);
        return new ProjectListItemDTO(project.PublicToken, project.Name, project.CreatedTime, project.ModifiedTime, 0, 0);
    }

    public async Task<bool> Rename(Guid token, string newName, string? analystId, string? guestToken)
    {
        var clean = (newName ?? "").Trim();
        if (clean.Length == 0) return false;
        var project = await GetProjectByToken(token);
        if (project is null || !OwnedBy(project, analystId, guestToken)) return false;
        await RenameProject(project.ID, clean);
        return true;
    }

    public async Task<bool> Delete(Guid token, string? analystId, string? guestToken)
    {
        var project = await GetProjectByToken(token);
        if (project is null || !OwnedBy(project, analystId, guestToken)) return false;
        await DeleteProject(project.ID);
        return true;
    }

    public async Task<UploadResultDTO?> UploadCSV(Guid projectToken, IFormFile file, string? analystId, string? guestToken)
    {
        var project = await GetProjectByToken(projectToken);
        if (project is null || !OwnedBy(project, analystId, guestToken)) return null;

        // CSV-only gate. Single-extension check mirrors the existing
        // CSVFile.NameCheck() which the home upload also runs.
        if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)) return null;

        if (analystId is not null)
        {
            var analyst = await _analystService.FindById(analystId);
            if (analyst is null) return null;
            return await _csvService.ProcessUploaded(analyst, file, project.ID);
        }
        return await _csvService.ProcessUploadedGuest(guestToken!, file, project.ID);
    }

    public async Task<ProjectChildDTO?> CreateCanvas(Guid projectToken, string? requestedName, string? analystId, string? guestToken)
    {
        var project = await GetProjectByToken(projectToken);
        if (project is null || !OwnedBy(project, analystId, guestToken)) return null;
        var name = string.IsNullOrWhiteSpace(requestedName) ? "Untitled canvas" : requestedName.Trim();
        var emptyDoc = JsonSerializer.Serialize(new { title = "", blocks = Array.Empty<object>() });
        var canvas = new Canvas(project.ID, name, emptyDoc);
        await InsertCanvas(canvas);
        return new ProjectChildDTO(
            ProjectChildKind.Canvas, canvas.PublicToken, canvas.Name, canvas.UpdatedAt,
            RowCount: null, FieldCount: null, Status: null,
            PreviewSvg: _previewService.BuildSvg(emptyDoc));
    }

    public async Task<bool> RenameCanvas(Guid canvasToken, string newName, string? analystId, string? guestToken)
    {
        var clean = (newName ?? "").Trim();
        if (clean.Length == 0) return false;
        var canvas = await GetCanvasByToken(canvasToken);
        if (canvas is null) return false;
        if (!await CanvasOwnedBy(canvas, analystId, guestToken)) return false;
        await base.RenameCanvas(canvas.ID, clean);
        return true;
    }

    public async Task<bool> DeleteCanvas(Guid canvasToken, string? analystId, string? guestToken)
    {
        var canvas = await GetCanvasByToken(canvasToken);
        if (canvas is null) return false;
        if (!await CanvasOwnedBy(canvas, analystId, guestToken)) return false;
        await base.DeleteCanvas(canvas.ID);
        return true;
    }

    private async Task<bool> CanvasOwnedBy(Canvas canvas, string? analystId, string? guestToken)
    {
        var project = await _lqDbContext.Projects
            .AsNoTracking()
            .SingleOrDefaultAsync(p => p.ID == canvas.ProjectID);
        return project is not null && OwnedBy(project, analystId, guestToken);
    }

    public async Task RegenerateCanvasPreview(Guid canvasToken)
    {
        var canvas = await GetCanvasByToken(canvasToken);
        if (canvas is null) return;
        var svg = _previewService.BuildSvg(canvas.Document);
        await UpdateCanvasPreview(canvas.ID, svg);
    }

    // Centralised ownership check — guards every mutation. We reject if the
    // project belongs to someone else (or is a guest project and the cookie
    // doesn't match). 404 vs 403 is the caller's choice; here we just say no.
    private static bool OwnedBy(Project project, string? analystId, string? guestToken)
    {
        if (analystId is not null) return project.AnalystID == analystId;
        if (guestToken is not null) return project.GuestToken == guestToken;
        return false;
    }
}
