using System.Text.Json;
using System.Text.Json.Nodes;
using Anthropic.SDK;
using Anthropic.SDK.Constants;
using Anthropic.SDK.Messaging;
using GpaCalculator.Models.Db;
using GpaCalculator.Models.Dto;
using CT = Anthropic.SDK.Common.Tool;
using CF = Anthropic.SDK.Common.Function;

namespace GpaCalculator.Services;

public class ClaudeService : IClaudeService
{
    private readonly AnthropicClient _client;
    private readonly ILogger<ClaudeService> _logger;
    private static readonly JsonSerializerOptions _jsonOpts = new() { PropertyNameCaseInsensitive = true };

    public ClaudeService(IConfiguration config, ILogger<ClaudeService> logger)
    {
        var apiKey = config["ClaudeApiKey"] ?? Environment.GetEnvironmentVariable("CLAUDE_API_KEY") ?? "";
        _client = new AnthropicClient(apiKey);
        _logger = logger;
    }

    public async Task<SyllabusParseResponse> ParseSyllabusAsync(string syllabusText, string courseName, List<SyllabusTemplate> ragContext)
    {
        var ragBlock = "";
        if (ragContext.Count > 0)
        {
            var examples = ragContext.Select((t, i) => $"Example {i + 1} ({t.CourseName}):\n{t.ParsedCategories}");
            ragBlock = "Here are examples of previously parsed syllabi for reference:\n\n" + string.Join("\n\n", examples) + "\n\n";
        }

        var userMessage = ragBlock + $"Now parse this syllabus:\n<syllabus>\n{syllabusText}\n</syllabus>";

        var parameters = new MessageParameters
        {
            Model = AnthropicModels.Claude46Sonnet,
            MaxTokens = 1024,
            System = new List<SystemMessage>
            {
                new SystemMessage("You are a syllabus parser. Extract grading information and return ONLY valid JSON. No markdown, no explanation.\n\nReturn exactly this structure:\n{\"courseName\": \"string\", \"gradingScale\": {\"A\": 90, \"B\": 80, \"C\": 70, \"D\": 60}, \"categories\": [{\"name\": \"string\", \"weight\": 0.XX}]}\n\nRules:\n- gradingScale MUST come from the syllabus text. Extract the actual letter grade cutoffs (e.g. A=93, A-=90, B+=87). If the syllabus uses +/- grades include them. If no scale is found, use the standard {\"A\":90,\"B\":80,\"C\":70,\"D\":60}.\n- gradingScale keys are letter grades, values are minimum percentages (numbers, not strings).\n- category weights must sum to 1.0.\n- Weights must be decimals (0.40 not 40).")
            },
            Messages = new List<Message>
            {
                new Message(RoleType.User, userMessage, null)
            }
        };

        var response = await _client.Messages.GetClaudeMessageAsync(parameters);
        var text = response.Content.OfType<TextContent>().FirstOrDefault()?.Text ?? "{}";

        text = text.Trim();
        if (text.StartsWith("```")) text = text.Substring(text.IndexOf('\n') + 1);
        if (text.EndsWith("```")) text = text.Substring(0, text.LastIndexOf("```"));
        text = text.Trim();

        var parsed = JsonSerializer.Deserialize<JsonObject>(text, _jsonOpts) ?? new JsonObject();

        var result = new SyllabusParseResponse
        {
            CourseName = parsed["courseName"]?.GetValue<string>() ?? courseName
        };

        if (parsed["gradingScale"] is JsonObject scale)
        {
            foreach (var kv in scale)
            {
                if (kv.Value != null)
                    result.GradingScale[kv.Key] = kv.Value.GetValue<double>();
            }
        }

        if (parsed["categories"] is JsonArray cats)
        {
            foreach (var cat in cats)
            {
                if (cat is JsonObject catObj)
                {
                    result.Categories.Add(new GradingCategory
                    {
                        Name = catObj["name"]?.GetValue<string>() ?? "",
                        Weight = catObj["weight"]?.GetValue<double>() ?? 0
                    });
                }
            }
        }

        return result;
    }

    public async Task<ScoreMapResponse> MapScoresAsync(string rawScoresText, List<GradingCategory> categories)
    {
        var schema = JsonNode.Parse(@"{
            ""type"": ""object"",
            ""properties"": {
                ""mappings"": {
                    ""type"": ""array"",
                    ""items"": {
                        ""type"": ""object"",
                        ""properties"": {
                            ""categoryName"": { ""type"": ""string"" },
                            ""earnedPoints"": { ""type"": ""number"" },
                            ""totalPoints"": { ""type"": ""number"" }
                        },
                        ""required"": [""categoryName"", ""earnedPoints"", ""totalPoints""]
                    }
                }
            },
            ""required"": [""mappings""]
        }");

        var fn = new CF("map_scores_to_categories", "Maps raw gradebook score entries to their grading categories", schema);
        var tools = new List<CT> { new CT(fn) };

        var categoriesJson = JsonSerializer.Serialize(categories, _jsonOpts);
        var categoryNames = string.Join(", ", categories.Select(c => $"\"{c.Name}\""));
        var userMessage = $"""
You are mapping student scores to grading categories. The categories are:
{categoriesJson}

Rules — follow EXACTLY:
1. Use the EXACT category names listed above (copy spelling, capitalization, spaces).
2. For each category, sum ALL matching earned points and ALL matching total points from the scores below.
   Example: if "Exams" has Exam1=88/100 and Exam2=92/100 → earnedPoints=180, totalPoints=200.
3. If a category name implies ranking (e.g. "Highest Exam Score", "Second Highest Exam Score", "Third Highest Exam Score"),
   collect all scores of that type, sort descending by percentage, and assign them in order.
4. Every category in [{categoryNames}] MUST appear in your output. If no scores match, use earnedPoints=0, totalPoints=0.
5. Do NOT invent new category names.

Scores:
<scores>
{rawScoresText}
</scores>

Call map_scores_to_categories now.
""";

        var messages = new List<Message>
        {
            new Message(RoleType.User, userMessage, null)
        };

        var parameters = new MessageParameters
        {
            Model = AnthropicModels.Claude46Sonnet,
            MaxTokens = 1024,
            Tools = tools,
            Messages = messages
        };

        var response = await _client.Messages.GetClaudeMessageAsync(parameters);

        ToolUseContent? capturedToolUse = null;

        while (response.StopReason == "tool_use")
        {
            var toolUse = response.Content.OfType<ToolUseContent>().FirstOrDefault();
            if (toolUse == null) break;

            capturedToolUse = toolUse;

            // Add assistant response (contains tool_use block)
            messages.Add(new Message { Role = RoleType.Assistant, Content = response.Content });

            // Add tool result
            messages.Add(new Message
            {
                Role = RoleType.User,
                Content = new List<ContentBase>
                {
                    new ToolResultContent
                    {
                        ToolUseId = toolUse.Id,
                        Content = new List<ContentBase> { new TextContent { Text = "Mappings accepted." } }
                    }
                }
            });

            parameters.Messages = messages;
            response = await _client.Messages.GetClaudeMessageAsync(parameters);
        }

        if (capturedToolUse != null)
        {
            var inputJson = capturedToolUse.Input?.ToString() ?? "{}";
            var inputObj = JsonSerializer.Deserialize<JsonObject>(inputJson, _jsonOpts);
            var mappings = inputObj?["mappings"] as JsonArray;

            if (mappings != null)
            {
                var mapped = new List<GradingCategory>();
                foreach (var m in mappings)
                {
                    if (m is JsonObject mObj)
                    {
                        var catName = mObj["categoryName"]?.GetValue<string>() ?? "";
                        mapped.Add(new GradingCategory
                        {
                            Name = catName,
                            Weight = categories.FirstOrDefault(c => c.Name == catName)?.Weight ?? 0,
                            EarnedPoints = mObj["earnedPoints"]?.GetValue<double>() ?? 0,
                            TotalPoints = mObj["totalPoints"]?.GetValue<double>() ?? 0
                        });
                    }
                }
                return new ScoreMapResponse { MappedScores = mapped };
            }
        }

        return new ScoreMapResponse { MappedScores = categories };
    }

    public async Task<GradeCalculateResponse> CalculateGradeAsync(GradeCalculateRequest request)
    {
        var calcSchema = JsonNode.Parse(@"{
            ""type"": ""object"",
            ""properties"": {
                ""categories"": {
                    ""type"": ""array"",
                    ""items"": {
                        ""type"": ""object"",
                        ""properties"": {
                            ""name"": {""type"": ""string""},
                            ""weight"": {""type"": ""number""},
                            ""earnedPoints"": {""type"": ""number""},
                            ""totalPoints"": {""type"": ""number""}
                        }
                    }
                }
            },
            ""required"": [""categories""]
        }");

        var letterSchema = JsonNode.Parse(@"{
            ""type"": ""object"",
            ""properties"": {
                ""percentage"": { ""type"": ""number"" },
                ""gradingScale"": { ""type"": ""object"" }
            },
            ""required"": [""percentage"", ""gradingScale""]
        }");

        var gpaSchema = JsonNode.Parse(@"{
            ""type"": ""object"",
            ""properties"": {
                ""letterGrade"": { ""type"": ""string"" }
            },
            ""required"": [""letterGrade""]
        }");

        var tools = new List<CT>
        {
            new CT(new CF("calculate_weighted_grade", "Calculates weighted average percentage across all categories", calcSchema)),
            new CT(new CF("convert_to_letter_grade", "Converts a percentage to a letter grade using the course grading scale", letterSchema)),
            new CT(new CF("get_gpa_points", "Returns GPA point value (4.0 scale) for a letter grade", gpaSchema))
        };

        var categoriesJson = JsonSerializer.Serialize(request.Categories, _jsonOpts);
        var scaleJson = JsonSerializer.Serialize(request.GradingScale, _jsonOpts);
        var userMessage = $"Calculate the grade for {request.CourseName}. Categories: {categoriesJson}. Grading scale: {scaleJson}. Use the tools in order: first calculate_weighted_grade, then convert_to_letter_grade, then get_gpa_points.";

        var messages = new List<Message>
        {
            new Message(RoleType.User, userMessage, null)
        };

        var parameters = new MessageParameters
        {
            Model = AnthropicModels.Claude46Sonnet,
            MaxTokens = 1024,
            Tools = tools,
            Messages = messages
        };

        var response = await _client.Messages.GetClaudeMessageAsync(parameters);

        double weightedPct = 0;
        string letterGrade = "F";
        double gpaPoints = 0;
        var breakdown = new List<CategoryBreakdown>();

        while (response.StopReason == "tool_use")
        {
            var toolUse = response.Content.OfType<ToolUseContent>().FirstOrDefault();
            if (toolUse == null) break;

            string toolResult;

            if (toolUse.Name == "calculate_weighted_grade")
            {
                var inputJson = toolUse.Input?.ToString() ?? "{}";
                var inputObj = JsonSerializer.Deserialize<JsonObject>(inputJson, _jsonOpts);
                var cats = inputObj?["categories"] as JsonArray ?? new JsonArray();

                double sum = 0;
                foreach (var cat in cats)
                {
                    if (cat is JsonObject catObj)
                    {
                        var earned = catObj["earnedPoints"]?.GetValue<double>() ?? 0;
                        var total = catObj["totalPoints"]?.GetValue<double>() ?? 0;
                        var weight = catObj["weight"]?.GetValue<double>() ?? 0;
                        var name = catObj["name"]?.GetValue<string>() ?? "";
                        double pct = total > 0 ? (earned / total) * 100 : 0;
                        double contribution = pct * weight;
                        sum += contribution;
                        breakdown.Add(new CategoryBreakdown(name, contribution));
                    }
                }
                weightedPct = sum;
                toolResult = weightedPct.ToString("F2");
            }
            else if (toolUse.Name == "convert_to_letter_grade")
            {
                var inputJson = toolUse.Input?.ToString() ?? "{}";
                var inputObj = JsonSerializer.Deserialize<JsonObject>(inputJson, _jsonOpts);
                var pct = inputObj?["percentage"]?.GetValue<double>() ?? weightedPct;
                var scale = inputObj?["gradingScale"] as JsonObject ?? new JsonObject();

                var sortedScale = scale
                    .Select(kv => (Grade: kv.Key, Min: kv.Value?.GetValue<double>() ?? 0))
                    .OrderByDescending(x => x.Min)
                    .ToList();

                letterGrade = "F";
                foreach (var (grade, min) in sortedScale)
                {
                    if (pct >= min)
                    {
                        letterGrade = grade;
                        break;
                    }
                }
                toolResult = letterGrade;
            }
            else if (toolUse.Name == "get_gpa_points")
            {
                var inputJson = toolUse.Input?.ToString() ?? "{}";
                var inputObj = JsonSerializer.Deserialize<JsonObject>(inputJson, _jsonOpts);
                var grade = inputObj?["letterGrade"]?.GetValue<string>() ?? letterGrade;

                gpaPoints = grade switch
                {
                    "A+" or "A" => 4.0,
                    "A-" => 3.7,
                    "B+" => 3.3,
                    "B" => 3.0,
                    "B-" => 2.7,
                    "C+" => 2.3,
                    "C" => 2.0,
                    "C-" => 1.7,
                    "D+" => 1.3,
                    "D" => 1.0,
                    "D-" => 0.7,
                    _ => 0.0
                };
                toolResult = gpaPoints.ToString("F1");
            }
            else
            {
                toolResult = "ok";
            }

            // Add assistant response (contains tool_use block)
            messages.Add(new Message { Role = RoleType.Assistant, Content = response.Content });

            // Add tool result
            messages.Add(new Message
            {
                Role = RoleType.User,
                Content = new List<ContentBase>
                {
                    new ToolResultContent
                    {
                        ToolUseId = toolUse.Id,
                        Content = new List<ContentBase> { new TextContent { Text = toolResult } }
                    }
                }
            });

            parameters.Messages = messages;
            response = await _client.Messages.GetClaudeMessageAsync(parameters);
        }

        return new GradeCalculateResponse
        {
            WeightedPercentage = weightedPct,
            LetterGrade = letterGrade,
            GpaPoints = gpaPoints,
            Breakdown = breakdown
        };
    }
}
