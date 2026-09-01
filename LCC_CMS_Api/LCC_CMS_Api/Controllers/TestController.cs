using LCC_CMS_Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LCC_CMS_Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TestController : ControllerBase
{
    private readonly LccCmsDbContext _dbContext;

    public TestController(LccCmsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        var databaseConnected = await _dbContext.Database.CanConnectAsync();

        var studentCount = databaseConnected ? await _dbContext.Students.CountAsync() : 0;
        var departmentCount = databaseConnected ? await _dbContext.Departments.CountAsync() : 0;
        var courseCount = databaseConnected ? await _dbContext.Courses.CountAsync() : 0;

        return Ok(new
        {
            databaseConnected,
            studentCount,
            departmentCount,
            courseCount,
            serverTime = DateTime.UtcNow,
        });
    }
}