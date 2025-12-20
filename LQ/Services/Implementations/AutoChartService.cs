using LQ.Models.ChartModels;
using LQ.Models.TableModels;
using LQ.Services.Interfaces;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace LQ.Services.Implementations;

public class AutoChartService : IAutoChartService
{
    private readonly ILQDbService _lqDbService;
    public AutoChartService(ILQDbService lqDbService)
    {
        _lqDbService = lqDbService;
    }
    public void DetectCategory(FieldValues fieldValues)
    {
        List <Category> result =  new List<Category>();
        List<string> valueStringlist = new List<string>();
        foreach (Value value in fieldValues.Values)
        {
            if (string.IsNullOrWhiteSpace(value.ValueString)) continue;
            valueStringlist.Add(value.ValueString.Trim());
        }

        double uniqPercentage = (1.0 * valueStringlist.Distinct().ToList().Count) / (fieldValues.Values.Count * 1.0);
        
        // Categorical
        if (uniqPercentage <= Field.CATEGORICAL_THRESHOLD &&
            (fieldValues.Field.Type == typeof(string) || fieldValues.Field.Type == typeof(int)) ||
            fieldValues.Field.Type == typeof(bool)) result.Add(Category.Categorical);
        
        // Identifier
        if ((uniqPercentage >= Field.IDENTIFIER_THRESHOLD &&
            (fieldValues.Field.Type == typeof(string) || 
             fieldValues.Field.Type == typeof(int))) ||
            (fieldValues.Field.Type == typeof(string) && !result.Contains(Category.Categorical))) result.Add(Category.Identifier);
        
        // Numeric
        if ((fieldValues.Field.Type == typeof(int) || 
            fieldValues.Field.Type == typeof(double) || 
            fieldValues.Field.Type == typeof(DateTime)) && 
            !result.Contains(Category.Categorical)) result.Add(Category.Numeric);

        fieldValues.Field.Categories = result;
    }

    public List<Chart> DetectChart(int csvfileid, List<int> fieldindexes)
    {
        Table tempTable = _lqDbService.GetTable(csvfileid, fieldindexes);
        tempTable.GetDisplay();
        return new List<Chart>();
    }
}