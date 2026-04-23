namespace GpaCalculator.Models.Db;

public class SyllabusTemplate
{
    public int Id { get; set; }
    public string CourseName { get; set; } = "";
    public string RawText { get; set; } = "";
    public string ParsedCategories { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
