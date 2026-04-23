using GpaCalculator.Models.Db;
using GpaCalculator.Models.Dto;

namespace GpaCalculator.Services;

public interface IClaudeService
{
    Task<SyllabusParseResponse> ParseSyllabusAsync(string syllabusText, string courseName, List<SyllabusTemplate> ragContext);
    Task<ScoreMapResponse> MapScoresAsync(string rawScoresText, List<GradingCategory> categories);
    Task<GradeCalculateResponse> CalculateGradeAsync(GradeCalculateRequest request);
}
