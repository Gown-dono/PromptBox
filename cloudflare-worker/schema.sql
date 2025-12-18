-- Ratings table
CREATE TABLE IF NOT EXISTS ratings (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    template_id TEXT NOT NULL,
    user_hash TEXT NOT NULL,
    rating INTEGER NOT NULL CHECK (rating >= 1 AND rating <= 5),
    comment TEXT,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(template_id, user_hash)
);

-- Index for fast lookups
CREATE INDEX IF NOT EXISTS idx_ratings_template_id ON ratings(template_id);
CREATE INDEX IF NOT EXISTS idx_ratings_user_hash ON ratings(user_hash);

-- Aggregate ratings view (materialized as table for performance)
CREATE TABLE IF NOT EXISTS rating_aggregates (
    template_id TEXT PRIMARY KEY,
    average_rating REAL DEFAULT 0,
    rating_count INTEGER DEFAULT 0,
    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP
);

-- Download counts
CREATE TABLE IF NOT EXISTS downloads (
    template_id TEXT PRIMARY KEY,
    download_count INTEGER DEFAULT 0
);

-- Template submissions
CREATE TABLE IF NOT EXISTS submissions (
    id TEXT PRIMARY KEY,
    title TEXT NOT NULL,
    category TEXT NOT NULL,
    description TEXT NOT NULL,
    content TEXT NOT NULL,
    tags TEXT DEFAULT '[]',
    author TEXT NOT NULL,
    license_type TEXT DEFAULT 'MIT',
    status TEXT DEFAULT 'pending' CHECK (status IN ('pending', 'approved', 'rejected')),
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    approved_at DATETIME
);

-- Index for fast status lookups
CREATE INDEX IF NOT EXISTS idx_submissions_status ON submissions(status);
