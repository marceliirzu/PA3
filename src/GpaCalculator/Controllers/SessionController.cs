using GpaCalculator.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GpaCalculator.Controllers;

[ApiController]
[Route("api/session")]
public class SessionController : ControllerBase
{
    private readonly AppDbContext _db;

    public SessionController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("{sessionId}")]
    public async Task<IActionResult> Get(string sessionId)
    {
        var sessions = await _db.GradebookSessions
            .Where(s => s.SessionId == sessionId)
            .ToListAsync();

        var courses = sessions
            .GroupBy(s => s.CourseName)
            .Select(g => new
            {
                courseName = g.Key,
                finalGrade = g.OrderByDescending(s => s.CreatedAt).First().FinalGrade,
                gpaPoints = (double?)g.OrderByDescending(s => s.CreatedAt).First().GpaPoints
            })
            .ToList();

        var semesterGpa = courses
            .Where(c => c.gpaPoints.HasValue)
            .Select(c => c.gpaPoints!.Value)
            .DefaultIfEmpty(0)
            .Average();

        return Ok(new { courses, semesterGpa });
    }
}
