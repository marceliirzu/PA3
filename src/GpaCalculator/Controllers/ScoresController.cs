using GpaCalculator.Services;
using GpaCalculator.Models.Dto;
using Microsoft.AspNetCore.Mvc;

namespace GpaCalculator.Controllers;

[ApiController]
[Route("api/scores")]
public class ScoresController : ControllerBase
{
    private readonly IClaudeService _claude;

    public ScoresController(IClaudeService claude)
    {
        _claude = claude;
    }

    [HttpPost("map")]
    public async Task<IActionResult> Map([FromBody] ScoreMapRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RawScoresText))
            return BadRequest("RawScoresText is required.");

        var response = await _claude.MapScoresAsync(request.RawScoresText, request.Categories);
        return Ok(response);
    }
}
