using LQ.Models.AnalystModels;
using LQ.Models.ChartModels;
using LQ.Models.DTOs;
using LQ.Models.TableModels;

namespace LQ.Data.Repositories;

public class TableRepository : TableRepositoryAbstract
{
    protected override Task InsertFile(CSVFile csvFile)
    {
        throw new NotImplementedException();
    }

    public override Task<CSVFile> GetFile(Analyst analyst, string fileName)
    {
        throw new NotImplementedException();
    }

    public override Task<CSVFile> GetFile(int csvFileId)
    {
        throw new NotImplementedException();
    }

    public override Task<List<CSVFile>> GetAllFiles(Analyst analyst)
    {
        throw new NotImplementedException();
    }

    public override Task<bool> FileExists(Analyst analyst, string fileName)
    {
        throw new NotImplementedException();
    }

    protected override Task RemoveFile(CSVFile csvFile)
    {
        throw new NotImplementedException();
    }

    protected override Task RenameFile(CSVFile csvFile, string newName)
    {
        throw new NotImplementedException();
    }

    protected override Task Insert(Table table)
    {
        throw new NotImplementedException();
    }

    public override Task<Table> Get(CSVFile csvFile)
    {
        throw new NotImplementedException();
    }

    public override Task<Table> Get(CSVFile csvFile, List<int> fieldIndexes)
    {
        throw new NotImplementedException();
    }

    public override Task<Table> GetLimited(CSVFile csvFile, int rowLimit)
    {
        throw new NotImplementedException();
    }

    public override Task<Table> GetWithFilter(CSVFile csvFile, List<ValueFilterDTO> filters)
    {
        throw new NotImplementedException();
    }

    public override Task<Table> GetWithFilter(CSVFile csvFile, List<int> fieldIndexes, List<ValueFilterDTO> filters)
    {
        throw new NotImplementedException();
    }

    public override Task<Table> GetWithFilterLimited(CSVFile csvFile, List<ValueFilterDTO> filters, int rowLimit)
    {
        throw new NotImplementedException();
    }

    protected override Task<TryChangeFieldTypeDTO> ChangeFieldType(ChangeFieldTypeDTO dto)
    {
        throw new NotImplementedException();
    }

    public override int CountDistinct(Field field)
    {
        throw new NotImplementedException();
    }

    public override List<(object Key, double Sum)> Sum(Field fieldNumeric, params Field[] fieldGroup)
    {
        throw new NotImplementedException();
    }

    public override (object Key, double Sum) Sum(Field fieldNumeric)
    {
        throw new NotImplementedException();
    }

    public override List<(object Key, int Count)> CountByGroup(Field groupField)
    {
        throw new NotImplementedException();
    }

    public override List<(object KeyA, object KeyB, int Count)> CountByGroup(Field groupField1, Field groupField2)
    {
        throw new NotImplementedException();
    }

    public override List<(object Key, int CountDistinct)> CountDistinctByGroup(Field distinctField, Field groupField)
    {
        throw new NotImplementedException();
    }

    public override List<(object Key, double Average)> AverageByGroup(Field numericField, Field groupField)
    {
        throw new NotImplementedException();
    }

    public override List<(object KeyA, object KeyB, double Average)> AverageByGroup(Field numericField, Field groupFieldA, Field groupFieldB)
    {
        throw new NotImplementedException();
    }

    public override List<(object Key, double Min, double Q1, double Median, double Q3, double Max)> FiveNumberSummaryByGroup(Field numericField, Field groupField)
    {
        throw new NotImplementedException();
    }

    public override (double Min, double Max) MinMax(Field numericField)
    {
        throw new NotImplementedException();
    }

    public override SortedDictionary<DateTime, int> CountByTimeBucket(Field dateField, TimeGranularity granularity)
    {
        throw new NotImplementedException();
    }

    public override SortedDictionary<DateTime, double> AverageByTimeBucket(Field numericField, Field dateField, TimeGranularity granularity)
    {
        throw new NotImplementedException();
    }

    public override SortedDictionary<DateTime, double> SumByTimeBucket(Field numericField, Field dateField, TimeGranularity granularity)
    {
        throw new NotImplementedException();
    }

    public override SortedDictionary<DateTime, Dictionary<string, int>> CountByTimeBucketAndCategory(Field dateField, Field catField, TimeGranularity granularity)
    {
        throw new NotImplementedException();
    }

    public override SortedDictionary<DateTime, Dictionary<string, double>> AverageByTimeBucketAndCategory(Field numericField, Field dateField, Field catField,
        TimeGranularity granularity)
    {
        throw new NotImplementedException();
    }
}