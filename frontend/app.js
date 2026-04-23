const API_BASE = 'https://pa3-production.up.railway.app';

const state = {
  sessionId: crypto.randomUUID(),
  courses: [],
  semesterGpa: null
};

function gradeBadgeClass(grade) {
  if (!grade) return '';
  const g = grade[0].toUpperCase();
  if (g === 'A') return 'grade-a';
  if (g === 'B') return 'grade-b';
  if (g === 'C') return 'grade-c';
  if (g === 'D') return 'grade-d';
  return 'grade-f';
}

function renderCourses() {
  const container = document.getElementById('courses-container');
  if (state.courses.length === 0) {
    container.innerHTML = '<div class="empty-state">No courses yet. Click "+ Add Class" to get started.</div>';
    return;
  }

  container.innerHTML = state.courses.map(course => {
    const gradeLabel = course.finalGrade
      ? `${course.finalGrade} | ${course.gpaPoints !== null ? course.gpaPoints.toFixed(1) : '--'}`
      : '--';
    const badgeClass = gradeBadgeClass(course.finalGrade);

    const categoriesHtml = course.categories.length > 0
      ? `<table class="categories-table">
          <thead><tr><th>Category</th><th>Weight</th><th>Earned</th><th>Total</th></tr></thead>
          <tbody>
            ${course.categories.map((cat, ci) => `
              <tr>
                <td>${escHtml(cat.name)}</td>
                <td>${(cat.weight * 100).toFixed(0)}%</td>
                <td><input class="score-input" type="number" min="0" value="${cat.earnedPoints || ''}"
                    placeholder="0"
                    oninput="updateCategoryScore('${course.id}', ${ci}, 'earnedPoints', this.value)" /></td>
                <td><input class="score-input" type="number" min="0" value="${cat.totalPoints || ''}"
                    placeholder="100"
                    oninput="updateCategoryScore('${course.id}', ${ci}, 'totalPoints', this.value)" /></td>
              </tr>`).join('')}
          </tbody>
        </table>`
      : '<div class="empty-state">Parse a syllabus to see categories here.</div>';

    return `
      <div class="course-card" id="card-${course.id}">
        <div class="course-card-header">
          <input class="course-name-input" type="text" placeholder="Course Name (e.g. MIS 321)"
            value="${escHtml(course.name)}"
            oninput="updateCourseName('${course.id}', this.value)" />
          <span class="grade-badge ${badgeClass}">${gradeLabel}</span>
          <button class="remove-btn" onclick="removeCourse('${course.id}')" title="Remove course">&times;</button>
        </div>
        <div class="course-card-body">
          <div>
            <div class="section-label">Syllabus</div>
            <textarea class="syllabus-area" id="syllabus-${course.id}"
              placeholder="Paste your course syllabus text here...">${escHtml(course.syllabusText || '')}</textarea>
            <div class="action-row" style="margin-top:0.5rem">
              <button class="btn btn-secondary" id="parse-btn-${course.id}" onclick="parseSyllabus('${course.id}')">
                Parse Syllabus
              </button>
            </div>
            ${course.parseError ? `<div class="error-msg">${escHtml(course.parseError)}</div>` : ''}
          </div>

          <div>
            <div class="section-label">Categories &amp; Scores</div>
            ${categoriesHtml}
          </div>

          <div>
            <div class="section-label">Gradebook Scores</div>
            <textarea class="scores-area" id="scores-${course.id}"
              placeholder="Paste raw score data (e.g. Exam 1: 88/100, HW1: 45/50)...">${escHtml(course.scoresText || '')}</textarea>
            <div class="action-row" style="margin-top:0.5rem">
              <button class="btn btn-secondary" id="map-btn-${course.id}" onclick="mapScores('${course.id}')">
                Map Scores
              </button>
              <button class="btn btn-success" id="calc-btn-${course.id}" onclick="calculateGrade('${course.id}')">
                Calculate Grade
              </button>
            </div>
            ${course.scoresError ? `<div class="error-msg">${escHtml(course.scoresError)}</div>` : ''}
            ${course.calcError ? `<div class="error-msg">${escHtml(course.calcError)}</div>` : ''}
          </div>
        </div>
      </div>`;
  }).join('');
}

function escHtml(str) {
  if (!str) return '';
  return String(str)
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');
}

function addCourse() {
  state.courses.push({
    id: crypto.randomUUID(),
    name: '',
    syllabusText: '',
    scoresText: '',
    gradingScale: {},
    categories: [],
    finalGrade: null,
    gpaPoints: null,
    parseError: null,
    scoresError: null,
    calcError: null
  });
  renderCourses();
}

function removeCourse(id) {
  state.courses = state.courses.filter(c => c.id !== id);
  calculateSemesterGpa();
  renderCourses();
}

function updateCourseName(id, value) {
  const course = state.courses.find(c => c.id === id);
  if (course) course.name = value;
}

function updateCategoryScore(courseId, catIndex, field, value) {
  const course = state.courses.find(c => c.id === courseId);
  if (course && course.categories[catIndex]) {
    course.categories[catIndex][field] = parseFloat(value) || 0;
  }
}

function setLoading(btnId, loading) {
  const btn = document.getElementById(btnId);
  if (!btn) return;
  btn.disabled = loading;
  if (loading) {
    btn.dataset.originalText = btn.textContent;
    btn.innerHTML = '<span class="spinner"></span> Loading...';
  } else {
    btn.innerHTML = btn.dataset.originalText || btn.textContent;
  }
}

async function parseSyllabus(courseId) {
  const course = state.courses.find(c => c.id === courseId);
  if (!course) return;

  const syllabusEl = document.getElementById(`syllabus-${courseId}`);
  const syllabusText = syllabusEl ? syllabusEl.value : '';
  course.syllabusText = syllabusText;
  course.parseError = null;

  if (!syllabusText.trim()) {
    course.parseError = 'Please paste a syllabus first.';
    renderCourses();
    return;
  }

  setLoading(`parse-btn-${courseId}`, true);
  try {
    const data = await apiCall('/api/syllabus/parse', {
      syllabusText,
      courseName: course.name || 'Unknown Course'
    });
    course.gradingScale = data.gradingScale || {};
    course.categories = (data.categories || []).map(c => ({
      name: c.name,
      weight: c.weight,
      earnedPoints: 0,
      totalPoints: 100
    }));
    if (data.courseName && !course.name) course.name = data.courseName;
  } catch (err) {
    course.parseError = err.message;
  } finally {
    setLoading(`parse-btn-${courseId}`, false);
    renderCourses();
  }
}

async function mapScores(courseId) {
  const course = state.courses.find(c => c.id === courseId);
  if (!course) return;

  const scoresEl = document.getElementById(`scores-${courseId}`);
  const scoresText = scoresEl ? scoresEl.value : '';
  course.scoresText = scoresText;
  course.scoresError = null;

  if (!scoresText.trim()) {
    course.scoresError = 'Please paste score data first.';
    renderCourses();
    return;
  }
  if (course.categories.length === 0) {
    course.scoresError = 'Parse a syllabus first to get categories.';
    renderCourses();
    return;
  }

  setLoading(`map-btn-${courseId}`, true);
  try {
    const data = await apiCall('/api/scores/map', {
      rawScoresText: scoresText,
      categories: course.categories
    });
    if (data.mappedScores) {
      course.categories = course.categories.map(cat => {
        const norm = s => s.toLowerCase().trim();
        // Exact match first, then case-insensitive, then partial
        let mapped = data.mappedScores.find(m => m.name === cat.name)
          || data.mappedScores.find(m => norm(m.name) === norm(cat.name))
          || data.mappedScores.find(m => norm(m.name).includes(norm(cat.name)) || norm(cat.name).includes(norm(m.name)));
        if (mapped && (mapped.earnedPoints > 0 || mapped.totalPoints > 0)) {
          return { ...cat, earnedPoints: mapped.earnedPoints, totalPoints: mapped.totalPoints };
        }
        return cat;
      });
    }
  } catch (err) {
    course.scoresError = err.message;
  } finally {
    setLoading(`map-btn-${courseId}`, false);
    renderCourses();
  }
}

async function calculateGrade(courseId) {
  const course = state.courses.find(c => c.id === courseId);
  if (!course) return;

  course.calcError = null;

  if (course.categories.length === 0) {
    course.calcError = 'Parse a syllabus first to get categories.';
    renderCourses();
    return;
  }

  // Sync any unsaved input values from DOM before submitting
  course.categories.forEach((cat, ci) => {
    const card = document.getElementById(`card-${courseId}`);
    if (!card) return;
    const rows = card.querySelectorAll('.categories-table tbody tr');
    if (rows[ci]) {
      const inputs = rows[ci].querySelectorAll('input');
      if (inputs[0]) cat.earnedPoints = parseFloat(inputs[0].value) || 0;
      if (inputs[1]) cat.totalPoints = parseFloat(inputs[1].value) || 100;
    }
  });

  setLoading(`calc-btn-${courseId}`, true);
  try {
    const data = await apiCall('/api/grade/calculate', {
      sessionId: state.sessionId,
      courseName: course.name || 'Unknown Course',
      categories: course.categories,
      gradingScale: course.gradingScale
    });
    course.finalGrade = data.letterGrade;
    course.gpaPoints = data.gpaPoints;
    calculateSemesterGpa();
  } catch (err) {
    course.calcError = err.message;
  } finally {
    setLoading(`calc-btn-${courseId}`, false);
    renderCourses();
  }
}

function calculateSemesterGpa() {
  const coursesWithGpa = state.courses.filter(c => c.gpaPoints !== null && c.gpaPoints !== undefined);
  if (coursesWithGpa.length === 0) {
    state.semesterGpa = null;
    document.getElementById('semester-gpa').textContent = '--';
    return;
  }
  const avg = coursesWithGpa.reduce((sum, c) => sum + c.gpaPoints, 0) / coursesWithGpa.length;
  state.semesterGpa = avg;
  document.getElementById('semester-gpa').textContent = avg.toFixed(2);
}

async function apiCall(path, body) {
  const res = await fetch(`${API_BASE}${path}`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body)
  });
  if (!res.ok) {
    const text = await res.text();
    throw new Error(`API error ${res.status}: ${text}`);
  }
  return res.json();
}

// Initialize
renderCourses();
