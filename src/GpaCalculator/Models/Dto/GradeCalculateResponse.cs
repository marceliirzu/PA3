namespace GpaCalculator.Models.Dto;

public record CategoryBreakdown(string Category, double Contribution);

public class GradeCalculateResponse
{
    public double WeightedPercentage { get; set; }
    public string LetterGrade { get; set; } = "";
    public double GpaPoints { get; set; }
    public List<CategoryBreakdown> Breakdown { get; set; } = new();
}
