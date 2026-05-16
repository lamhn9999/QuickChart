using System.Text.Json;
using LQ.Data.Database;
using LQ.Data.Repositories;
using LQ.Models.TableModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Testcontainers.PostgreSql;

namespace LQ.Tests;

/// <summary>
/// Integration tests for the Canvas persistence layer (entity, DbContext mapping,
/// repository upsert/get, FK cascade) against an ephemeral Postgres container.
/// One container per class; each test seeds its own CSVFile so writes are isolated.
///
/// Requires Docker on the host.
/// </summary>
[TestClass]
public class CanvasPersistenceTest
{
    private static PostgreSqlContainer _pg = null!;
    private static string _connStr = null!;

    private ServiceProvider _provider = null!;
    private IServiceScope _scope = null!;
    private LQDbContext _db = null!;
    private TableRepositoryAbstract _repo = null!;

    [ClassInitialize]
    public static async Task ClassInit(TestContext _)
    {
        _pg = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .Build();
        await _pg.StartAsync();
        _connStr = _pg.GetConnectionString();

        var opts = new DbContextOptionsBuilder<LQDbContext>().UseNpgsql(_connStr).Options;
        await using var ctx = new LQDbContext(opts);
        await ctx.Database.EnsureCreatedAsync();
    }

    [ClassCleanup]
    public static async Task ClassCleanup() => await _pg.DisposeAsync();

    [TestInitialize]
    public void TestSetup()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddDebug());
        services.AddDbContext<LQDbContext>(o => o.UseNpgsql(_connStr));
        services.AddScoped<TableRepositoryAbstract, TableRepository>();

        _provider = services.BuildServiceProvider();
        _scope = _provider.CreateScope();
        _db = _scope.ServiceProvider.GetRequiredService<LQDbContext>();
        _repo = _scope.ServiceProvider.GetRequiredService<TableRepositoryAbstract>();
    }

    [TestCleanup]
    public void TestTeardown()
    {
        _scope.Dispose();
        _provider.Dispose();
    }

    private async Task<CSVFile> SeedFile()
    {
        // Phase 1 made ProjectID required on CSVFile, so every seeded file
        // needs a project to land in. Each test gets its own throwaway project
        // to avoid cross-test interference.
        var token = Guid.NewGuid().ToString();
        var project = new Project(token, "test-project", isGuest: true);
        _db.Projects.Add(project);
        await _db.SaveChangesAsync();

        var file = new CSVFile(token, $"test_{token}.csv", isGuest: true)
        {
            CreatedTime = DateTime.UtcNow,
            ModifiedTime = DateTime.UtcNow,
            ProjectID = project.ID
        };
        _db.CSVFiles.Add(file);
        await _db.SaveChangesAsync();
        return file;
    }

    [TestMethod]
    public async Task Upsert_ThenGet_ReturnsSameDocument()
    {
        var file = await SeedFile();
        const string doc = """{"title":"Hello","blocks":[{"kind":"text","blockType":"h1","content":"Title"}]}""";

        await _repo.UpsertCanvasDocument(file.ID, doc);
        var got = await _repo.GetCanvasDocument(file.ID);

        Assert.IsNotNull(got);
        // jsonb may re-serialize whitespace/key order; compare semantic equality.
        AssertJsonEqual(doc, got);
    }

    [TestMethod]
    public async Task Upsert_Twice_OverwritesAndKeepsSingleRow()
    {
        var file = await SeedFile();
        const string first  = """{"title":"v1","blocks":[]}""";
        const string second = """{"title":"v2","blocks":[{"kind":"divider"}]}""";

        await _repo.UpsertCanvasDocument(file.ID, first);
        await _repo.UpsertCanvasDocument(file.ID, second);

        var got = await _repo.GetCanvasDocument(file.ID);
        AssertJsonEqual(second, got!);

        var rowCount = await _db.Canvases.AsNoTracking()
            .CountAsync(c => c.LastUsedCSVFileID == file.ID);
        Assert.AreEqual(1, rowCount, "upsert must not create a second row");
    }

    [TestMethod]
    public async Task Get_NoCanvas_ReturnsNull()
    {
        var file = await SeedFile();
        var got = await _repo.GetCanvasDocument(file.ID);
        Assert.IsNull(got);
    }

    [TestMethod]
    public async Task DeleteCSVFile_NullsCanvasLastUsedFile()
    {
        // Post Phase 1: Canvas is owned by Project (cascade), not CSVFile.
        // CSVFile is referenced only via LastUsedCSVFileID with SetNull. A
        // canvas should survive deletion of its associated CSV; the pointer
        // just gets cleared.
        var file = await SeedFile();
        await _repo.UpsertCanvasDocument(file.ID, """{"title":"x","blocks":[]}""");

        var canvasId = await _db.Canvases.AsNoTracking()
            .Where(c => c.LastUsedCSVFileID == file.ID)
            .Select(c => c.ID)
            .SingleAsync();

        await _db.CSVFiles.Where(f => f.ID == file.ID).ExecuteDeleteAsync();

        var canvas = await _db.Canvases.AsNoTracking().SingleOrDefaultAsync(c => c.ID == canvasId);
        Assert.IsNotNull(canvas, "Canvas should outlive its associated CSVFile");
        Assert.IsNull(canvas!.LastUsedCSVFileID, "LastUsedCSVFileID should be nulled on file delete");
    }

    [TestMethod]
    public async Task Document_PreservesNestedShape()
    {
        var file = await SeedFile();
        var payload = new
        {
            title = "My report",
            blocks = new object[]
            {
                new { kind = "text",    blockType = "h1",       content = "Sales overview" },
                new { kind = "text",    blockType = "paragraph", content = "Quarterly numbers below." },
                new { kind = "divider" },
                new
                {
                    kind = "chart",
                    fieldIndexes = new[] { 0, 3, 7 },
                    chartType = 2,
                    backgroundColor = "#fafafa",
                    x = 120, y = 240, width = 480, height = 320,
                },
            },
        };
        var json = JsonSerializer.Serialize(payload);

        await _repo.UpsertCanvasDocument(file.ID, json);
        var got = await _repo.GetCanvasDocument(file.ID);
        Assert.IsNotNull(got);

        using var doc = JsonDocument.Parse(got);
        var root = doc.RootElement;
        Assert.AreEqual("My report", root.GetProperty("title").GetString());

        var blocks = root.GetProperty("blocks");
        Assert.AreEqual(4, blocks.GetArrayLength());

        var chart = blocks[3];
        Assert.AreEqual("chart", chart.GetProperty("kind").GetString());
        Assert.AreEqual(2, chart.GetProperty("chartType").GetInt32());
        Assert.AreEqual("#fafafa", chart.GetProperty("backgroundColor").GetString());
        Assert.AreEqual(480, chart.GetProperty("width").GetInt32());

        var idx = chart.GetProperty("fieldIndexes");
        CollectionAssert.AreEqual(
            new[] { 0, 3, 7 },
            idx.EnumerateArray().Select(e => e.GetInt32()).ToArray());
    }

    // Postgres jsonb is documented to not preserve key order or whitespace, so a
    // round-tripped document is semantically — but not textually — identical to
    // the input. Compare object keys as unordered, arrays as ordered.
    [TestMethod]
    public async Task Document_PreservesTextCardKind()
    {
        var file = await SeedFile();
        var payload = new
        {
            title = "with text card",
            blocks = new object[]
            {
                new { kind = "textCard", content = "free-form note", x = 200, y = 80, width = 360 },
            },
        };
        await _repo.UpsertCanvasDocument(file.ID, JsonSerializer.Serialize(payload));

        var got = await _repo.GetCanvasDocument(file.ID);
        Assert.IsNotNull(got);

        using var doc = JsonDocument.Parse(got);
        var card = doc.RootElement.GetProperty("blocks")[0];
        Assert.AreEqual("textCard", card.GetProperty("kind").GetString());
        Assert.AreEqual("free-form note", card.GetProperty("content").GetString());
        Assert.AreEqual(200, card.GetProperty("x").GetInt32());
        Assert.AreEqual(80, card.GetProperty("y").GetInt32());
        Assert.AreEqual(360, card.GetProperty("width").GetInt32());
    }

    [TestMethod]
    public async Task Document_RoundTrips_ChartBlockWithCsvFileToken()
    {
        // Cross-file charts carry their source CSVFile's public Guid token in
        // the persisted block. The token is opaque to the repository — it must
        // round-trip unchanged so the front-end can resolve it on hydrate.
        var file = await SeedFile();
        var sourceToken = Guid.NewGuid().ToString();
        var payload = new
        {
            title = "with foreign chart",
            blocks = new object[]
            {
                new
                {
                    kind = "chart",
                    csvFileToken = sourceToken,
                    fieldIndexes = new[] { 1, 4 },
                    x = 24, y = 24, width = 480, height = 361,
                },
            },
        };
        await _repo.UpsertCanvasDocument(file.ID, JsonSerializer.Serialize(payload));

        var got = await _repo.GetCanvasDocument(file.ID);
        Assert.IsNotNull(got);

        using var doc = JsonDocument.Parse(got);
        var chart = doc.RootElement.GetProperty("blocks")[0];
        Assert.AreEqual("chart", chart.GetProperty("kind").GetString());
        Assert.AreEqual(sourceToken, chart.GetProperty("csvFileToken").GetString());
        CollectionAssert.AreEqual(
            new[] { 1, 4 },
            chart.GetProperty("fieldIndexes").EnumerateArray().Select(e => e.GetInt32()).ToArray());
    }

    [TestMethod]
    public async Task GetFileByPublicToken_ReturnsFile_WhenTokenMatches()
    {
        var file = await SeedFile();
        var got = await _repo.GetFileByPublicToken(file.PublicToken);

        Assert.IsNotNull(got);
        Assert.AreEqual(file.ID, got!.ID);
    }

    [TestMethod]
    public async Task GetFileByPublicToken_ReturnsNull_WhenTokenUnknown()
    {
        var got = await _repo.GetFileByPublicToken(Guid.NewGuid());
        Assert.IsNull(got);
    }

    [TestMethod]
    public async Task UpsertCanvasDocumentById_UpdatesDocument_WithoutCreatingRow()
    {
        // Phase 4: token-keyed save path. Writes Document + UpdatedAt to a
        // known canvas row without going through the legacy CSVFile-keyed
        // lookup. Verifies the row stays singular and Document is overwritten.
        var file = await SeedFile();
        await _repo.UpsertCanvasDocument(file.ID, """{"title":"v1","blocks":[]}""");
        var canvasId = await _db.Canvases.AsNoTracking()
            .Where(c => c.LastUsedCSVFileID == file.ID)
            .Select(c => c.ID)
            .SingleAsync();

        await _repo.UpsertCanvasDocumentById(canvasId, """{"title":"v2","blocks":[{"kind":"divider"}]}""");

        var canvas = await _db.Canvases.AsNoTracking().SingleAsync(c => c.ID == canvasId);
        AssertJsonEqual("""{"title":"v2","blocks":[{"kind":"divider"}]}""", canvas.Document);
        var rowCount = await _db.Canvases.AsNoTracking().CountAsync(c => c.ID == canvasId);
        Assert.AreEqual(1, rowCount);
    }

    [TestMethod]
    public async Task SetCanvasLastUsedFileAsync_UpdatesPointer_RoundTripsNull()
    {
        // Phase 4: the CSV picker stores its selection on Canvas.LastUsedCSVFileID
        // so the next visit defaults back to the same file. Both a real id and
        // null (cleared) must persist.
        var file = await SeedFile();
        await _repo.UpsertCanvasDocument(file.ID, """{"title":"x","blocks":[]}""");
        var canvasId = await _db.Canvases.AsNoTracking()
            .Where(c => c.LastUsedCSVFileID == file.ID)
            .Select(c => c.ID)
            .SingleAsync();

        await _repo.SetCanvasLastUsedFileAsync(canvasId, null);
        var afterNull = await _db.Canvases.AsNoTracking().SingleAsync(c => c.ID == canvasId);
        Assert.IsNull(afterNull.LastUsedCSVFileID);

        await _repo.SetCanvasLastUsedFileAsync(canvasId, file.ID);
        var afterSet = await _db.Canvases.AsNoTracking().SingleAsync(c => c.ID == canvasId);
        Assert.AreEqual(file.ID, afterSet.LastUsedCSVFileID);
    }

    private static void AssertJsonEqual(string expected, string actual)
    {
        using var e = JsonDocument.Parse(expected);
        using var a = JsonDocument.Parse(actual);
        Assert.IsTrue(JsonStructurallyEqual(e.RootElement, a.RootElement),
            $"json mismatch.\nexpected: {expected}\nactual:   {actual}");
    }

    private static bool JsonStructurallyEqual(JsonElement x, JsonElement y)
    {
        if (x.ValueKind != y.ValueKind) return false;
        switch (x.ValueKind)
        {
            case JsonValueKind.Object:
                var xProps = x.EnumerateObject().ToDictionary(p => p.Name, p => p.Value);
                var yProps = y.EnumerateObject().ToDictionary(p => p.Name, p => p.Value);
                if (xProps.Count != yProps.Count) return false;
                foreach (var (k, vx) in xProps)
                    if (!yProps.TryGetValue(k, out var vy) || !JsonStructurallyEqual(vx, vy)) return false;
                return true;
            case JsonValueKind.Array:
                if (x.GetArrayLength() != y.GetArrayLength()) return false;
                for (int i = 0; i < x.GetArrayLength(); i++)
                    if (!JsonStructurallyEqual(x[i], y[i])) return false;
                return true;
            case JsonValueKind.String:
                return x.GetString() == y.GetString();
            case JsonValueKind.Number:
                return x.GetRawText() == y.GetRawText();
            case JsonValueKind.True:
            case JsonValueKind.False:
            case JsonValueKind.Null:
                return true;
            default:
                return x.GetRawText() == y.GetRawText();
        }
    }
}
