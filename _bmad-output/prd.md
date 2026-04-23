# Product Requirements Document
## AI-Powered GPA Calculator

**Version:** 1.1
**Date:** 2026-04-23
**Course:** MIS 321 — PA3

---

## 1. Problem Statement

College students waste time manually recreating their gradebook structure from syllabi and hand-entering scores into calculators. Existing tools require users to know their exact grade weighting upfront — information buried in a syllabus.

---

## 2. Goal

A single-session web app where a student pastes their syllabi and gradebook data, and AI automatically structures their gradebook and populates scores — giving them a live GPA across all their classes with zero manual configuration.

---

## 3. Required PA3 Features

| Requirement | Implementation |
|-------------|---------------|
| **LLM** | Claude API parses syllabus text and maps gradebook scores |
| **RAG** | Past syllabus structures stored in MySQL; retrieved as context to improve parsing of new syllabi |
| **Function Calling** | Claude uses tools (`calculate_weighted_grade`, `convert_to_letter_grade`, `get_gpa_points`) to perform grade math rather than generating it as text |

---

## 4. Tech Stack

| Layer | Technology |
|-------|-----------|
| Frontend | HTML, CSS, JavaScript (vanilla) |
| Backend | C# — ASP.NET Core Web API |
| Database | MySQL — hosted on Railway |
| AI | Claude API (`claude-sonnet-4-6`) |
| Hosting | Railway (backend + DB), +25pts bonus |

---

## 5. Core Features

### 5.1 Semester Dashboard
- Add / remove classes for the session
- Overall semester GPA displayed at top, recalculated on every change

### 5.2 Syllabus Parser (LLM + RAG)
- User pastes raw syllabus text
- Backend retrieves similar stored syllabus structures from MySQL (RAG) and passes them as context to Claude
- Claude extracts: course name, grading categories, weights, grading scale
- Result is stored in MySQL and displayed as an editable gradebook template

### 5.3 Score Parser (LLM + Function Calling)
- User pastes raw gradebook text (copied from Canvas/Blackboard)
- Claude maps scores to categories via tool call: `map_scores_to_categories(scores[], categories[])`
- Scores populate the gradebook; user can edit

### 5.4 Grade Calculation (Function Calling)
Claude tools called by the backend:
- `calculate_weighted_grade(category_scores[], weights[])` → weighted average per class
- `convert_to_letter_grade(percentage, grade_scale)` → letter grade
- `get_gpa_points(letter_grade)` → GPA point value (4.0 scale)
- `calculate_semester_gpa(classes[])` → final semester GPA

### 5.5 MySQL Schema

```sql
-- Stores parsed syllabus structures for RAG retrieval
CREATE TABLE syllabus_templates (
  id INT PRIMARY KEY AUTO_INCREMENT,
  course_name VARCHAR(255),
  raw_text TEXT,
  parsed_categories JSON,
  created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Stores current session gradebook state
CREATE TABLE gradebook_sessions (
  id INT PRIMARY KEY AUTO_INCREMENT,
  session_id VARCHAR(64),
  course_name VARCHAR(255),
  categories JSON,
  scores JSON,
  final_grade VARCHAR(5),
  gpa_points DECIMAL(3,2),
  created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);
```

---

## 6. RAG Flow Detail

1. User pastes syllabus
2. Backend queries MySQL: `SELECT parsed_categories FROM syllabus_templates WHERE course_name SOUNDS LIKE ? LIMIT 3`
3. Retrieved templates are injected into Claude's prompt as few-shot examples
4. Claude parses the new syllabus with that context
5. Result is saved back to MySQL for future retrieval

---

## 7. User Flow

```
Add Class
  → Paste syllabus
    → RAG: retrieve similar syllabi from DB
    → Claude parses with context → returns categories/weights
  → Review/edit structure
  → Paste gradebook scores
    → Claude tool call: map_scores_to_categories()
  → Claude tool calls: calculate_weighted_grade() → convert_to_letter_grade() → get_gpa_points()
  → Class grade displayed

Repeat per class → semester GPA calculated
```

---

## 8. API Endpoints (C# Backend)

| Method | Route | Description |
|--------|-------|-------------|
| POST | `/api/syllabus/parse` | RAG lookup + Claude parse |
| POST | `/api/scores/map` | Claude maps scores to categories |
| POST | `/api/grade/calculate` | Claude function calls for grade math |
| GET | `/api/session/{id}` | Retrieve session state from MySQL |

---

## 9. Out of Scope

- User authentication
- File upload (paste only)
- Mobile optimization

---

## 10. Hosting

- Backend + MySQL on Railway
- Frontend served as static files from the same Railway deployment
- +25 bonus points if fully hosted and accessible via public URL
