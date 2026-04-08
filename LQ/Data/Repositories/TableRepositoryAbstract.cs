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
    public abstract Task<List<CSVFile>> GetAllFiles(Analyst analyst);

    public abstract Task<bool> FileExists(Analyst analyst, string fileName);

    protected abstract Task RemoveFile(CSVFile csvFile);
    protected abstract Task RenameFile(CSVFile csvFile, string newName);

    protected abstract Task Insert(Table table);

    public abstract Task<Table> Get(CSVFile csvFile);
    public abstract Task<Table> Get(CSVFile csvFile, List<int> fieldIndexes);
    public abstract Task<Table> GetLimited(CSVFile csvFile, int rowLimit);
    public abstract Task<Table> GetWithFilter(CSVFile csvFile, List<ValueFilterDTO> filters);
    public abstract Task<Table> GetWithFilter(CSVFile csvFile, List<int> fieldIndexes, List<ValueFilterDTO> filters);
    public abstract Task<Table> GetWithFilterLimited(CSVFile csvFile, List<ValueFilterDTO> filters, int rowLimit);

    protected abstract Task<TryChangeFieldTypeDTO> ChangeFieldType(ChangeFieldTypeDTO dto);

    /// <summary>Distinct(field). Returns the number of distinct value.</summary>
    public abstract int CountDistinct(Field field);

    /// <summary>SUM(numericField) GROUP BY groupFields. Returns a scalar when no group fields are provided.</summary>
    public abstract List<(object Key, double Sum)> Sum(Field fieldNumeric, params Field[] fieldGroup); 
    
    /// <summary>SUM(numericField). Returns the sum of a numeric field.</summary>
    public abstract (object Key, double Sum) Sum(Field fieldNumeric);
    
    // ─── Count aggregates ───────────────────────────────────────────────────
    
    /// <summary>COUNT(*) GROUP BY groupField. Used by: Pie, SideBar, Pareto.</summary>
    public abstract List<(object Key, int Count)> CountByGroup(Field groupField);

    /// <summary>COUNT(*) GROUP BY groupField1, groupField2. Used by: Matrix.</summary>
    public abstract List<(object KeyA, object KeyB, int Count)> CountByGroup(Field groupField1, Field groupField2);
    
    /// <summary>COUNT(DISTINCT distinctField) GROUP BY groupField. Used by: MixedBarChart.</summary>
    public abstract List<(object Key, int CountDistinct)> CountDistinctByGroup(Field distinctField, Field groupField);

    // ─── Average aggregates ───────────────────────────────────────────────────

    /// <summary>AVG(numericField) GROUP BY groupField. Used by: Bar, Line, RankedBar, SideBar.</summary>
    public abstract List<(object Key, double Average)> AverageByGroup(Field numericField, Field groupField);

    /// <summary>AVG(numericField) GROUP BY groupFieldA, groupFieldB. Used by: Bar (cat+cat+num), MatrixMean.</summary>
    public abstract List<(object KeyA, object KeyB, double Average)> AverageByGroup(Field numericField, Field groupFieldA, Field groupFieldB);

    // ─── Five-number summary ──────────────────────────────────────────────────

    /// <summary>MIN, Q1, MEDIAN, Q3, MAX of numericField per category. Used by: BoxPlot.</summary>
    public abstract List<(object Key, double Min, double Q1, double Median, double Q3, double Max)> FiveNumberSummaryByGroup(Field numericField, Field groupField);

    // ─── Scalar min/max ───────────────────────────────────────────────────────

    /// <summary>MIN and MAX of a numeric field across the whole table. Used by: Histogram, BubbleChart.</summary>
    public abstract (double Min, double Max) MinMax(Field numericField);

    // ─── Time-bucketed aggregates ─────────────────────────────────────────────

    /// <summary>COUNT(*) GROUP BY date_trunc(granularity, dateField). Used by: DateTimeFrequency.</summary>
    public abstract SortedDictionary<DateTime, int> CountByTimeBucket(Field dateField, TimeGranularity granularity);

    /// <summary>AVG(numericField) GROUP BY date_trunc(granularity, dateField). Used by: TimeSeriesLine, TimeSeriesEnhanced, GrowthRate.</summary>
    public abstract SortedDictionary<DateTime, double> AverageByTimeBucket(Field numericField, Field dateField, TimeGranularity granularity);

    /// <summary>SUM(numericField) GROUP BY date_trunc(granularity, dateField). Used by: CumulativeSum.</summary>
    public abstract SortedDictionary<DateTime, double> SumByTimeBucket(Field numericField, Field dateField, TimeGranularity granularity);

    /// <summary>COUNT(*) GROUP BY date_trunc(granularity, dateField), catField. Used by: TimeSeriesCategorical.</summary>
    public abstract SortedDictionary<DateTime, Dictionary<string, int>> CountByTimeBucketAndCategory(Field dateField, Field catField, TimeGranularity granularity);

    /// <summary>AVG(numericField) GROUP BY date_trunc(granularity, dateField), catField. Used by: TimeSeriesCategoricalNumeric.</summary>
    public abstract SortedDictionary<DateTime, Dictionary<string, double>> AverageByTimeBucketAndCategory(Field numericField, Field dateField, Field catField, TimeGranularity granularity);

}
