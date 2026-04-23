namespace GpaCalculator.Models.Db;

public class GradebookSession
{
    public int Id { get; set; }
    public string SessionId { get; set; } = "";
    public string CourseName { get; set; } = "";
    public string Categories { get; set; } = "";
    public string? Scores { get; set; }
    public string? FinalGrade { get; set; }
    public decimal? GpaPoints { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
