CREATE TABLE IF NOT EXISTS syllabus_templates (
    id INT PRIMARY KEY AUTO_INCREMENT,
    course_name VARCHAR(255) NOT NULL,
    raw_text TEXT NOT NULL,
    parsed_categories JSON NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS gradebook_sessions (
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
