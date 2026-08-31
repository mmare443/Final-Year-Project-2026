using Microsoft.AspNetCore.Mvc;

namespace LCC_CMS_Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new
    {
        status = "ok",
        message = "LCC-CMS API is running",
        timestampUtc = DateTime.UtcNow,
    });
}
