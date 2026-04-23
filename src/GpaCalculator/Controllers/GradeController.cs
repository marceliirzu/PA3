using GpaCalculator.Data;
using GpaCalculator.Models.Db;
using GpaCalculator.Models.Dto;
using GpaCalculator.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace GpaCalculator.Controllers;

[ApiController]
[Route("api/grade")]
public class GradeController : ControllerBase
{
    private readonly IClaudeService _claude;
    private readonly AppDbContext _db;

    public GradeController(IClaudeService claude, AppDbContext db)
    {
        _claude = claude;
        _db = db;
    }

    [HttpPost("calculate")]
    public async Task<IActionResult> Calculate([FromBody] GradeCalculateRequest request)
    {
        if (request.Categories == null || request.Categories.Count == 0)
            return BadRequest("Categories are required.");

        var response = await _claude.CalculateGradeAsync(request);

        var session = new GradebookSession
        {
            SessionId = request.SessionId,
            CourseName = request.CourseName,
            Categories = JsonSerializer.Serialize(request.Categories),
            FinalGrade = response.LetterGrade,
            GpaPoints = (decimal)response.GpaPoints,
            CreatedAt = DateTime.UtcNow
        };
        _db.GradebookSessions.Add(session);
        await _db.SaveChangesAsync();

        return Ok(response);
    }
}
