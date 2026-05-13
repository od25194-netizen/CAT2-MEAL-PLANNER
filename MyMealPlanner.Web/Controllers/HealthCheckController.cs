using Microsoft.AspNetCore.Mvc;

namespace MyMealPlanner.Web.Controllers;

[Route("health")]
public class HealthCheckController : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        return Ok("Healthy");
    }
}
