using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
namespace LQ.Controllers;

[ApiController]
[Route("/")]
public class HomeController : Controller
{
    [HttpGet]
    public IActionResult Show()
    {
        DirectoryInfo di = new DirectoryInfo("/home/lam/LQ/LQ/Data/SampleCSVs");
        List<string> SampleCSVs = di.EnumerateFiles().Select(f => f.Name).ToList();
        return View("Home", SampleCSVs);
    }
}