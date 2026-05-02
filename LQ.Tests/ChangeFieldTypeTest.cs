using System.Diagnostics;
using System.Globalization;
using LQ.Data.Database;
using LQ.Models.TableModels;
using LQ.Services.Implementations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Testcontainers.PostgreSql;

namespace LQ.Tests;

/// <summary>
/// Integration tests for CSVService.ChangeFieldType against an ephemeral Postgres
/// container (Testcontainers). One container per test class; each test seeds its own
/// CSVFile + Field + Values so tests are isolated by primary key.
///
/// Requires Docker to be available on the host.
/// </summary>
[TestClass]
public class ChangeFieldTypeContainerTest
{
    private static PostgreSqlContainer _pg = null!;
    private static string _connStr = null!;

    private ServiceProvider _provider = null!;
    private IServiceScope _scope = null!;
    private LQDbContext _db = null!;
    private CSVService _csv = null!;

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
        services.AddScoped<CSVService>();

        _provider = services.BuildServiceProvider();
        _scope = _provider.CreateScope();
        _db = _scope.ServiceProvider.GetRequiredService<LQDbContext>();
        _csv = _scope.ServiceProvider.GetRequiredService<CSVService>();
    }

    [TestCleanup]
    public void TestTeardown()
    {
        _scope.Dispose();
        _provider.Dispose();
    }

    private async Task<(CSVFile file, Field field)> SeedColumn(
        Type initialType, string columnName, IEnumerable<string?> formattedValues)
    {
        var token = Guid.NewGuid().ToString();
        var file = new CSVFile(token, $"test_{token}.csv", isGuest: true)
        {
            CreatedTime = DateTime.UtcNow,
            ModifiedTime = DateTime.UtcNow
        };
        _db.CSVFiles.Add(file);
        await _db.SaveChangesAsync();

        var field = new Field
        {
            Name = columnName,
            Index = 0,
            Categories = new List<Category>(),
            CSVFile = file
        };
        field.Type = initialType;
        _db.Fields.Add(field);
        await _db.SaveChangesAsync();

        int rowIndex = 0;
        foreach (var v in formattedValues)
        {
            _db.Values.Add(new Value
            {
                Field = field,
                FieldID = field.ID,
                RowIndex = rowIndex++,
                ValueString = v,
                ValueStringFormatted = v
            });
        }
        await _db.SaveChangesAsync();

        return (file, field);
    }

    [TestMethod]
    public async Task NumericToDateTime_UnixSeconds_ReformatsToCanonical()
    {
        var (file, field) = await SeedColumn(typeof(int), "ts",
            Enumerable.Repeat<string?>("1362076081", 5));

        var result = await _csv.ChangeFieldType(file, field.Index, "datetime", null);

        Assert.IsTrue(result.Status,
            $"expected status=true. mismatches: {string.Join(", ", result.MismatchedValues)}");
        Assert.AreEqual(0, result.MismatchedCount);

        var f = await _db.Fields.AsNoTracking().SingleAsync(x => x.ID == field.ID);
        Assert.AreEqual(typeof(DateTime).AssemblyQualifiedName, f.TypeName);

        // Build expected the same way production does, so any culture-specific
        // whitespace (e.g. U+202F before AM/PM in .NET 6+ en-US) matches.
        var expected = DateTime.UnixEpoch.AddSeconds(1362076081)
            .ToString(CultureInfo.GetCultureInfo("en-US"));
        var vsfs = await _db.Values.AsNoTracking()
            .Where(v => v.FieldID == field.ID)
            .Select(v => v.ValueStringFormatted)
            .ToListAsync();
        Assert.IsTrue(vsfs.All(v => v == expected),
            $"expected '{expected}', got [{string.Join(", ", vsfs)}]");
    }

    [TestMethod]
    public async Task NumericToDateTime_UnixMilliseconds_PicksMsUnit()
    {
        var (file, field) = await SeedColumn(typeof(double), "ts_ms",
            Enumerable.Repeat<string?>("1704067200000", 3));

        var result = await _csv.ChangeFieldType(file, field.Index, "datetime", null);

        Assert.IsTrue(result.Status,
            $"expected status=true. mismatches: {string.Join(", ", result.MismatchedValues)}");

        var vsf = await _db.Values.AsNoTracking()
            .Where(v => v.FieldID == field.ID)
            .Select(v => v.ValueStringFormatted)
            .FirstAsync();
        StringAssert.Contains(vsf, "2024", $"expected year 2024, got {vsf}");
    }

    [TestMethod]
    public async Task NumericToDateTime_OutOfRangeValues_AreReportedAsMismatches()
    {
        var seeded = new string?[] { "1704067200", "99999999999999999999" };
        var (file, field) = await SeedColumn(typeof(double), "mixed", seeded);

        var result = await _csv.ChangeFieldType(file, field.Index, "datetime", null);

        Assert.IsFalse(result.Status, "expected at least one mismatch");
        Assert.IsTrue(result.MismatchedValues.Contains("99999999999999999999"),
            $"expected '99999999999999999999' in mismatches, got: [{string.Join(", ", result.MismatchedValues)}]");

        // Field type unchanged on failure.
        var f = await _db.Fields.AsNoTracking().SingleAsync(x => x.ID == field.ID);
        Assert.AreEqual(typeof(double).AssemblyQualifiedName, f.TypeName);

        // VSF unchanged on failure.
        var vsfs = await _db.Values.AsNoTracking()
            .Where(v => v.FieldID == field.ID)
            .OrderBy(v => v.RowIndex)
            .Select(v => v.ValueStringFormatted)
            .ToListAsync();
        CollectionAssert.AreEqual(seeded, vsfs);
    }

    [TestMethod]
    public async Task SameTargetType_NoOp_NoMutation()
    {
        const string canonical = "1/15/2024 12:00:00 AM";
        var (file, field) = await SeedColumn(typeof(DateTime), "dt",
            Enumerable.Repeat<string?>(canonical, 3));

        var result = await _csv.ChangeFieldType(file, field.Index, "datetime", null);

        Assert.IsTrue(result.Status);
        Assert.AreEqual(0, result.MismatchedCount);

        var vsfs = await _db.Values.AsNoTracking()
            .Where(v => v.FieldID == field.ID)
            .Select(v => v.ValueStringFormatted)
            .ToListAsync();
        Assert.IsTrue(vsfs.All(v => v == canonical),
            $"expected unchanged values; got [{string.Join(", ", vsfs)}]");
    }

    [TestMethod]
    public async Task StringToDateTime_NonCanonical_GetsReformatted()
    {
        var (file, field) = await SeedColumn(typeof(string), "iso",
            Enumerable.Repeat<string?>("2024-01-15", 4));

        var result = await _csv.ChangeFieldType(file, field.Index, "datetime", null);

        Assert.IsTrue(result.Status,
            $"expected status=true. mismatches: {string.Join(", ", result.MismatchedValues)}");

        var expected = DateTime.Parse("2024-01-15", CultureInfo.InvariantCulture)
            .ToString(CultureInfo.GetCultureInfo("en-US"));
        var vsf = await _db.Values.AsNoTracking()
            .Where(v => v.FieldID == field.ID)
            .Select(v => v.ValueStringFormatted)
            .FirstAsync();
        Assert.AreEqual(expected, vsf);
    }

    /// <summary>
    /// Locks the regression: the actual values from production field id 3404 (csvfileid 225,
    /// column "timestamp") that originally surfaced the numeric-to-DateTime mismatch bug.
    /// Fixture is a one-time dump; refresh via LQ.Tests/Fixtures/README.md if the live
    /// data shape changes.
    /// </summary>
    [TestMethod]
    public async Task NumericToDateTime_Field3404_Snapshot_ReformatsToCanonical()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "field_3404_timestamp.txt");
        Assert.IsTrue(File.Exists(fixturePath), $"fixture missing at {fixturePath}");

        var values = (await File.ReadAllLinesAsync(fixturePath))
            .Select(l => string.IsNullOrWhiteSpace(l) ? null : l)
            .ToList();

        var (file, field) = await SeedColumn(typeof(int), "timestamp", values);

        var sw = Stopwatch.StartNew();
        var result = await _csv.ChangeFieldType(file, field.Index, "datetime", null);
        sw.Stop();
        Debug.WriteLine($"Field 3404 snapshot ({values.Count} rows) converted in {sw.ElapsedMilliseconds}ms");

        Assert.IsTrue(result.Status,
            $"snapshot should convert cleanly. {result.MismatchedCount} mismatches. " +
            $"first 5: [{string.Join(", ", result.MismatchedValues.Take(5))}]");
        Assert.AreEqual(0, result.MismatchedCount);

        var f = await _db.Fields.AsNoTracking().SingleAsync(x => x.ID == field.ID);
        Assert.AreEqual(typeof(DateTime).AssemblyQualifiedName, f.TypeName);

        var firstNonNull = values.First(v => !string.IsNullOrWhiteSpace(v))!;
        var expected = DateTime.UnixEpoch
            .AddSeconds(double.Parse(firstNonNull, CultureInfo.InvariantCulture))
            .ToString(CultureInfo.GetCultureInfo("en-US"));
        var actual = await _db.Values.AsNoTracking()
            .Where(v => v.FieldID == field.ID && v.ValueString == firstNonNull)
            .Select(v => v.ValueStringFormatted)
            .FirstAsync();
        Assert.AreEqual(expected, actual);
    }
}
