using LQ.Models.DTOs;
using LQ.Services.Interfaces;

namespace LQ.Services.Implementations;

public class TableService
{
    public static TableModel InterpretCsv(string path)
    {
        TableModel newTable = new TableModel{Columns = new List<ColumnModel>()};
        
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
        
        foreach (string field in fields)
        {
            string trimmedfield = field.Trim();
            // if (trimmedfield == "") 
            FieldModel newField = new FieldModel
                { FieldName = trimmedfield, FieldType = typeof(string), FieldCategories = new List<FieldModel.FieldCategory>() };
            ColumnModel newColumn = new ColumnModel { Field = newField, Values = new List<ValueModel>() };
            newTable.Columns.Add(newColumn);
        }

        foreach (string line in lines)
        {
            List<string> splits = line.Split(',').Select(f => f.Trim()).ToList();
            List<string?> newValues = new  List<string?>();
            foreach (string split in splits)
            {
                if(string.IsNullOrEmpty(split)) newValues.Add(null);
                else newValues.Add(split);
            }
            newTable.AddRow(newValues);
        }

        return newTable;
    }
}