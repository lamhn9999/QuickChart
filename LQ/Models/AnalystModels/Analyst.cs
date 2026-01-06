using LQ.Models.TableModels;
using Microsoft.AspNetCore.Identity;

namespace LQ.Models.AnalystModels;

public class Analyst
{
    public int ID { get; private set; }
    public string Name { get; private set; }

    public const int NameLowerBound = 1;
    public const int NameUpperBound = 100;

    private ICollection<CSVFile> CSVFiles { get; } = new HashSet<CSVFile>();

    protected Analyst() { } // for EF-Core

    private Analyst(string name)
    {
        Name = name;
    }

    private static bool Validate(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        name = name.Trim();

        if (name.Length is < NameLowerBound or > NameUpperBound)
            return false;

        return name.All(c => char.IsLetterOrDigit(c) || c == '_' || c == ' ');
    }

    public static Analyst? GiveBirth(string? name)
    {
        return Validate(name)
            ? new Analyst(name)
            : null;
    }
}
