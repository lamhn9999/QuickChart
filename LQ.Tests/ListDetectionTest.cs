using LQ.Data.Database;
using LQ.Data.Repositories;
using LQ.Models.AnalystModels;
using LQ.Models.DTOs;
using LQ.Services.Implementations;
using LQ.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LQ.Tests;

/// <summary>
/// Integration tests for the suspicious-list detection flow:
///   - Upload CSV → get SuspiciousLists in result (field not yet written as list)
///   - ConfirmListFields  → converts the field to List&lt;string&gt; in DB
///
/// Each test copies the source CSV to a temp file with a unique name so that
/// parallel test runners can never conflict on the same (AnalystID, Name) key.
/// </summary>
[TestClass]
public class ListDetectionTest
{
    private ICSVService _csvService = null!;
    private IAnalystService _analystService = null!;
    private Analyst _analyst = null!;
    private string _sampleDir = null!;

    // Per-test tracking for cleanup
    private readonly List<string> _dbFileNames = new();   // names inserted into DB
    private readonly List<string> _tmpPaths    = new();   // temp file copies on disk

    private const string TestAnalystName = "ListTest.User";
    private const string TestPassword    = "Ab@123456";

    // Short names produced by Type.Name (how TableFieldDTO.TypeName is set in TableRepository)
    private static readonly string StringTypeName = typeof(string).Name;        // "String"
    private static readonly string ListTypeName   = typeof(List<string>).Name;  // "List`1"

    [TestInitialize]
    public async Task Setup()
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();
        var services = new ServiceCollection();
        services.AddDbContext<LQDbContext>(options =>
            options.UseNpgsql(config.GetConnectionString("DefaultConnection")));
        services.AddAuthentication();
        services.AddIdentityCore<Analyst>()
            .AddEntityFrameworkStores<LQDbContext>()
            .AddSignInManager();
        services.AddScoped<IAnalystService, AnalystService>();
        services.AddScoped<ICSVService, CSVService>();
        services.AddScoped<TableRepositoryAbstract, TableRepository>();

        var provider = services.BuildServiceProvider();
        _analystService = provider.GetRequiredService<IAnalystService>();
        _csvService     = provider.GetRequiredService<ICSVService>();

        _analyst = await _analystService.FindByName(TestAnalystName)
                   ?? await CreateTestAnalyst();

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !dir.GetFiles("*.csproj").Any())
            dir = dir.Parent;
        _sampleDir = Path.Combine(dir!.Parent!.FullName, "LQ", "Data", "SampleCSVs");
    }

    private async Task<Analyst> CreateTestAnalyst()
    {
        await _analystService.Add(Analyst.GiveBirth(TestAnalystName)!, TestPassword);
        return (await _analystService.FindByName(TestAnalystName))!;
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        foreach (var name in _dbFileNames)
            try { await _csvService.RemoveFile(_analyst, name); } catch { /* already gone */ }
        _dbFileNames.Clear();

        foreach (var path in _tmpPaths)
            try { File.Delete(path); } catch { /* already gone */ }
        _tmpPaths.Clear();
    }

    /// <summary>
    /// Copies <paramref name="csvFileName"/> from the sample directory to a temp file with a
    /// unique name (GUID suffix) and uploads it. Returns the UploadResultDTO, or null on failure.
    /// The unique name prevents any parallel test instance from conflicting on the same DB key.
    /// </summary>
    private async Task<UploadResultDTO?> Upload(string csvFileName)
    {
        var src   = Path.Combine(_sampleDir, csvFileName);
        var ext   = Path.GetExtension(csvFileName);
        var uniqueName = $"{Path.GetFileNameWithoutExtension(csvFileName)}_{Guid.NewGuid():N}{ext}";
        var tmp   = Path.Combine(Path.GetTempPath(), uniqueName);

        File.Copy(src, tmp);
        _tmpPaths.Add(tmp);

        var result = await _csvService.ProcessUploaded(_analyst, tmp);
        if (result is not null) _dbFileNames.Add(uniqueName);
        return result;
    }

    // ─── top_rated_2000webseries.csv  ────────────────────────────────────────

    [TestMethod]
    public async Task WebSeries_Upload_Succeeds()
    {
        var result = await Upload("top_rated_2000webseries.csv");
        Assert.IsNotNull(result);
    }

    [TestMethod]
    public async Task WebSeries_GenreColumn_FlaggedAsSuspicious()
    {
        var result = await Upload("top_rated_2000webseries.csv");
        Assert.IsNotNull(result);

        var genre = result.SuspiciousLists.FirstOrDefault(s => s.FieldName == "genre");
        Assert.IsNotNull(genre, "Expected 'genre' to be flagged as a suspicious list column.");
    }

    [TestMethod]
    public async Task WebSeries_GenreSuspicion_HasCommaAsDetectedSeparator()
    {
        var result = await Upload("top_rated_2000webseries.csv");
        Assert.IsNotNull(result);

        var genre = result.SuspiciousLists.First(s => s.FieldName == "genre");
        Assert.AreEqual(",", genre.Separator,
            "The detected separator for multi-genre values should be ','.");
    }

    [TestMethod]
    public async Task WebSeries_GenreSuspicion_HasSampleValues()
    {
        var result = await Upload("top_rated_2000webseries.csv");
        Assert.IsNotNull(result);

        var genre = result.SuspiciousLists.First(s => s.FieldName == "genre");
        Assert.IsTrue(genre.Samples.Count is > 0 and <= 5,
            "Should return 1–5 sample values.");
        Assert.IsTrue(genre.Samples.Any(s => s.Contains(",")),
            "At least one sample should contain a comma.");
    }

    [TestMethod]
    public async Task WebSeries_GenreField_StoredAsStringNotList()
    {
        var result = await Upload("top_rated_2000webseries.csv");
        Assert.IsNotNull(result);

        var csvFile = await _csvService.GetFile(result.FileId);
        var display = await _csvService.GetTableDisplayLimited(csvFile, 5);
        var genre   = display.Fields.First(f => f.Name == "genre");

        Assert.AreEqual(StringTypeName, genre.TypeName,
            "Suspicious field should be stored as string until the user confirms it is a list.");
    }

    [TestMethod]
    public async Task WebSeries_ConfirmGenre_ChangesFieldTypeToList()
    {
        var result = await Upload("top_rated_2000webseries.csv");
        Assert.IsNotNull(result);

        var genreSuspicion = result.SuspiciousLists.First(s => s.FieldName == "genre");
        await _csvService.ConfirmListFields(result.FileId, _analyst.Id, null,
            [new ConfirmFieldDTO(genreSuspicion.FieldIndex, genreSuspicion.Separator)]);

        var csvFile = await _csvService.GetFile(result.FileId);
        var display = await _csvService.GetTableDisplayLimited(csvFile, 5);
        var genre   = display.Fields.First(f => f.Name == "genre");

        Assert.AreEqual(ListTypeName, genre.TypeName,
            "After confirming genre as a list, field type should become List<string>.");
    }

    [TestMethod]
    public async Task WebSeries_NotConfirming_FieldRemainsString()
    {
        var result = await Upload("top_rated_2000webseries.csv");
        Assert.IsNotNull(result);

        // Deliberately do NOT call ConfirmListFields
        var csvFile = await _csvService.GetFile(result.FileId);
        var display = await _csvService.GetTableDisplayLimited(csvFile, 5);
        var genre   = display.Fields.First(f => f.Name == "genre");

        Assert.AreEqual(StringTypeName, genre.TypeName,
            "Without confirmation, the field should remain as string.");
    }

    // ─── duolingo_learning_traces_reduced.csv  ───────────────────────────────
    // This CSV contains lexeme_string values like "lernt/lernen<vblex><pri><p3><sg>".
    // The repeated grammar tags (<vblex>, <sg>, etc.) previously caused the column to be
    // silently misclassified as List<string>, corrupting the stored values. After the fix,
    // the column is flagged as suspicious and kept as a plain string until the user confirms.

    [TestMethod]
    public async Task Duolingo_Upload_Succeeds()
    {
        var result = await Upload("duolingo_learning_traces_reduced.csv");
        Assert.IsNotNull(result);
    }

    [TestMethod]
    public async Task Duolingo_LexemeString_FlaggedAsSuspicious()
    {
        var result = await Upload("duolingo_learning_traces_reduced.csv");
        Assert.IsNotNull(result);

        var lexeme = result.SuspiciousLists.FirstOrDefault(s => s.FieldName == "lexeme_string");
        Assert.IsNotNull(lexeme,
            "lexeme_string has repeated grammar tags that look like list separators and should be flagged for user review.");
    }

    [TestMethod]
    public async Task Duolingo_LexemeString_StoredAsStringNotList()
    {
        var result = await Upload("duolingo_learning_traces_reduced.csv");
        Assert.IsNotNull(result);

        var csvFile = await _csvService.GetFile(result.FileId);
        var display = await _csvService.GetTableDisplayLimited(csvFile, 5);
        var lexeme  = display.Fields.First(f => f.Name == "lexeme_string");

        Assert.AreEqual(StringTypeName, lexeme.TypeName,
            "lexeme_string should be stored as a plain string — not silently converted to a list.");
    }

    [TestMethod]
    public async Task Duolingo_LexemeString_ValuesNotCorrupted()
    {
        var result = await Upload("duolingo_learning_traces_reduced.csv");
        Assert.IsNotNull(result);

        // Samples are taken from raw ValueString; verify the original annotation chars survive
        var lexeme = result.SuspiciousLists.FirstOrDefault(s => s.FieldName == "lexeme_string");
        Assert.IsNotNull(lexeme);
        Assert.IsTrue(
            lexeme.Samples.Any(s => s.Contains('/') || s.Contains('<')),
            "Sample values should still contain the original annotation characters ('/' or '<'), " +
            "not be re-joined with commas.");
    }

    // ─── student_data.csv  ───────────────────────────────────────────────────
    // Only categorical (school, sex, Mjob…) and numeric columns — no special separator
    // characters appear in any values, so the suspicious list should be empty.

    [TestMethod]
    public async Task StudentData_Upload_Succeeds()
    {
        var result = await Upload("student_data.csv");
        Assert.IsNotNull(result);
    }

    [TestMethod]
    public async Task StudentData_NoSuspiciousListColumns()
    {
        var result = await Upload("student_data.csv");
        Assert.IsNotNull(result);

        Assert.AreEqual(0, result.SuspiciousLists.Count,
            $"Expected no suspicious list columns in student_data.csv but got: " +
            string.Join(", ", result.SuspiciousLists.Select(s => s.FieldName)));
    }

    // ─── games.csv  ──────────────────────────────────────────────────────────
    // The 'moves' column has space-separated chess moves like "d4 d5 c4 c6".
    // Spaces are excluded from separator candidates, so moves should never be flagged.

    [TestMethod]
    public async Task Games_Upload_Succeeds()
    {
        var result = await Upload("games.csv");
        Assert.IsNotNull(result);
    }

    [TestMethod]
    public async Task Games_MovesColumn_NotFlaggedAsSuspicious()
    {
        var result = await Upload("games.csv");
        Assert.IsNotNull(result);

        var moves = result.SuspiciousLists.FirstOrDefault(s => s.FieldName == "moves");
        Assert.IsNull(moves,
            "The 'moves' column uses spaces as separators; spaces are excluded from candidates so it should not be flagged.");
    }

    // ─── SuspiciousListDTO content ───────────────────────────────────────────

    [TestMethod]
    public async Task SuspiciousListDTO_FieldIndex_MatchesColumnPosition()
    {
        var result = await Upload("top_rated_2000webseries.csv");
        Assert.IsNotNull(result);

        // genre is the 7th column (0-based index 6):
        // id(0), title(1), original_title(2), overview(3), premiere_date(4), popularity(5), genre(6)
        var genre = result.SuspiciousLists.First(s => s.FieldName == "genre");
        Assert.AreEqual(6, genre.FieldIndex,
            "FieldIndex should match the 0-based column position in the CSV.");
    }

    [TestMethod]
    public async Task SuspiciousListDTO_SamplesAreRawCsvValues()
    {
        var result = await Upload("top_rated_2000webseries.csv");
        Assert.IsNotNull(result);

        // Breaking Bad's genre in the CSV is "Drama, Crime"
        var genre = result.SuspiciousLists.First(s => s.FieldName == "genre");
        Assert.IsTrue(
            genre.Samples.Any(s => s.Contains("Drama")),
            "Samples should include the raw CSV content such as 'Drama, Crime'.");
    }

    // ─── ConfirmListFields edge cases ────────────────────────────────────────

    [TestMethod]
    public async Task ConfirmListFields_WithEmptyList_IsNoOp()
    {
        var result = await Upload("top_rated_2000webseries.csv");
        Assert.IsNotNull(result);

        await _csvService.ConfirmListFields(result.FileId, _analyst.Id, null, []);

        var csvFile = await _csvService.GetFile(result.FileId);
        var display = await _csvService.GetTableDisplayLimited(csvFile, 5);
        var genre   = display.Fields.First(f => f.Name == "genre");

        Assert.AreEqual(StringTypeName, genre.TypeName,
            "Confirming an empty list should leave all field types unchanged.");
    }

    [TestMethod]
    public async Task ConfirmListFields_WithInvalidFieldIndex_IsIgnored()
    {
        var result = await Upload("top_rated_2000webseries.csv");
        Assert.IsNotNull(result);

        // Should silently skip unknown field indexes without throwing
        await _csvService.ConfirmListFields(result.FileId, _analyst.Id, null, [new ConfirmFieldDTO(9999, ",")]);
    }
}
