namespace GpaCalculator.Models.Dto;

public class SyllabusParseResponse
{
    public string CourseName { get; set; } = "";
    public Dictionary<string, double> GradingScale { get; set; } = new();
    public List<GradingCategory> Categories { get; set; } = new();
    public int TemplateId { get; set; }
}
