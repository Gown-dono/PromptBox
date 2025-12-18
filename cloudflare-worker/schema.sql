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
