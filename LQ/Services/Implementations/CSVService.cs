using LQ.Data.Database;
using LQ.Models.TableModels;
using LQ.Models.AnalystModels;
using LQ.Services.Interfaces;

namespace LQ.Services.Implementations;

public class CSVService : ICSVService
{
    private readonly ILQDbService _lqDbService;

    public CSVService(ILQDbService lqdbService)
    {
        _lqDbService = lqdbService;
    }
    public bool Validate(Analyst analyst, string path)
    {
        int cnt1 = -1, cnt2 = -1;
        using (var reader = new StreamReader(path))
        {
            string? line = null;
            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                int cnt = 0, unit = 1;
                for (int i = 0; i < line.Length; i++)
                {
                    if (line[i] == ',') cnt += unit; 
                    else if (line[i] == '"') unit = 1 - unit;
                }

                if (cnt1 == -1) cnt1 = cnt;
                else if (cnt != cnt1 && cnt2 == -1) { 
                    cnt2 = cnt; 
                    if (Math.Abs(cnt1 - cnt2) != 1) return false; 
                }
                else if (cnt1 != cnt && cnt2 != cnt) return false;
            }
        }
        return true;
    }

    private Table Interpret(CSVFile csvFile, string path)
    {
        Table newTable = new Table{CSVFile = csvFile, Columns = new List<FieldValues>()};

        List<string> fields = new List<string>();
        List<string> lines = new List<string>();
        using (var reader = new StreamReader(path))
        {
            string? headerLine = null;
            while ((headerLine = reader.ReadLine()) != null)
            {
                if (!string.IsNullOrWhiteSpace(headerLine))
                    break;
            }
        
            if (headerLine == null)
            {
                return newTable;
            }
        
            fields = headerLine.Split(',').Select(f => f.Trim()).ToList();
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (!string.IsNullOrWhiteSpace(line))
                    lines.Add(line);
            }
        }
        
        for (int i = 0; i < fields.Count; i++)
        {
            string trimmedfield = fields[i].Trim();
            // if (trimmedfield == "") 
            Field newField = new Field()
                { Name = trimmedfield, CSVFile = csvFile, Index = i, Categories = new List<Category>() };
            newTable.Columns.Add(new FieldValues{Field = newField, Values = new List<Value>()});
        }
        
        for (int i = 0; i < lines.Count; i++)
        {
            List<string> splits = lines[i].Split(',').ToList();
            List<string?> newValues = new List<string?>();
            foreach (string split in splits)
            {
                if(string.IsNullOrEmpty(split)) newValues.Add(null);
                else newValues.Add(split);
            }
            newTable.AddRow(i, newValues);
        }

        return newTable;
    }

    public bool ProcessUploadedCSV(Analyst analyst, string path)
    {
        if (!Validate(analyst, path)) return false;
        CSVFile newCSVFile = new CSVFile{Name = Path.GetFileName(path), Analyst = analyst, CreatedTime = DateTime.UtcNow, ModifiedTime = DateTime.UtcNow};
        Table newTable = Interpret(newCSVFile, path);
        _lqDbService.InsertTable(newTable);
        // newTable.Show(25);
        return true;
    }
}