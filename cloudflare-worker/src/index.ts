/**
 * PromptBox Community Ratings API
 * Cloudflare Worker with D1 Database
 */

export interface Env {
  DB: D1Database;
}

interface RatingRequest {
  templateId: string;
  userHash: string;
  rating: number;
  comment?: string;
}

interface RatingResponse {
  templateId: string;
  averageRating: number;
  ratingCount: number;
  userRating?: number;
}

// CORS headers for cross-origin requests
const corsHeaders = {
  'Access-Control-Allow-Origin': '*',
  'Access-Control-Allow-Methods': 'GET, POST, OPTIONS',
  'Access-Control-Allow-Headers': 'Content-Type',
  'Content-Type': 'application/json',
};

export default {
  async fetch(request: Request, env: Env): Promise<Response> {
    // Handle CORS preflight
    if (request.method === 'OPTIONS') {
      return new Response(null, { headers: corsHeaders });
    }

    const url = new URL(request.url);
    const path = url.pathname;

    try {
      // GET /api/ratings - Get all aggregate ratings
      if (path === '/api/ratings' && request.method === 'GET') {
        return await getAllRatings(env);
      }

      // GET /api/ratings/:templateId - Get ratings for a specific template
      if (path.startsWith('/api/ratings/') && request.method === 'GET') {
        const templateId = path.split('/')[3];
        const userHash = url.searchParams.get('userHash');
        return await getTemplateRatings(env, templateId, userHash);
      }

      // POST /api/ratings - Submit a rating
      if (path === '/api/ratings' && request.method === 'POST') {
        const body: RatingRequest = await request.json();
        return await submitRating(env, body);
      }

      // GET /api/downloads - Get all download counts
      if (path === '/api/downloads' && request.method === 'GET') {
        return await getAllDownloads(env);
      }

      // POST /api/downloads/:templateId - Increment download count
      if (path.startsWith('/api/downloads/') && request.method === 'POST') {
        const templateId = path.split('/')[3];
        return await incrementDownload(env, templateId);
      }

      // GET /api/health - Health check
      if (path === '/api/health') {
        return new Response(JSON.stringify({ status: 'ok', timestamp: new Date().toISOString() }), {
          headers: corsHeaders,
        });
      }

      return new Response(JSON.stringify({ error: 'Not found' }), {
        status: 404,
        headers: corsHeaders,
      });
    } catch (error) {
      console.error('Error:', error);
      return new Response(JSON.stringify({ error: 'Internal server error' }), {
        status: 500,
        headers: corsHeaders,
      });
    }
  },
};


// Get all aggregate ratings
async function getAllRatings(env: Env): Promise<Response> {
  const results = await env.DB.prepare(`
    SELECT 
      ra.template_id as templateId,
      ra.average_rating as averageRating,
      ra.rating_count as ratingCount,
      COALESCE(d.download_count, 0) as downloadCount
    FROM rating_aggregates ra
    LEFT JOIN downloads d ON ra.template_id = d.template_id
  `).all();

  return new Response(JSON.stringify(results.results || []), {
    headers: corsHeaders,
  });
}

// Get ratings for a specific template
async function getTemplateRatings(env: Env, templateId: string, userHash: string | null): Promise<Response> {
  // Get aggregate
  const aggregate = await env.DB.prepare(`
    SELECT 
      template_id as templateId,
      average_rating as averageRating,
      rating_count as ratingCount
    FROM rating_aggregates
    WHERE template_id = ?
  `).bind(templateId).first();

  // Get user's rating if userHash provided
  let userRating = null;
  if (userHash) {
    const userRatingResult = await env.DB.prepare(`
      SELECT rating, comment FROM ratings
      WHERE template_id = ? AND user_hash = ?
    `).bind(templateId, userHash).first();
    userRating = userRatingResult;
  }

  // Get recent ratings with comments
  const recentRatings = await env.DB.prepare(`
    SELECT rating, comment, created_at as createdAt
    FROM ratings
    WHERE template_id = ? AND comment IS NOT NULL AND comment != ''
    ORDER BY created_at DESC
    LIMIT 20
  `).bind(templateId).all();

  return new Response(JSON.stringify({
    templateId,
    averageRating: aggregate?.averageRating || 0,
    ratingCount: aggregate?.ratingCount || 0,
    userRating: userRating?.rating || null,
    userComment: userRating?.comment || null,
    recentRatings: recentRatings.results || [],
  }), {
    headers: corsHeaders,
  });
}

// Submit or update a rating
async function submitRating(env: Env, body: RatingRequest): Promise<Response> {
  const { templateId, userHash, rating, comment } = body;

  // Validate
  if (!templateId || !userHash || !rating) {
    return new Response(JSON.stringify({ error: 'Missing required fields' }), {
      status: 400,
      headers: corsHeaders,
    });
  }

  if (rating < 1 || rating > 5) {
    return new Response(JSON.stringify({ error: 'Rating must be between 1 and 5' }), {
      status: 400,
      headers: corsHeaders,
    });
  }

  // Upsert rating
  await env.DB.prepare(`
    INSERT INTO ratings (template_id, user_hash, rating, comment, updated_at)
    VALUES (?, ?, ?, ?, CURRENT_TIMESTAMP)
    ON CONFLICT(template_id, user_hash) 
    DO UPDATE SET rating = ?, comment = ?, updated_at = CURRENT_TIMESTAMP
  `).bind(templateId, userHash, rating, comment || null, rating, comment || null).run();

  // Update aggregate
  const aggregateResult = await env.DB.prepare(`
    SELECT AVG(rating) as avg, COUNT(*) as count
    FROM ratings
    WHERE template_id = ?
  `).bind(templateId).first();

  await env.DB.prepare(`
    INSERT INTO rating_aggregates (template_id, average_rating, rating_count, updated_at)
    VALUES (?, ?, ?, CURRENT_TIMESTAMP)
    ON CONFLICT(template_id)
    DO UPDATE SET average_rating = ?, rating_count = ?, updated_at = CURRENT_TIMESTAMP
  `).bind(
    templateId,
    aggregateResult?.avg || 0,
    aggregateResult?.count || 0,
    aggregateResult?.avg || 0,
    aggregateResult?.count || 0
  ).run();

  return new Response(JSON.stringify({
    success: true,
    averageRating: aggregateResult?.avg || 0,
    ratingCount: aggregateResult?.count || 0,
  }), {
    headers: corsHeaders,
  });
}

// Get all download counts
async function getAllDownloads(env: Env): Promise<Response> {
  const results = await env.DB.prepare(`
    SELECT template_id as templateId, download_count as downloadCount
    FROM downloads
  `).all();

  return new Response(JSON.stringify(results.results || []), {
    headers: corsHeaders,
  });
}

// Increment download count
async function incrementDownload(env: Env, templateId: string): Promise<Response> {
  await env.DB.prepare(`
    INSERT INTO downloads (template_id, download_count)
    VALUES (?, 1)
    ON CONFLICT(template_id)
    DO UPDATE SET download_count = download_count + 1
  `).bind(templateId).run();

  const result = await env.DB.prepare(`
    SELECT download_count FROM downloads WHERE template_id = ?
  `).bind(templateId).first();

  return new Response(JSON.stringify({
    success: true,
    downloadCount: result?.download_count || 1,
  }), {
    headers: corsHeaders,
  });
}
