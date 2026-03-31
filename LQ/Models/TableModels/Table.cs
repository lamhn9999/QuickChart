using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace LQ.Models.TableModels;

public class FieldValues
{
    public required Field Field { get; set; }
    public required List<Value> Values { get; set; }
}
public class Table
{
    public int RowCount { get; private set; } = 0;
    public required CSVFile CSVFile { get; set; }
    public required List<FieldValues> Columns { get; set; } = new();

    [SetsRequiredMembers]
    public Table(CSVFile csvFile)
    {
        CSVFile = csvFile;
    }

    public void AddRow(int index, List<string?> valuestrings)
    {
        for (int i = 0; i < Columns.Count; i++)
        {
            Console.WriteLine(Columns[i].Field.Name);
            Value newValue = new Value{ValueString = valuestrings[i],  RowIndex = index, Field = Columns[i].Field};
            Console.WriteLine($"{i}, {newValue.ValueString}, {Columns.Count}");
            Columns[i].Values.Add(newValue);
        }

        RowCount++;
    }

    public void Show(int maxLen)
    {
        Debug.WriteLine(CSVFile.ID);
        for (int i = 0; i < Columns.Count; i++)
        {
            Debug.Write("{0," + maxLen.ToString() + "}", Columns[i].Field.Name);
        }
        Debug.WriteLine("");
        for (int i = 0; i < RowCount; i++)
        {
            for (int j = 0; j < Columns.Count; j++)
            {
                Debug.Write("{0," + maxLen.ToString() + "}", Columns[j].Values[i].ValueCasted.ToString());
            }
            Debug.WriteLine("");
        }
    }

    public string[][] GetDisplay()
    {
        string[][] tableString = new string[RowCount+1][];
        for (int i = 0; i < RowCount+1; i++)
        {
            tableString[i] = new string[Columns.Count];
        }

        for (int i = 0; i < Columns.Count; i++)
        {
            tableString[0][i] = Columns[i].Field.Name;
        }

        for (int i = 0; i < Columns.Count; i++)
        {
            for (int j = 0; j < RowCount; j++)
            {
                tableString[j+1][i] = Columns[i].Values[j].ValueString;
            }
        }

        // for (int i = 0; i <= RowCount; i++)
        // {
        //     for (int j = 0; j < Columns.Count; j++)
        //     {
        //         System.Console.Write($"{tableString[i][j]},");
        //     }
        //
        //     System.Console.WriteLine();
        // }
        return tableString;
    }
    public void SetRowCount()
    {
        RowCount = (Columns.Count == 0) ? 0 : Columns[0].Values.Count;
    }
}