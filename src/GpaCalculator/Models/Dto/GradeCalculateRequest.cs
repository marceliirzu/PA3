namespace GpaCalculator.Models.Dto;

public class GradeCalculateRequest
{
    public string SessionId { get; set; } = "";
    public string CourseName { get; set; } = "";
    public List<GradingCategory> Categories { get; set; } = new();
    public Dictionary<string, double> GradingScale { get; set; } = new();
}
