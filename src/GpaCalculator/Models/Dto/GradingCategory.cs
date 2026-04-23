namespace GpaCalculator.Models.Dto;

public class GradingCategory
{
    public string Name { get; set; } = "";
    public double Weight { get; set; }
    public double EarnedPoints { get; set; } = 0;
    public double TotalPoints { get; set; } = 0;
}
