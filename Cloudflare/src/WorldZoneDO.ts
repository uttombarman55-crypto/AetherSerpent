import { DurableObject } from 'cloudflare:workers';

interface Env {
  GAME_ROOM: DurableObjectNamespace;
  WORLD_ZONE: DurableObjectNamespace;
}

export class WorldZoneDO extends DurableObject {
  constructor(ctx: DurableObjectState, env: Env) {
    super(ctx, env);

    // Initialize SQLite storage for persistent World Zone
    this.ctx.storage.sql.exec(`
      CREATE TABLE IF NOT EXISTS zone_state (
        zone_id TEXT PRIMARY KEY,
        name TEXT,
        dominant_biome TEXT,
        aether_density REAL,
        last_updated INTEGER
      );

      CREATE TABLE IF NOT EXISTS global_shrines (
        id TEXT PRIMARY KEY,
        zone_id TEXT,
        pos_x REAL,
        pos_y REAL,
        pos_z REAL,
        is_active INTEGER,
        attuned_evolution TEXT
      );
    `);
  }

  async fetch(request: Request): Promise<Response> {
    const url = new URL(request.url);

    if (url.pathname.endsWith('/status')) {
      const cursor = this.ctx.storage.sql.exec('SELECT * FROM zone_state LIMIT 1');
      const rows = cursor.toArray();
      return Response.json({ success: true, zone: rows[0] || null });
    }

    return new Response('WorldZoneDO Active', { status: 200 });
  }
}

