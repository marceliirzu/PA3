using GpaCalculator.Models.Db;

namespace GpaCalculator.Services;

public interface IRagService
{
    Task<List<SyllabusTemplate>> GetSimilarTemplatesAsync(string courseName, int limit = 3);
    Task<int> SaveTemplateAsync(string courseName, string rawText, string parsedCategoriesJson);
}
