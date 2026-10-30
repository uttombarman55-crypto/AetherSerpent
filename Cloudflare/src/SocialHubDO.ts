import { DurableObject } from 'cloudflare:workers';
import {
  LeaderboardEntry,
  ScoreSubmissionPayload,
  FriendInfo,
  PartyInfo,
  PartyMemberInfo,
  CloudSaveData
} from './types';

interface Env {
  GAME_ROOM: DurableObjectNamespace;
  WORLD_ZONE: DurableObjectNamespace;
  SOCIAL_HUB: DurableObjectNamespace;
  JWT_SECRET: string;
}

/**
 * Calculates ISO-8601 week number and year from a timestamp.
 * Guarantees a consistent server-authoritative weekly time boundary.
 */
function getWeekInfo(date: Date): { year: number; week: number } {
  const target = new Date(date.valueOf());
  const dayNr = (date.getUTCDay() + 6) % 7; // Monday = 0
  target.setUTCDate(target.getUTCDate() - dayNr + 3);
  const firstThursday = target.valueOf();
  target.setUTCMonth(0, 1);
  if (target.getUTCDay() !== 4) {
    target.setUTCMonth(0, 1 + ((4 - target.getUTCDay() + 7) % 7));
  }
  const weekNumber = 1 + Math.ceil((firstThursday - target.valueOf()) / 604800000);
  return { year: target.getUTCFullYear(), week: weekNumber };
}

export class SocialHubDO extends DurableObject {
  constructor(ctx: DurableObjectState, env: Env) {
    super(ctx, env);

    // Initialize SQLite storage for Social, Leaderboards, Parties, and Cloud Saves
    this.ctx.storage.sql.exec(`
      CREATE TABLE IF NOT EXISTS leaderboards (
        id INTEGER PRIMARY KEY AUTOINCREMENT,
        player_id TEXT NOT NULL,
        username TEXT NOT NULL,
        score INTEGER NOT NULL,
        length INTEGER NOT NULL,
        evolution_path INTEGER NOT NULL,
        year INTEGER NOT NULL,
        week_number INTEGER NOT NULL,
        submitted_at INTEGER NOT NULL
      );

      CREATE INDEX IF NOT EXISTS idx_leaderboards_global ON leaderboards(score DESC);
      CREATE INDEX IF NOT EXISTS idx_leaderboards_weekly ON leaderboards(year, week_number, score DESC);

      CREATE TABLE IF NOT EXISTS friends (
        player_id TEXT NOT NULL,
        friend_id TEXT NOT NULL,
        friend_username TEXT NOT NULL,
        created_at INTEGER NOT NULL,
        PRIMARY KEY (player_id, friend_id)
      );

      CREATE TABLE IF NOT EXISTS parties (
        party_id TEXT PRIMARY KEY,
        leader_id TEXT NOT NULL,
        leader_name TEXT NOT NULL,
        created_at INTEGER NOT NULL
      );

      CREATE TABLE IF NOT EXISTS party_members (
        party_id TEXT NOT NULL,
        player_id TEXT NOT NULL,
        username TEXT NOT NULL,
        is_leader INTEGER NOT NULL,
        joined_at INTEGER NOT NULL,
        PRIMARY KEY (party_id, player_id)
      );

      CREATE TABLE IF NOT EXISTS cloud_saves (
        player_id TEXT PRIMARY KEY,
        save_version INTEGER NOT NULL,
        save_data_json TEXT NOT NULL,
        checksum TEXT NOT NULL,
        updated_at INTEGER NOT NULL
      );
    `);
  }

  async fetch(request: Request): Promise<Response> {
    const url = new URL(request.url);
    const path = url.pathname;

    try {
      // 1. Submit Score to Leaderboard
      if (path === '/submit-score' && request.method === 'POST') {
        const body = (await request.json().catch(() => ({}))) as Partial<ScoreSubmissionPayload & { playerId: string; username: string }>;
        const playerId = body.playerId || url.searchParams.get('playerId') || '';
        const username = body.username || url.searchParams.get('username') || '';
        const score = body.score;
        const length = body.length || 50;
        const evolutionPath = body.evolutionPath || 0;

        // Server-Side Anti-Cheat Sanity Checks
        if (!playerId || !username || typeof score !== 'number' || score < 0) {
          return Response.json({ success: false, error: 'Invalid score submission payload' }, { status: 400 });
        }

        // Bounded sanity limits (Score vs reasonable Serpent length)
        if (score > 10000000 || length < 1 || length > 10000) {
          return Response.json({ success: false, error: 'Score exceeds physical simulation boundary' }, { status: 400 });
        }

        const now = new Date();
        const { year, week } = getWeekInfo(now);
        const submittedAt = now.getTime();

        this.ctx.storage.sql.exec(
          `INSERT INTO leaderboards (player_id, username, score, length, evolution_path, year, week_number, submitted_at)
           VALUES (?, ?, ?, ?, ?, ?, ?, ?)`,
          playerId,
          username,
          Math.floor(score),
          Math.floor(length),
          evolutionPath || 0,
          year,
          week,
          submittedAt
        );

        return Response.json({ success: true, submittedAt });
      }

      // 2. Query Global Leaderboard
      if (path === '/leaderboard/global' && request.method === 'GET') {
        const limit = Math.min(Math.max(parseInt(url.searchParams.get('limit') || '50', 10), 1), 100);
        const offset = Math.max(parseInt(url.searchParams.get('offset') || '0', 10), 0);

        const cursor = this.ctx.storage.sql.exec(
          `SELECT player_id, username, score, length, evolution_path, submitted_at
           FROM leaderboards
           ORDER BY score DESC
           LIMIT ? OFFSET ?`,
          limit,
          offset
        );

        const entries: LeaderboardEntry[] = [];
        for (const row of cursor) {
          entries.push({
            playerId: row.player_id as string,
            username: row.username as string,
            score: row.score as number,
            length: row.length as number,
            evolutionPath: row.evolution_path as number,
            submittedAt: row.submitted_at as number,
          });
        }

        return Response.json({ success: true, count: entries.length, entries });
      }

      // 3. Query Weekly Leaderboard
      if (path === '/leaderboard/weekly' && request.method === 'GET') {
        const limit = Math.min(Math.max(parseInt(url.searchParams.get('limit') || '50', 10), 1), 100);
        const offset = Math.max(parseInt(url.searchParams.get('offset') || '0', 10), 0);

        const now = new Date();
        const { year, week } = getWeekInfo(now);

        const cursor = this.ctx.storage.sql.exec(
          `SELECT player_id, username, score, length, evolution_path, submitted_at
           FROM leaderboards
           WHERE year = ? AND week_number = ?
           ORDER BY score DESC
           LIMIT ? OFFSET ?`,
          year,
          week,
          limit,
          offset
        );

        const entries: LeaderboardEntry[] = [];
        for (const row of cursor) {
          entries.push({
            playerId: row.player_id as string,
            username: row.username as string,
            score: row.score as number,
            length: row.length as number,
            evolutionPath: row.evolution_path as number,
            submittedAt: row.submitted_at as number,
          });
        }

        return Response.json({ success: true, year, week, count: entries.length, entries });
      }

      // 4. Friends Management
      if (path === '/friends/list' && request.method === 'GET') {
        const playerId = url.searchParams.get('playerId');
        if (!playerId) return Response.json({ success: false, error: 'Missing playerId' }, { status: 400 });

        const cursor = this.ctx.storage.sql.exec(
          `SELECT friend_id, friend_username, created_at FROM friends WHERE player_id = ?`,
          playerId
        );

        const friends: FriendInfo[] = [];
        for (const row of cursor) {
          friends.push({
            playerId: row.friend_id as string,
            username: row.friend_username as string,
            addedAt: row.created_at as number,
            isOnline: true // Active network identity
          });
        }

        return Response.json({ success: true, friends });
      }

      if (path === '/friends/add' && request.method === 'POST') {
        const body = (await request.json().catch(() => ({}))) as { playerId?: string; friendId?: string; friendUsername?: string };
        const playerId = body.playerId || url.searchParams.get('playerId') || '';
        const friendId = body.friendId || '';
        const friendUsername = body.friendUsername || 'AetherDrifter';
        if (!playerId || !friendId) return Response.json({ success: false, error: 'Invalid parameters' }, { status: 400 });

        this.ctx.storage.sql.exec(
          `INSERT OR REPLACE INTO friends (player_id, friend_id, friend_username, created_at)
           VALUES (?, ?, ?, ?)`,
          playerId,
          friendId,
          friendUsername,
          Date.now()
        );

        return Response.json({ success: true });
      }

      if (path === '/friends/remove' && request.method === 'POST') {
        const body = (await request.json().catch(() => ({}))) as { playerId?: string; friendId?: string };
        const playerId = body.playerId || url.searchParams.get('playerId') || '';
        const friendId = body.friendId || '';
        if (!playerId || !friendId) return Response.json({ success: false, error: 'Invalid parameters' }, { status: 400 });

        this.ctx.storage.sql.exec(
          `DELETE FROM friends WHERE player_id = ? AND friend_id = ?`,
          playerId,
          friendId
        );

        return Response.json({ success: true });
      }

      // 5. Party Management
      if (path === '/party/create' && request.method === 'POST') {
        const body = (await request.json().catch(() => ({}))) as { playerId?: string; username?: string };
        const playerId = body.playerId || url.searchParams.get('playerId') || '';
        const username = body.username || url.searchParams.get('username') || 'AetherDrifter';
        const partyId = `PTY_${crypto.randomUUID().substring(0, 6).toUpperCase()}`;

        this.ctx.storage.sql.exec(
          `INSERT INTO parties (party_id, leader_id, leader_name, created_at) VALUES (?, ?, ?, ?)`,
          partyId,
          playerId,
          username,
          Date.now()
        );

        this.ctx.storage.sql.exec(
          `INSERT INTO party_members (party_id, player_id, username, is_leader, joined_at) VALUES (?, ?, ?, 1, ?)`,
          partyId,
          playerId,
          username,
          Date.now()
        );

        return Response.json({
          success: true,
          party: {
            partyId,
            leaderId: playerId,
            leaderName: username,
            members: [{ playerId, username, isLeader: true, joinedAt: Date.now() }],
            createdAt: Date.now()
          }
        });
      }

      if (path === '/party/join' && request.method === 'POST') {
        const body = (await request.json().catch(() => ({}))) as { partyId?: string; playerId?: string; username?: string };
        const partyId = body.partyId || '';
        const playerId = body.playerId || url.searchParams.get('playerId') || '';
        const username = body.username || url.searchParams.get('username') || 'AetherDrifter';

        const partyCursor = this.ctx.storage.sql.exec(`SELECT * FROM parties WHERE party_id = ?`, partyId);
        let partyRow: Record<string, unknown> | null = null;
        for (const row of partyCursor) {
          partyRow = row;
          break;
        }

        if (!partyRow) {
          return Response.json({ success: false, error: 'Party code not found' }, { status: 404 });
        }

        this.ctx.storage.sql.exec(
          `INSERT OR REPLACE INTO party_members (party_id, player_id, username, is_leader, joined_at) VALUES (?, ?, ?, 0, ?)`,
          partyId,
          playerId,
          username,
          Date.now()
        );

        return Response.json({ success: true, partyId });
      }

      if (path === '/party/info' && request.method === 'GET') {
        const partyId = url.searchParams.get('partyId');
        if (!partyId) return Response.json({ success: false, error: 'Missing partyId' }, { status: 400 });

        const partyCursor = this.ctx.storage.sql.exec(`SELECT * FROM parties WHERE party_id = ?`, partyId);
        let partyRow: Record<string, unknown> | null = null;
        for (const row of partyCursor) {
          partyRow = row;
          break;
        }

        if (!partyRow) return Response.json({ success: false, error: 'Party not found' }, { status: 404 });

        const membersCursor = this.ctx.storage.sql.exec(`SELECT * FROM party_members WHERE party_id = ?`, partyId);
        const members: PartyMemberInfo[] = [];
        for (const row of membersCursor) {
          members.push({
            playerId: row.player_id as string,
            username: row.username as string,
            isLeader: (row.is_leader as number) === 1,
            joinedAt: row.joined_at as number,
          });
        }

        const party: PartyInfo = {
          partyId,
          leaderId: partyRow.leader_id as string,
          leaderName: partyRow.leader_name as string,
          members,
          createdAt: partyRow.created_at as number,
        };

        return Response.json({ success: true, party });
      }

      // 6. Cloud Save API
      if (path === '/save/cloud' && request.method === 'POST') {
        const body = (await request.json()) as CloudSaveData;
        const { playerId, saveVersion, saveDataJson, checksum } = body;
        if (!playerId || !saveDataJson) {
          return Response.json({ success: false, error: 'Missing save payload' }, { status: 400 });
        }

        this.ctx.storage.sql.exec(
          `INSERT OR REPLACE INTO cloud_saves (player_id, save_version, save_data_json, checksum, updated_at)
           VALUES (?, ?, ?, ?, ?)`,
          playerId,
          saveVersion || 1,
          saveDataJson,
          checksum || '',
          Date.now()
        );

        return Response.json({ success: true, savedAt: Date.now() });
      }

      if (path === '/save/cloud' && request.method === 'GET') {
        const playerId = url.searchParams.get('playerId');
        if (!playerId) return Response.json({ success: false, error: 'Missing playerId' }, { status: 400 });

        const cursor = this.ctx.storage.sql.exec(`SELECT * FROM cloud_saves WHERE player_id = ?`, playerId);
        let saveRow: Record<string, unknown> | null = null;
        for (const row of cursor) {
          saveRow = row;
          break;
        }

        if (!saveRow) {
          return Response.json({ success: true, exists: false, save: null });
        }

        return Response.json({
          success: true,
          exists: true,
          save: {
            playerId: saveRow.player_id as string,
            saveVersion: saveRow.save_version as number,
            saveDataJson: saveRow.save_data_json as string,
            checksum: saveRow.checksum as string,
            updatedAt: saveRow.updated_at as number,
          }
        });
      }

      return new Response('Not Found in SocialHubDO', { status: 404 });
    } catch (err: unknown) {
      const errStr = err instanceof Error ? err.message : String(err);
      return Response.json({ success: false, error: errStr }, { status: 500 });
    }
  }
}
