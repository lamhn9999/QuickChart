namespace LQ.Models.TableModels;

public class FieldValues
{
    public Field Field { get; set; }
    public List<Value> Values { get; set; }
}
public class Table
{
    public int RowCount { get; private set; } = 0;
    public CSVFile? CSVFile { get; set; }
    public List<FieldValues> Columns { get; set; } = new();

    public void AddRow(int index, List<string?> valuestrings)
    {
        for (int i = 0; i < Columns.Count; i++)
        {
            Value newValue = new Value{ValueString = valuestrings[i],  RowIndex = index, Field = Columns[i].Field};
            Columns[i].Values.Add(newValue);
        }

        RowCount++;
    }

    public void Show(int maxLen)
    {
        System.Console.WriteLine(CSVFile.Name);
        for (int i = 0; i < Columns.Count; i++)
        {
            System.Console.Write("{0," + maxLen.ToString() + "}", Columns[i].Field.Name);
        }
        System.Console.WriteLine();
        for (int i = 0; i < RowCount; i++)
        {
            for (int j = 0; j < Columns.Count; j++)
            {
                System.Console.Write("{0," + maxLen.ToString() + "}", Columns[j].Values[i].ValueString);
                System.Console.Write("{0," + maxLen.ToString() + "}", Columns[j].Values[i].Field.Name);
            }
            System.Console.WriteLine();
        }
    }
    public void SetRowCount()
    {
        RowCount = (Columns.Count == 0) ? 0 : Columns[0].Values.Count;
    }
}