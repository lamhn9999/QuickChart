using LQ.Models.AnalystModels;
using LQ.Models.ChartModels;
using LQ.Models.TableModels;
using LQ.Models.DTOs;

namespace LQ.Data.Repositories;

public abstract class TableRepositoryAbstract
{
    protected abstract Task InsertFile(CSVFile csvFile);

    public abstract Task<CSVFile> GetFile(Analyst analyst, string fileName);
    public abstract Task<CSVFile> GetFile(int csvFileId);
    public abstract Task<CSVFile?> GetFileByPublicToken(Guid publicToken);
    public abstract Task<List<CSVFile>> GetAllFiles(Analyst analyst);
    public abstract Task<bool> FileExists(Analyst analyst, string fileName);
    
    public abstract Task<CSVFile> GetFile(string guestToken, string fileName);                                                                                                                                                          
    public abstract Task<List<CSVFile>> GetAllFiles(string guestToken);
    public abstract Task<bool> FileExists(string guestToken, string fileName);
    protected abstract Task RemoveFileByGuest(string guestToken, string fileName);

    protected abstract Task RemoveFile(CSVFile csvFile);
    protected abstract Task RenameFile(CSVFile csvFile, string newName);

    protected abstract Task Insert(Table table);

    public abstract Task<List<Field>> GetFields(CSVFile csvFile, List<int> fieldIndexes);
    public abstract Task<Field> GetField(int csvFileId, int fieldIndex);

    public abstract Task<Table> Get(CSVFile csvFile);
    public abstract Task<Table> Get(CSVFile csvFile, List<int> fieldIndexes);
    public abstract Task<Table> GetLimited(CSVFile csvFile, int rowLimit);
    public abstract Task<Table> GetLimited(CSVFile csvFile, List<int> fieldIndexes, int rowLimit);
    public abstract Task<Table> GetWithFilter(CSVFile csvFile, List<ValueFilterDTO> filters);
    public abstract Task<Table> GetWithFilter(CSVFile csvFile, List<int> fieldIndexes, List<ValueFilterDTO> filters);
    public abstract Task<Table> GetWithFilterLimited(CSVFile csvFile, List<ValueFilterDTO> filters, int rowLimit);
    public abstract Task<TableDisplayDTO> GetDisplayLimited(CSVFile csvFile, int rowLimit);
    public abstract Task<TableDisplayDTO> GetDisplayWithFilterLimited(CSVFile csvFile, List<ValueFilterDTO> filters,
        int rowLimit); 

    protected abstract Task<TryChangeFieldTypeDTO> ChangeFieldType(ChangeFieldTypeDTO dto);

    /// <summary>Returns the saved canvas document JSON for a CSVFile, or null if none exists.</summary>
    public abstract Task<string?> GetCanvasDocument(int csvFileId);

    /// <summary>Inserts or updates the canvas document for a CSVFile. Caller is responsible for ownership checks.</summary>
    public abstract Task UpsertCanvasDocument(int csvFileId, string documentJson);

    /// <summary>
    /// Returns cached sparkline JSON if it was computed for the field's current type.
    /// Returns null on cache miss or TypeName mismatch.
    /// </summary>
    public abstract Task<string?> GetSparklineJsonAsync(int csvFileId, int fieldIndex);

    /// <summary>
    /// Conditionally stores sparkline JSON. The write is a no-op if TypeName has changed
    /// since <paramref name="typeName"/> was captured, preventing stale cache on type-change races.
    /// </summary>
    public abstract Task SetSparklineJsonAsync(int csvFileId, int fieldIndex, string json, string typeName);

    /// <summary>Persists changes to the database. Destructive action — reserved for internal repository use.</summary>
    protected abstract Task SaveChangesAsync();

    /// <summary>Gets a field by ID. Reserved for internal repository use.</summary>
    protected abstract Task<Field> GetFieldById(int fieldId);

    /// <summary>Gets raw values for a field, excluding empty/null entries. Reserved for internal repository use.</summary>
    protected abstract Task<List<string>> GetFieldRawValuesAsync(int fieldId);

    /// <summary>Queries CSVFiles by upload status. Returns paginated results ordered by creation time descending.</summary>
    protected abstract Task<List<CSVFile>> GetPendingUploadsByStatusAsync(string? analystId, string? guestToken, UploadStatus status);

    /// <summary>Gets all CSVFiles with statuses other than Ready. Reserved for internal repository use.</summary>
    protected abstract Task<List<CSVFile>> GetAllNonReadyFilesAsync(string? analystId, string? guestToken);

    /// <summary>
    /// Materialises the row indexes that survive <paramref name="filters"/>.
    /// Returns an empty array (not null) when no rows match.
    /// Pass the result as <c>rowFilter</c> to any aggregate method to scope
    /// that query to the filtered subset without loading values into RAM.
    /// </summary>
    public abstract Task<int[]> GetFilteredRowIndexes(CSVFile csvFile, List<ValueFilterDTO> filters);

    // ─── Scale helpers ────────────────────────────────────────────────────────

    /// <summary>Distinct(field). Returns the number of distinct values.</summary>
    public abstract Task<int> CountDistinct(Field field, int[]? rowFilter = null);

    /// <summary>PERCENTILE_CONT(0.5) and IQR = Q3-Q1 for a numeric field. Used by: CreateChartAsync scale grouping.</summary>
    public abstract Task<(double Median, double IQR)> GetScaleInfo(Field numericField, int[]? rowFilter = null);

    /// <summary>MIN and MAX of a DateTime field as UTC. Used by: granularity detection.</summary>
    public abstract Task<(DateTime Min, DateTime Max)> GetDateRange(Field dateField, int[]? rowFilter = null);

    /// <summary>WIDTH_BUCKET histogram. Returns bucket index (1-based) → count. Used by: Histogram.</summary>
    public abstract Task<SortedDictionary<int, int>> GetHistogramBuckets(Field numericField, double min, double max, int numBuckets, int[]? rowFilter = null);

    /// <summary>SUM(numericField) GROUP BY groupFields. Returns a scalar when no group fields are provided.</summary>
    public abstract Task<List<(object Key, double Sum)>> Sum(Field fieldNumeric, params Field[] fieldGroup);

    /// <summary>SUM(numericField). Returns the sum of a numeric field.</summary>
    public abstract Task<(object Key, double Sum)> Sum(Field fieldNumeric);

    // ─── Count aggregates ───────────────────────────────────────────────────

    /// <summary>COUNT(*) GROUP BY groupField. Used by: Pie, SideBar, Pareto.</summary>
    public abstract Task<List<(object Key, int Count)>> CountByGroup(Field groupField, int[]? rowFilter = null);

    /// <summary>COUNT(*) GROUP BY groupField1, groupField2. Used by: Matrix.</summary>
    public abstract Task<List<(object KeyA, object KeyB, int Count)>> CountByGroup(Field groupField1, Field groupField2, int[]? rowFilter = null);

    /// <summary>COUNT(DISTINCT distinctField) GROUP BY groupField. Used by: MixedBarChart.</summary>
    public abstract Task<List<(object Key, int CountDistinct)>> CountDistinctByGroup(Field distinctField, Field groupField, int[]? rowFilter = null);

    // ─── Average aggregates ───────────────────────────────────────────────────

    /// <summary>AVG(numericField) GROUP BY groupField. Used by: Bar, Line, RankedBar, SideBar.</summary>
    public abstract Task<List<(object Key, double Average)>> AverageByGroup(Field numericField, Field groupField, int[]? rowFilter = null);

    /// <summary>AVG(numericField) GROUP BY groupFieldA, groupFieldB. Used by: Bar (cat+cat+num), MatrixMean.</summary>
    public abstract Task<List<(object KeyA, object KeyB, double Average)>> AverageByGroup(Field numericField, Field groupFieldA, Field groupFieldB, int[]? rowFilter = null);

    // ─── Five-number summary ──────────────────────────────────────────────────

    /// <summary>MIN, Q1, MEDIAN, Q3, MAX of numericField per category. Used by: BoxPlot.</summary>
    public abstract Task<List<(object Key, double Min, double Q1, double Median, double Q3, double Max)>> FiveNumberSummaryByGroup(Field numericField, Field groupField, int[]? rowFilter = null);

    // ─── Scalar min/max ───────────────────────────────────────────────────────

    /// <summary>MIN and MAX of a numeric field across the whole table. Used by: Histogram, BubbleChart.</summary>
    public abstract Task<(double Min, double Max)> MinMax(Field numericField, int[]? rowFilter = null);

    // ─── Time-bucketed aggregates ─────────────────────────────────────────────

    /// <summary>COUNT(*) GROUP BY date_trunc(granularity, dateField). Used by: DateTimeFrequency.</summary>
    public abstract Task<SortedDictionary<DateTime, int>> CountByTimeBucket(Field dateField, TimeGranularity granularity, int[]? rowFilter = null);

    /// <summary>AVG(numericField) GROUP BY date_trunc(granularity, dateField). Used by: TimeSeriesLine, TimeSeriesEnhanced, GrowthRate.</summary>
    public abstract Task<SortedDictionary<DateTime, double>> AverageByTimeBucket(Field numericField, Field dateField, TimeGranularity granularity, int[]? rowFilter = null);

    /// <summary>SUM(numericField) GROUP BY date_trunc(granularity, dateField). Used by: CumulativeSum.</summary>
    public abstract Task<SortedDictionary<DateTime, double>> SumByTimeBucket(Field numericField, Field dateField, TimeGranularity granularity, int[]? rowFilter = null);

    /// <summary>COUNT(*) GROUP BY date_trunc(granularity, dateField), catField. Used by: TimeSeriesCategorical.</summary>
    public abstract Task<SortedDictionary<DateTime, Dictionary<string, int>>> CountByTimeBucketAndCategory(Field dateField, Field catField, TimeGranularity granularity, int[]? rowFilter = null);

    /// <summary>AVG(numericField) GROUP BY date_trunc(granularity, dateField), catField. Used by: TimeSeriesCategoricalNumeric.</summary>
    public abstract Task<SortedDictionary<DateTime, Dictionary<string, double>>> AverageByTimeBucketAndCategory(Field numericField, Field dateField, Field catField, TimeGranularity granularity, int[]? rowFilter = null);
}
