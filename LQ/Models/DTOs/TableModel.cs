namespace LQ.Models.DTOs;

public class TableModel
{
    public required List<ColumnModel> Columns;
    public int NumberOfRows { get; private set; }

    public bool AddRow(List<string?> newValues)
    {
        if (newValues.Count != Columns.Count) return false;
        for (int i = 0; i < newValues.Count; i++)
        {
            Columns[i].AddValue(newValues[i]);
        }
        NumberOfRows++;
        return true;
    }

    public void Show()
    {
        for (int i = 0; i < Columns.Count; i++)
        {
            System.Console.Write("{0,10}", Columns[i].Field.FieldName);
        }

        System.Console.WriteLine();
        for (int i = 0; i < NumberOfRows; i++)
        {
            for (int j = 0; j < Columns.Count; j++)
            {
                System.Console.Write("{0,10}", Columns[j].Values[i].Value);
            }
            System.Console.WriteLine();
        }
    }
}