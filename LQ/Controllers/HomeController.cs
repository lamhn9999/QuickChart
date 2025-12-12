using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using LQ.Models.DTOs;
using LQ.Services.Implementations;
using LQ.Services.Interfaces;

namespace LQ.Controllers;

[ApiController]
[Route("/")]
public class HomeController : Controller
{
    private ICSVService _csvService;
    public HomeController(ICSVService csvService)
    {
        _csvService = csvService;
    }
    [HttpGet]
    public IActionResult Show()
    {
        DirectoryInfo di = new DirectoryInfo(BaseDTO.SampleCSVsPath);
        List<string> SampleCSVs = di.EnumerateFiles().Select(f => f.Name).ToList();
        return View("Home", SampleCSVs);
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public IActionResult Upload(IFormFile file)
    {
        return Ok($"Received {file.FileName}");
    }
}