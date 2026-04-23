using GpaCalculator.Services;
using GpaCalculator.Models.Dto;
using Microsoft.AspNetCore.Mvc;

namespace GpaCalculator.Controllers;

[ApiController]
[Route("api/syllabus")]
public class SyllabusController : ControllerBase
{
    private readonly IClaudeService _claude;
    private readonly IRagService _rag;

    public SyllabusController(IClaudeService claude, IRagService rag)
    {
        _claude = claude;
        _rag = rag;
    }

    [HttpPost("parse")]
    public async Task<IActionResult> Parse([FromBody] SyllabusParseRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SyllabusText))
            return BadRequest("SyllabusText is required.");

        var ragContext = await _rag.GetSimilarTemplatesAsync(request.CourseName);
        var response = await _claude.ParseSyllabusAsync(request.SyllabusText, request.CourseName, ragContext);

        var categoriesJson = System.Text.Json.JsonSerializer.Serialize(response.Categories);
        var templateId = await _rag.SaveTemplateAsync(request.CourseName, request.SyllabusText, categoriesJson);
        response.TemplateId = templateId;

        return Ok(response);
    }
}
