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

function escHtml(str) {
  if (!str) return '';
  return String(str)
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');
}

// ── Rendering ────────────────────────────────────────────────────────────────

function renderCourses() {
  const container = document.getElementById('courses-container');
  if (state.courses.length === 0) {
    container.innerHTML = '<div class="empty-state">No courses yet. Click "+ Add Class" to get started.</div>';
    return;
  }
  container.innerHTML = state.courses.map(renderCourse).join('');
}

function renderCourse(course) {
  if (course.finalGrade !== null) return renderDoneCard(course);
  if (course.step === 1) return renderStep1(course);
  if (course.step === 2) return renderStep2(course);
  return '';
}

function renderDoneCard(course) {
  const badgeClass = gradeBadgeClass(course.finalGrade);
  return `
    <div class="course-card done-card" id="card-${course.id}">
      <div class="course-card-header">
        <span class="course-name-display">${escHtml(course.name || 'Unnamed Course')}</span>
        <span class="grade-badge ${badgeClass}">
          ${escHtml(course.finalGrade)} &nbsp;|&nbsp; ${course.gpaPoints !== null ? Number(course.gpaPoints).toFixed(1) : '--'} GPA pts
        </span>
        <button class="btn btn-sm btn-secondary" onclick="editCourse('${course.id}')">Edit</button>
        <button class="remove-btn" onclick="removeCourse('${course.id}')" title="Remove">&times;</button>
      </div>
    </div>`;
}

function renderStep1(course) {
  const hasParsed = course.categories.length > 0;

  const reviewHtml = hasParsed ? `
    <div class="parsed-result">
      <div class="section-label">Parsed Categories — Confirm before continuing</div>
      <table class="categories-table">
        <thead><tr><th>Category</th><th>Weight</th></tr></thead>
        <tbody>
          ${course.categories.map(cat => `
            <tr>
              <td>${escHtml(cat.name)}</td>
              <td>${(cat.weight * 100).toFixed(0)}%</td>
            </tr>`).join('')}
        </tbody>
      </table>
      <div class="action-row" style="margin-top:1rem">
        <button class="btn btn-secondary" onclick="resetParse('${course.id}')">Re-parse</button>
        <button class="btn btn-primary" onclick="confirmCategories('${course.id}')">
          Looks Good — Enter Grades &rarr;
        </button>
      </div>
    </div>` : '';

  return `
    <div class="course-card" id="card-${course.id}">
      <div class="course-card-header">
        <input class="course-name-input" type="text" placeholder="Course Name (e.g. MIS 321)"
          value="${escHtml(course.name)}"
          oninput="updateCourseName('${course.id}', this.value)" />
        <span class="step-badge">Step 1 of 2</span>
        <button class="remove-btn" onclick="removeCourse('${course.id}')" title="Remove">&times;</button>
      </div>
      <div class="course-card-body">
        <div>
          <div class="section-label">Paste your syllabus</div>
          <textarea class="syllabus-area" id="syllabus-${course.id}"
            placeholder="Paste your full course syllabus here...">${escHtml(course.syllabusText || '')}</textarea>
          <div class="action-row" style="margin-top:0.5rem">
            <button class="btn btn-secondary" id="parse-btn-${course.id}"
              onclick="parseSyllabus('${course.id}')">Parse Syllabus</button>
          </div>
          ${course.parseError ? `<div class="error-msg">${escHtml(course.parseError)}</div>` : ''}
        </div>
        ${reviewHtml}
      </div>
    </div>`;
}

function renderStep2(course) {
  return `
    <div class="course-card" id="card-${course.id}">
      <div class="course-card-header">
        <span class="course-name-display">${escHtml(course.name || 'Unnamed Course')}</span>
        <span class="step-badge">Step 2 of 2</span>
        <button class="remove-btn" onclick="removeCourse('${course.id}')" title="Remove">&times;</button>
      </div>
      <div class="course-card-body">

        <div>
          <div class="section-label">Review &amp; Enter Scores</div>
          <table class="categories-table">
            <thead><tr><th>Category</th><th>Weight</th><th>Earned</th><th>Total</th></tr></thead>
            <tbody>
              ${course.categories.map((cat, ci) => `
                <tr>
                  <td>${escHtml(cat.name)}</td>
                  <td>${(cat.weight * 100).toFixed(0)}%</td>
                  <td><input class="score-input" type="number" min="0"
                      value="${cat.earnedPoints > 0 ? cat.earnedPoints : ''}"
                      placeholder="0"
                      oninput="updateCategoryScore('${course.id}', ${ci}, 'earnedPoints', this.value)" /></td>
                  <td><input class="score-input" type="number" min="0"
                      value="${cat.totalPoints || 100}"
                      placeholder="100"
                      oninput="updateCategoryScore('${course.id}', ${ci}, 'totalPoints', this.value)" /></td>
                </tr>`).join('')}
            </tbody>
          </table>
        </div>

        <div>
          <div class="section-label">Or paste raw gradebook scores (optional — AI will map them)</div>
          <textarea class="scores-area" id="scores-${course.id}"
            placeholder="e.g. Exam 1: 88/100, HW1: 45/50, Quiz 2: 18/20...">${escHtml(course.scoresText || '')}</textarea>
        </div>

        <div class="action-row" style="margin-top:0.5rem">
          <button class="btn btn-secondary" onclick="goBackToStep1('${course.id}')">
            &larr; Back
          </button>
          <button class="btn btn-success" id="calc-btn-${course.id}"
            onclick="mapAndCalculate('${course.id}')">
            Map &amp; Calculate Grade
          </button>
        </div>
        ${course.calcError ? `<div class="error-msg">${escHtml(course.calcError)}</div>` : ''}

      </div>
    </div>`;
}

// ── State mutations ───────────────────────────────────────────────────────────

function addCourse() {
  state.courses.push({
    id: crypto.randomUUID(),
    name: '',
    step: 1,
    syllabusText: '',
    scoresText: '',
    gradingScale: {},
    categories: [],
    finalGrade: null,
    gpaPoints: null,
    parseError: null,
    calcError: null
  });
  renderCourses();
}

function removeCourse(id) {
  state.courses = state.courses.filter(c => c.id !== id);
  calculateSemesterGpa();
  renderCourses();
}

function editCourse(id) {
  const course = state.courses.find(c => c.id === id);
  if (!course) return;
  course.finalGrade = null;
  course.gpaPoints = null;
  course.calcError = null;
  course.step = 2;
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

function confirmCategories(courseId) {
  const course = state.courses.find(c => c.id === courseId);
  if (course) { course.step = 2; renderCourses(); }
}

function resetParse(courseId) {
  const course = state.courses.find(c => c.id === courseId);
  if (!course) return;
  course.categories = [];
  course.parseError = null;
  renderCourses();
}

function goBackToStep1(courseId) {
  const course = state.courses.find(c => c.id === courseId);
  if (!course) return;
  course.step = 1;
  course.calcError = null;
  renderCourses();
}

// ── Loading helper ────────────────────────────────────────────────────────────

function setLoading(btnId, loading, label) {
  const btn = document.getElementById(btnId);
  if (!btn) return;
  btn.disabled = loading;
  if (loading) {
    btn.dataset.originalText = btn.innerHTML;
    btn.innerHTML = '<span class="spinner"></span> ' + (label || 'Loading...');
  } else {
    btn.innerHTML = btn.dataset.originalText || btn.innerHTML;
  }
}

// ── Step 1: Parse syllabus ────────────────────────────────────────────────────

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
  setLoading(`parse-btn-${courseId}`, true, 'Parsing...');
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

// ── Step 2: Map scores + calculate ───────────────────────────────────────────

function syncScoreInputs(courseId) {
  const course = state.courses.find(c => c.id === courseId);
  if (!course) return;
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
}

async function mapAndCalculate(courseId) {
  const course = state.courses.find(c => c.id === courseId);
  if (!course) return;
  course.calcError = null;
  syncScoreInputs(courseId);

  const scoresEl = document.getElementById(`scores-${courseId}`);
  const scoresText = scoresEl ? scoresEl.value.trim() : '';
  course.scoresText = scoresText;

  setLoading(`calc-btn-${courseId}`, true, 'Mapping scores...');

  if (scoresText) {
    try {
      const mapData = await apiCall('/api/scores/map', {
        rawScoresText: scoresText,
        categories: course.categories
      });
      if (mapData.mappedScores) {
        course.categories = course.categories.map(cat => {
          const norm = s => s.toLowerCase().trim();
          const mapped = mapData.mappedScores.find(m => m.name === cat.name)
            || mapData.mappedScores.find(m => norm(m.name) === norm(cat.name))
            || mapData.mappedScores.find(m =>
                norm(m.name).includes(norm(cat.name)) || norm(cat.name).includes(norm(m.name)));
          if (mapped && (mapped.earnedPoints > 0 || mapped.totalPoints > 0)) {
            return { ...cat, earnedPoints: mapped.earnedPoints, totalPoints: mapped.totalPoints };
          }
          return cat;
        });
      }
    } catch (err) {
      course.calcError = 'Score mapping failed: ' + err.message;
      setLoading(`calc-btn-${courseId}`, false);
      renderCourses();
      return;
    }
    // Re-render so user sees updated values before modal check
    renderCourses();
  }

  setLoading(`calc-btn-${courseId}`, false);

  // Check for missing/zero earned scores
  const missing = course.categories.filter(c => !c.earnedPoints || c.earnedPoints === 0);
  if (missing.length > 0) {
    showConfirmModal(courseId, missing);
  } else {
    await doCalculateGrade(courseId);
  }
}

// ── Confirmation modal ────────────────────────────────────────────────────────

function showConfirmModal(courseId, missing) {
  const body = document.getElementById('modal-body');
  const list = missing.map(c => `<li>${escHtml(c.name)}</li>`).join('');
  body.innerHTML = `
    <h3 class="modal-title">Some scores are missing</h3>
    <p>${missing.length} categor${missing.length === 1 ? 'y has' : 'ies have'} no earned score entered:</p>
    <ul class="missing-list">${list}</ul>
    <p class="modal-hint">How would you like to proceed?</p>
    <div class="modal-actions">
      <button class="btn btn-secondary" onclick="closeModal()">Cancel — Keep Editing</button>
      <button class="btn btn-secondary" onclick="closeModal(); doCalculateGrade('${courseId}')">
        Calculate As-Is
      </button>
      <button class="btn btn-primary" onclick="showWhatIfEditor('${courseId}')">
        What-If Mode
      </button>
    </div>`;
  document.getElementById('confirm-modal').classList.add('open');
}

function showWhatIfEditor(courseId) {
  const course = state.courses.find(c => c.id === courseId);
  if (!course) return;
  const body = document.getElementById('modal-body');
  body.innerHTML = `
    <h3 class="modal-title">What-If Mode</h3>
    <p class="modal-hint">Enter projected scores for any missing categories:</p>
    <table class="categories-table" style="margin-top:0.75rem">
      <thead><tr><th>Category</th><th>Weight</th><th>Earned</th><th>Total</th></tr></thead>
      <tbody>
        ${course.categories.map((cat, ci) => `
          <tr>
            <td>${escHtml(cat.name)}</td>
            <td>${(cat.weight * 100).toFixed(0)}%</td>
            <td><input class="score-input" type="number" min="0"
                value="${cat.earnedPoints > 0 ? cat.earnedPoints : ''}"
                placeholder="0" id="wi-earned-${ci}" /></td>
            <td><input class="score-input" type="number" min="0"
                value="${cat.totalPoints || 100}"
                placeholder="100" id="wi-total-${ci}" /></td>
          </tr>`).join('')}
      </tbody>
    </table>
    <div class="modal-actions" style="margin-top:1.25rem">
      <button class="btn btn-secondary" onclick="closeModal()">Cancel</button>
      <button class="btn btn-primary"
        onclick="applyWhatIfAndCalculate('${courseId}', ${course.categories.length})">
        Calculate Grade
      </button>
    </div>`;
}

async function applyWhatIfAndCalculate(courseId, catCount) {
  const course = state.courses.find(c => c.id === courseId);
  if (!course) return;
  for (let i = 0; i < catCount; i++) {
    const e = document.getElementById(`wi-earned-${i}`);
    const t = document.getElementById(`wi-total-${i}`);
    if (e) course.categories[i].earnedPoints = parseFloat(e.value) || 0;
    if (t) course.categories[i].totalPoints = parseFloat(t.value) || 100;
  }
  closeModal();
  await doCalculateGrade(courseId);
}

function closeModal() {
  document.getElementById('confirm-modal').classList.remove('open');
}

// Close modal on backdrop click
document.getElementById('confirm-modal').addEventListener('click', function(e) {
  if (e.target === this) closeModal();
});

// ── Grade calculation ─────────────────────────────────────────────────────────

async function doCalculateGrade(courseId) {
  const course = state.courses.find(c => c.id === courseId);
  if (!course) return;
  course.calcError = null;

  const btn = document.getElementById(`calc-btn-${courseId}`);
  if (btn) { btn.disabled = true; btn.innerHTML = '<span class="spinner"></span> Calculating...'; }

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
    if (btn) { btn.disabled = false; btn.innerHTML = 'Map &amp; Calculate Grade'; }
  }
  renderCourses();
}

// ── Semester GPA ──────────────────────────────────────────────────────────────

function calculateSemesterGpa() {
  const graded = state.courses.filter(c => c.gpaPoints !== null && c.gpaPoints !== undefined);
  if (graded.length === 0) {
    state.semesterGpa = null;
    document.getElementById('semester-gpa').textContent = '--';
    return;
  }
  const avg = graded.reduce((s, c) => s + c.gpaPoints, 0) / graded.length;
  state.semesterGpa = avg;
  document.getElementById('semester-gpa').textContent = avg.toFixed(2);
}

// ── API helper ────────────────────────────────────────────────────────────────

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

// ── Init ──────────────────────────────────────────────────────────────────────
renderCourses();
