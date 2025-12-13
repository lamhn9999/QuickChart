using LQ.Models.TableModels;

namespace LQ.Models.ViewModels;

public enum Zaction
{
    Invalid,
    Sucess,
    Display
}
public class Result
{
    public Zaction Action { get; set; }
    public Table? Table { get; set; }
}