namespace GpaCalculator.Models.Dto;

public class ScoreMapRequest
{
    public string RawScoresText { get; set; } = "";
    public List<GradingCategory> Categories { get; set; } = new();
}
