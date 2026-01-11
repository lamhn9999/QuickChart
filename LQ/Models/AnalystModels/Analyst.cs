using LQ.Models.TableModels;
using Microsoft.AspNetCore.Identity;

namespace LQ.Models.AnalystModels;

public class Analyst : IdentityUser
{
    public const int NameLowerBound = 1;
    public const int NameUpperBound = 100;

    public ICollection<CSVFile> CSVFiles { get; } = new HashSet<CSVFile>();

    private Analyst() { } // for EF-Core

    public static Analyst? GiveBirth(string? name)
    {
        if (!ValidateName(name))
            return null;
        return new Analyst
        {
            UserName = name.Trim()
        };
    }

    private static bool ValidateName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        name = name.Trim();

        if (name.Length is < NameLowerBound or > NameUpperBound)
            return false;

        return name.All(c => char.IsLetterOrDigit(c) || c == '_' || c == '.');
    }
}
