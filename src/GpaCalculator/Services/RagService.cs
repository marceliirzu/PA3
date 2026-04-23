using GpaCalculator.Data;
using GpaCalculator.Models.Db;
using Microsoft.EntityFrameworkCore;

namespace GpaCalculator.Services;

public class RagService : IRagService
{
    private readonly AppDbContext _db;

    public RagService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<SyllabusTemplate>> GetSimilarTemplatesAsync(string courseName, int limit = 3)
    {
        var words = courseName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return new List<SyllabusTemplate>();

        var query = _db.SyllabusTemplates.AsQueryable();
        var results = await query
            .Where(t => words.Any(w => t.CourseName.Contains(w)))
            .OrderByDescending(t => t.CreatedAt)
            .Take(limit)
            .ToListAsync();

        return results;
    }

    public async Task<int> SaveTemplateAsync(string courseName, string rawText, string parsedCategoriesJson)
    {
        var template = new SyllabusTemplate
        {
            CourseName = courseName,
            RawText = rawText,
            ParsedCategories = parsedCategoriesJson,
            CreatedAt = DateTime.UtcNow
        };

        _db.SyllabusTemplates.Add(template);
        await _db.SaveChangesAsync();
        return template.Id;
    }
}
