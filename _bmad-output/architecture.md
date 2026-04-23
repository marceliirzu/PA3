# Architecture — AI-Powered GPA Calculator
## MIS 321 PA3

---

## 1. System Overview

```
[GitHub Pages: https://marceliirzu.github.io/PA3/]
  index.html / style.css / app.js
          |  fetch() to Railway URL
          v
[ASP.NET Core Web API — C# on Railway]
  ├── SyllabusController  →  RagService (MySQL)  →  ClaudeService (parse)
  ├── ScoresController    →  ClaudeService (map_scores_to_categories tool call)
  ├── GradeController     →  ClaudeService (calculate_weighted_grade / convert_to_letter_grade / get_gpa_points tools)
  └── SessionController   →  AppDbContext (MySQL read)
          |
          v
[MySQL on Railway]
  ├── syllabus_templates  (RAG store)
  └── gradebook_sessions  (session state)
```

---

## 2. Directory Structure

```
PA3/
├── src/
│   └── GpaCalculator/
│       ├── Controllers/
│       │   ├── SyllabusController.cs
│       │   ├── ScoresController.cs
│       │   ├── GradeController.cs
│       │   └── SessionController.cs
│       ├── Services/
│       │   ├── IClaudeService.cs
│       │   ├── ClaudeService.cs
│       │   ├── IRagService.cs
│       │   └── RagService.cs
│       ├── Models/
│       │   ├── Db/
│       │   │   ├── SyllabusTemplate.cs
│       │   │   └── GradebookSession.cs
│       │   └── Dto/
│       │       ├── SyllabusParseRequest.cs
│       │       ├── SyllabusParseResponse.cs
│       │       ├── ScoreMapRequest.cs
│       │       ├── ScoreMapResponse.cs
│       │       ├── GradeCalculateRequest.cs
│       │       └── GradeCalculateResponse.cs
│       ├── Data/
│       │   └── AppDbContext.cs
│       ├── Program.cs
│       ├── appsettings.json
│       └── GpaCalculator.csproj
├── frontend/
│   ├── index.html
│   ├── style.css
│   └── app.js
├── db/
│   └── schema.sql
└── _bmad-output/
```

---

## 3. NuGet Packages

| Package | Version | Purpose |
|---------|---------|---------|
| `Anthropic.SDK` | latest | Claude API client |
| `Pomelo.EntityFrameworkCore.MySql` | 8.0.x | MySQL via EF Core |
| `Microsoft.EntityFrameworkCore.Design` | 8.0.x | EF Core tooling |
| `Swashbuckle.AspNetCore` | included | Swagger (built-in for webapi template) |

---

## 4. Environment Variables

```
CLAUDE_API_KEY=sk-ant-...
MYSQL_CONNECTION_STRING=Server=...;Port=3306;Database=gpa_calc;Uid=...;Pwd=...;
FRONTEND_ORIGIN=http://localhost:3000   # for CORS in dev
```

In `appsettings.json` read via `IConfiguration`. Never hardcode.

---

## 5. MySQL Schema

```sql
CREATE TABLE syllabus_templates (
    id INT PRIMARY KEY AUTO_INCREMENT,
    course_name VARCHAR(255) NOT NULL,
    raw_text TEXT NOT NULL,
    parsed_categories JSON NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE gradebook_sessions (
    id INT PRIMARY KEY AUTO_INCREMENT,
    session_id VARCHAR(64) NOT NULL,
    course_name VARCHAR(255) NOT NULL,
    categories JSON NOT NULL,
    scores JSON,
    final_grade VARCHAR(5),
    gpa_points DECIMAL(3,2),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    INDEX idx_session (session_id)
);
```

---

## 6. API Contracts

### POST /api/syllabus/parse
**Request:**
```json
{
  "syllabusText": "string",
  "courseName": "string"
}
```
**Response:**
```json
{
  "courseName": "string",
  "gradingScale": { "A": 90, "B": 80, "C": 70, "D": 60 },
  "categories": [
    { "name": "Exams", "weight": 0.40 },
    { "name": "Homework", "weight": 0.30 },
    { "name": "Participation", "weight": 0.30 }
  ],
  "templateId": 1
}
```

### POST /api/scores/map
**Request:**
```json
{
  "rawScoresText": "string",
  "categories": [{ "name": "string", "weight": 0.0 }]
}
```
**Response:**
```json
{
  "mappedScores": [
    {
      "categoryName": "Exams",
      "earnedPoints": 85,
      "totalPoints": 100
    }
  ]
}
```

### POST /api/grade/calculate
**Request:**
```json
{
  "sessionId": "string",
  "courseName": "string",
  "categories": [
    {
      "name": "string",
      "weight": 0.0,
      "earnedPoints": 0,
      "totalPoints": 0
    }
  ],
  "gradingScale": { "A": 90, "B": 80, "C": 70, "D": 60 }
}
```
**Response:**
```json
{
  "weightedPercentage": 87.5,
  "letterGrade": "B+",
  "gpaPoints": 3.3,
  "breakdown": [
    { "category": "Exams", "contribution": 34.0 }
  ]
}
```

### GET /api/session/{sessionId}
**Response:**
```json
{
  "courses": [
    {
      "courseName": "string",
      "finalGrade": "B+",
      "gpaPoints": 3.3
    }
  ],
  "semesterGpa": 3.45
}
```

---

## 7. Claude API Integration

### 7.1 Syllabus Parsing (Structured Output — no tools)

```
SYSTEM:
You are a syllabus parser. Extract grading information and return ONLY valid JSON.
No markdown, no explanation. Return exactly this structure:
{
  "courseName": "string",
  "gradingScale": { "A": 90, "B": 80, "C": 70, "D": 60 },
  "categories": [{ "name": "string", "weight": 0.XX }]
}
Weights must sum to 1.0.

USER:
[RAG_EXAMPLES_BLOCK if any exist]

Now parse this syllabus:
<syllabus>
{syllabusText}
</syllabus>
```

RAG_EXAMPLES_BLOCK format:
```
Here are examples of previously parsed syllabi for reference:

Example 1 (course_name):
{parsed_categories JSON}

Example 2 (course_name):
{parsed_categories JSON}
```

### 7.2 Score Mapping (Function Calling)

Tool definition:
```json
{
  "name": "map_scores_to_categories",
  "description": "Maps raw gradebook score entries to their grading categories",
  "input_schema": {
    "type": "object",
    "properties": {
      "mappings": {
        "type": "array",
        "items": {
          "type": "object",
          "properties": {
            "categoryName": { "type": "string" },
            "earnedPoints": { "type": "number" },
            "totalPoints": { "type": "number" }
          },
          "required": ["categoryName", "earnedPoints", "totalPoints"]
        }
      }
    },
    "required": ["mappings"]
  }
}
```

Prompt:
```
USER:
Given these grading categories: {categories_json}

Map the following raw scores to their categories:
<scores>
{rawScoresText}
</scores>

Call map_scores_to_categories with your mappings.
```

### 7.3 Grade Calculation (Function Calling — 3 sequential tool calls)

Tools:
```json
[
  {
    "name": "calculate_weighted_grade",
    "description": "Calculates weighted average percentage across all categories",
    "input_schema": {
      "type": "object",
      "properties": {
        "categories": {
          "type": "array",
          "items": {
            "type": "object",
            "properties": {
              "name": { "type": "string" },
              "weight": { "type": "number" },
              "earnedPoints": { "type": "number" },
              "totalPoints": { "type": "number" }
            }
          }
        }
      },
      "required": ["categories"]
    }
  },
  {
    "name": "convert_to_letter_grade",
    "description": "Converts a percentage to a letter grade using the course grading scale",
    "input_schema": {
      "type": "object",
      "properties": {
        "percentage": { "type": "number" },
        "gradingScale": {
          "type": "object",
          "description": "Keys are letter grades, values are minimum percentages"
        }
      },
      "required": ["percentage", "gradingScale"]
    }
  },
  {
    "name": "get_gpa_points",
    "description": "Returns GPA point value (4.0 scale) for a letter grade",
    "input_schema": {
      "type": "object",
      "properties": {
        "letterGrade": { "type": "string" }
      },
      "required": ["letterGrade"]
    }
  }
]
```

Backend implementations (C# logic, called when Claude invokes the tool):

**calculate_weighted_grade:**
```csharp
double weightedSum = 0;
foreach (var cat in categories)
{
    double catPct = cat.TotalPoints > 0 ? (cat.EarnedPoints / cat.TotalPoints) * 100 : 0;
    weightedSum += catPct * cat.Weight;
}
return weightedSum; // percentage 0-100
```

**convert_to_letter_grade:**
```
Sort grading scale descending by min percentage.
Return the first key whose value <= percentage.
Fallback: "F"
```

**get_gpa_points:**
```
A+/A = 4.0, A- = 3.7
B+ = 3.3, B = 3.0, B- = 2.7
C+ = 2.3, C = 2.0, C- = 1.7
D+ = 1.3, D = 1.0, D- = 0.7
F = 0.0
```

### 7.4 Tool Call Loop (C# pattern)

```csharp
// Send initial message
var response = await _claudeClient.Messages.GetClaudeMessageAsync(parameters);

// Loop until no more tool calls
while (response.StopReason == "tool_use")
{
    var toolUseBlock = response.Content.OfType<ToolUseBlock>().First();
    var toolResult = ExecuteTool(toolUseBlock.Name, toolUseBlock.Input);
    
    // Add assistant response + tool result to messages
    messages.Add(new Message(RoleType.Assistant, response.Content));
    messages.Add(new Message(RoleType.User, new ToolResultBlock(toolUseBlock.Id, toolResult)));
    
    parameters.Messages = messages;
    response = await _claudeClient.Messages.GetClaudeMessageAsync(parameters);
}
```

---

## 8. Frontend Architecture

### Single-Page Layout
```
┌─────────────────────────────────────────┐
│  GPA Calculator        Semester GPA: --  │
├─────────────────────────────────────────┤
│  [+ Add Class]                           │
├─────────────────────────────────────────┤
│  ▼ Course Name                [A | 4.0] │
│  ┌───────────────────────────────────┐  │
│  │ Paste syllabus text...            │  │
│  │                    [Parse Syllabus]│  │
│  ├───────────────────────────────────┤  │
│  │ Category    Weight  Score  Points │  │
│  │ Exams       40%     85     /100   │  │
│  │ Homework    30%     92     /100   │  │
│  ├───────────────────────────────────┤  │
│  │ Paste gradebook scores...         │  │
│  │                    [Map Scores]   │  │
│  │                    [Calculate]    │  │
│  └───────────────────────────────────┘  │
└─────────────────────────────────────────┘
```

### app.js State Shape
```javascript
const state = {
  sessionId: crypto.randomUUID(),
  courses: [
    {
      id: "uuid",
      name: "",
      gradingScale: {},
      categories: [],        // { name, weight, earnedPoints, totalPoints }
      finalGrade: null,
      gpaPoints: null
    }
  ],
  semesterGpa: null
}
```

---

## 9. CORS Configuration

In `Program.cs`, allow the frontend origin. In production (Railway), frontend is served from the same origin — CORS only needed for local dev.

```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("DevCors", policy =>
        policy.WithOrigins("http://localhost:5500", "http://127.0.0.1:5500")
              .AllowAnyHeader()
              .AllowAnyMethod());
});
```

---

## 10. Deployment Architecture

### Frontend — GitHub Pages
- Repo: `https://github.com/marceliirzu/PA3`
- Served at: `https://marceliirzu.github.io/PA3/`
- GitHub Actions workflow auto-deploys `/frontend/` on every push to `main`
- `API_BASE` in `app.js` must point to the Railway backend URL

### Backend — Railway
- `Dockerfile` at project root builds only the C# API
- MySQL plugin attached to the same Railway project
- Environment vars set in Railway dashboard: `CLAUDE_API_KEY`, `MYSQL_CONNECTION_STRING`
- Railway URL format: `https://pa3-production-xxxx.up.railway.app`

### CORS
Backend must allow `https://marceliirzu.github.io` in production.
In dev, also allow `http://localhost:5500` and `http://127.0.0.1:5500`.

```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
        policy.WithOrigins(
            "https://marceliirzu.github.io",
            "http://localhost:5500",
            "http://127.0.0.1:5500"
        )
        .AllowAnyHeader()
        .AllowAnyMethod());
});
```

### GitHub Actions Workflow — Pages Deploy
File: `.github/workflows/deploy-pages.yml`
```yaml
name: Deploy Frontend to GitHub Pages
on:
  push:
    branches: [main]
  workflow_dispatch:

permissions:
  contents: read
  pages: write
  id-token: write

concurrency:
  group: "pages"
  cancel-in-progress: false

jobs:
  deploy:
    environment:
      name: github-pages
      url: ${{ steps.deployment.outputs.page_url }}
    runs-on: ubuntu-latest
    steps:
      - name: Checkout
        uses: actions/checkout@v4
      - name: Setup Pages
        uses: actions/configure-pages@v5
      - name: Upload artifact
        uses: actions/upload-pages-artifact@v3
        with:
          path: './frontend'
      - name: Deploy to GitHub Pages
        id: deployment
        uses: actions/deploy-pages@v4
```
