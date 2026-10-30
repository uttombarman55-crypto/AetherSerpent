import { DurableObject } from 'cloudflare:workers';
import {
  LeaderboardEntry,
  ScoreSubmissionPayload,
  FriendInfo,
  PartyInfo,
  PartyMemberInfo,
  CloudSaveData,
  AccountProfile,
  PrivacySettings,
  BlockedUser
} from './types';

interface Env {
  GAME_ROOM: DurableObjectNamespace;
  WORLD_ZONE: DurableObjectNamespace;
  SOCIAL_HUB: DurableObjectNamespace;
  JWT_SECRET: string;
}

const RESERVED_USERNAMES = new Set([
  'admin', 'administrator', 'moderator', 'staff', 'official', 'system',
  'aetherserpent', 'gm_', 'support', 'security', 'owner', 'root', 'bot',
  'aether', 'developer', 'dev'
]);

const PROFANITY_PATTERNS = [
  /\b(?:nigger|faggot|cunt|fuck|shit|bitch|whore|slut|retard)\b/i,
  /n[i1l]gg/i,
  /f[a4]g/i,
  /c[u*]nt/i
];

export function validateUsername(name: string): { valid: boolean; error?: string } {
  if (!name || typeof name !== 'string') return { valid: false, error: 'Username is required.' };
  const trimmed = name.trim();
  if (trimmed.length < 3 || trimmed.length > 16) {
    return { valid: false, error: 'Username must be between 3 and 16 characters.' };
  }
  if (!/^[a-zA-Z0-9_]+$/.test(trimmed)) {
    return { valid: false, error: 'Username can only contain letters, numbers, and underscores.' };
  }
  const lower = trimmed.toLowerCase();
  for (const reserved of RESERVED_USERNAMES) {
    if (lower === reserved || lower.startsWith(reserved + '_') || lower.startsWith('gm_')) {
      return { valid: false, error: 'This username is reserved by system administration.' };
    }
  }
  for (const pattern of PROFANITY_PATTERNS) {
    if (pattern.test(lower)) {
      return { valid: false, error: 'Username contains prohibited language.' };
    }
  }
  return { valid: true };
}

export function filterProfanity(text: string): string {
  if (!text) return '';
  let sanitized = text;
  for (const pattern of PROFANITY_PATTERNS) {
    sanitized = sanitized.replace(pattern, '***');
  }
  return sanitized;
}

async function hashPassword(password: string, salt: string): Promise<string> {
  const enc = new TextEncoder();
  const data = enc.encode(password + ':' + salt);
  const hash = await crypto.subtle.digest('SHA-256', data);
  return Array.from(new Uint8Array(hash)).map(b => b.toString(16).padStart(2, '0')).join('');
}

function generateNumericId(): number {
  // Free Fire style 9-digit numeric Player ID: 100,000,000 to 999,999,999
  return Math.floor(100000000 + Math.random() * 900000000);
}

function generateRecoveryCode(): string {
  const chars = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789';
  let p1 = '', p2 = '';
  for (let i = 0; i < 4; i++) p1 += chars.charAt(Math.floor(Math.random() * chars.length));
  for (let i = 0; i < 4; i++) p2 += chars.charAt(Math.floor(Math.random() * chars.length));
  return `${p1}-${p2}`;
}

function getWeekInfo(date: Date): { year: number; week: number } {
  const target = new Date(date.valueOf());
  const dayNr = (date.getUTCDay() + 6) % 7;
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

    // Initialize all SQLite schemas: Accounts, Profiles, Privacy, Blocks, Reports, Leaderboards, Friends, Parties, Saves
    this.ctx.storage.sql.exec(`
      CREATE TABLE IF NOT EXISTS accounts (
        id TEXT PRIMARY KEY,
        numeric_id INTEGER UNIQUE NOT NULL,
        username TEXT UNIQUE NOT NULL,
        password_hash TEXT,
        salt TEXT,
        is_guest INTEGER NOT NULL DEFAULT 0,
        device_id TEXT,
        recovery_code TEXT,
        username_changes_left INTEGER DEFAULT 3,
        created_at INTEGER NOT NULL,
        last_login_at INTEGER NOT NULL
      );

      CREATE INDEX IF NOT EXISTS idx_accounts_numeric_id ON accounts(numeric_id);
      CREATE INDEX IF NOT EXISTS idx_accounts_device_id ON accounts(device_id);

      CREATE TABLE IF NOT EXISTS profiles (
        player_id TEXT PRIMARY KEY,
        avatar_id INTEGER DEFAULT 1,
        frame_id INTEGER DEFAULT 1,
        banner_id INTEGER DEFAULT 1,
        bio TEXT DEFAULT 'Drifting through the celestial void.',
        squad_name TEXT DEFAULT '',
        level INTEGER DEFAULT 1,
        exp INTEGER DEFAULT 0,
        matches_played INTEGER DEFAULT 0,
        total_kills INTEGER DEFAULT 0,
        highest_score INTEGER DEFAULT 0,
        highest_length INTEGER DEFAULT 50,
        favorite_evolution INTEGER DEFAULT 0,
        updated_at INTEGER NOT NULL,
        FOREIGN KEY(player_id) REFERENCES accounts(id)
      );

      CREATE TABLE IF NOT EXISTS privacy_settings (
        player_id TEXT PRIMARY KEY,
        allow_invites INTEGER DEFAULT 0,
        allow_messages INTEGER DEFAULT 0,
        show_online_status INTEGER DEFAULT 1,
        FOREIGN KEY(player_id) REFERENCES accounts(id)
      );

      CREATE TABLE IF NOT EXISTS blocked_players (
        player_id TEXT NOT NULL,
        blocked_id TEXT NOT NULL,
        created_at INTEGER NOT NULL,
        PRIMARY KEY (player_id, blocked_id)
      );

      CREATE TABLE IF NOT EXISTS player_reports (
        id INTEGER PRIMARY KEY AUTOINCREMENT,
        reporter_id TEXT NOT NULL,
        target_id TEXT NOT NULL,
        reason TEXT NOT NULL,
        details TEXT,
        created_at INTEGER NOT NULL
      );

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

  private buildProfileObject(accountRow: Record<string, unknown>, profileRow?: Record<string, unknown> | null): AccountProfile {
    return {
      playerId: accountRow.id as string,
      numericId: accountRow.numeric_id as number,
      username: accountRow.username as string,
      isGuest: (accountRow.is_guest as number) === 1,
      avatarId: (profileRow?.avatar_id as number) ?? 1,
      frameId: (profileRow?.frame_id as number) ?? 1,
      bannerId: (profileRow?.banner_id as number) ?? 1,
      bio: (profileRow?.bio as string) ?? 'Drifting through the celestial void.',
      squadName: (profileRow?.squad_name as string) ?? '',
      level: (profileRow?.level as number) ?? 1,
      exp: (profileRow?.exp as number) ?? 0,
      matchesPlayed: (profileRow?.matches_played as number) ?? 0,
      totalKills: (profileRow?.total_kills as number) ?? 0,
      highestScore: (profileRow?.highest_score as number) ?? 0,
      highestLength: (profileRow?.highest_length as number) ?? 50,
      favoriteEvolution: (profileRow?.favorite_evolution as number) ?? 0,
      usernameChangesLeft: (accountRow.username_changes_left as number) ?? 3,
      createdAt: accountRow.created_at as number,
    };
  }

  private getProfileByPlayerId(playerId: string): AccountProfile | null {
    const accCursor = this.ctx.storage.sql.exec(`SELECT * FROM accounts WHERE id = ?`, playerId);
    let acc: Record<string, unknown> | null = null;
    for (const r of accCursor) { acc = r; break; }
    if (!acc) return null;

    const profCursor = this.ctx.storage.sql.exec(`SELECT * FROM profiles WHERE player_id = ?`, playerId);
    let prof: Record<string, unknown> | null = null;
    for (const r of profCursor) { prof = r; break; }

    return this.buildProfileObject(acc, prof);
  }

  async fetch(request: Request): Promise<Response> {
    const url = new URL(request.url);
    const path = url.pathname;

    try {
      // ---------------------------------------------------------
      // 1. ACCOUNT SYSTEM ENDPOINTS
      // ---------------------------------------------------------

      // Register full account
      if (path === '/account/register' && request.method === 'POST') {
        const body = (await request.json().catch(() => ({}))) as {
          username?: string;
          password?: string;
          deviceId?: string;
        };

        const val = validateUsername(body.username || '');
        if (!val.valid) return Response.json({ success: false, error: val.error }, { status: 400 });

        const username = body.username!.trim();
        const password = body.password || '';
        if (password.length < 6) {
          return Response.json({ success: false, error: 'Password must be at least 6 characters.' }, { status: 400 });
        }

        // Check unique username
        const existCursor = this.ctx.storage.sql.exec(`SELECT id FROM accounts WHERE username = ? COLLATE NOCASE`, username);
        for (const _ of existCursor) {
          return Response.json({ success: false, error: 'Username is already taken. Please choose another.' }, { status: 409 });
        }

        const playerId = `usr_${crypto.randomUUID().substring(0, 8)}`;
        let numericId = generateNumericId();

        // Ensure unique 9-digit numeric ID
        for (let i = 0; i < 5; i++) {
          const numCursor = this.ctx.storage.sql.exec(`SELECT id FROM accounts WHERE numeric_id = ?`, numericId);
          let clash = false;
          for (const _ of numCursor) { clash = true; break; }
          if (!clash) break;
          numericId = generateNumericId();
        }

        const salt = crypto.randomUUID().replace(/-/g, '');
        const passHash = await hashPassword(password, salt);
        const recoveryCode = generateRecoveryCode();
        const now = Date.now();

        this.ctx.storage.sql.exec(
          `INSERT INTO accounts (id, numeric_id, username, password_hash, salt, is_guest, device_id, recovery_code, username_changes_left, created_at, last_login_at)
           VALUES (?, ?, ?, ?, ?, 0, ?, ?, 3, ?, ?)`,
          playerId,
          numericId,
          username,
          passHash,
          salt,
          body.deviceId || '',
          recoveryCode,
          now,
          now
        );

        this.ctx.storage.sql.exec(
          `INSERT INTO profiles (player_id, avatar_id, frame_id, banner_id, bio, squad_name, level, exp, updated_at)
           VALUES (?, 1, 1, 1, 'Drifting through the celestial void.', '', 1, 0, ?)`,
          playerId,
          now
        );

        this.ctx.storage.sql.exec(
          `INSERT INTO privacy_settings (player_id, allow_invites, allow_messages, show_online_status)
           VALUES (?, 0, 0, 1)`,
          playerId
        );

        const profile = this.getProfileByPlayerId(playerId)!;

        return Response.json({
          success: true,
          account: {
            playerId,
            numericId,
            username,
            isGuest: false,
          },
          recoveryCode,
          profile
        });
      }

      // Login
      if (path === '/account/login' && request.method === 'POST') {
        const body = (await request.json().catch(() => ({}))) as {
          username?: string;
          password?: string;
          deviceId?: string;
        };

        const username = body.username?.trim() || '';
        const password = body.password || '';

        const accCursor = this.ctx.storage.sql.exec(
          `SELECT * FROM accounts WHERE username = ? COLLATE NOCASE OR numeric_id = ?`,
          username,
          parseInt(username, 10) || -1
        );

        let acc: Record<string, unknown> | null = null;
        for (const r of accCursor) { acc = r; break; }

        if (!acc) {
          return Response.json({ success: false, error: 'Invalid username or password.' }, { status: 401 });
        }

        if ((acc.is_guest as number) === 1) {
          return Response.json({ success: false, error: 'This is a guest account. Please use Guest Login or upgrade it.' }, { status: 400 });
        }

        const calculatedHash = await hashPassword(password, acc.salt as string);
        if (calculatedHash !== (acc.password_hash as string)) {
          return Response.json({ success: false, error: 'Invalid username or password.' }, { status: 401 });
        }

        const now = Date.now();
        this.ctx.storage.sql.exec(
          `UPDATE accounts SET last_login_at = ?, device_id = COALESCE(?, device_id) WHERE id = ?`,
          now,
          body.deviceId || null,
          acc.id as string
        );

        const profile = this.getProfileByPlayerId(acc.id as string)!;

        return Response.json({
          success: true,
          account: {
            playerId: acc.id as string,
            numericId: acc.numeric_id as number,
            username: acc.username as string,
            isGuest: false,
          },
          profile
        });
      }

      // Guest Login (Binds to deviceId)
      if (path === '/account/guest-login' && request.method === 'POST') {
        const body = (await request.json().catch(() => ({}))) as { deviceId?: string };
        const deviceId = body.deviceId || `DEV_${crypto.randomUUID().substring(0, 8)}`;

        // Check if device already has a guest account
        const existCursor = this.ctx.storage.sql.exec(
          `SELECT * FROM accounts WHERE device_id = ? AND is_guest = 1 ORDER BY last_login_at DESC LIMIT 1`,
          deviceId
        );

        let acc: Record<string, unknown> | null = null;
        for (const r of existCursor) { acc = r; break; }

        const now = Date.now();

        if (acc) {
          // Existing guest account
          this.ctx.storage.sql.exec(`UPDATE accounts SET last_login_at = ? WHERE id = ?`, now, acc.id as string);
          const profile = this.getProfileByPlayerId(acc.id as string)!;
          return Response.json({
            success: true,
            account: {
              playerId: acc.id as string,
              numericId: acc.numeric_id as number,
              username: acc.username as string,
              isGuest: true,
            },
            profile
          });
        }

        // Create new guest
        const playerId = `usr_${crypto.randomUUID().substring(0, 8)}`;
        const numericId = generateNumericId();
        const guestUsername = `Guest_${String(numericId).substring(5)}`;

        this.ctx.storage.sql.exec(
          `INSERT INTO accounts (id, numeric_id, username, is_guest, device_id, username_changes_left, created_at, last_login_at)
           VALUES (?, ?, ?, 1, ?, 1, ?, ?)`,
          playerId,
          numericId,
          guestUsername,
          deviceId,
          now,
          now
        );

        this.ctx.storage.sql.exec(
          `INSERT INTO profiles (player_id, avatar_id, frame_id, banner_id, bio, squad_name, level, exp, updated_at)
           VALUES (?, 1, 1, 1, 'Celestial Guest drifter.', '', 1, 0, ?)`,
          playerId,
          now
        );

        this.ctx.storage.sql.exec(
          `INSERT INTO privacy_settings (player_id, allow_invites, allow_messages, show_online_status)
           VALUES (?, 0, 0, 1)`,
          playerId
        );

        const profile = this.getProfileByPlayerId(playerId)!;

        return Response.json({
          success: true,
          account: {
            playerId,
            numericId,
            username: guestUsername,
            isGuest: true,
          },
          profile
        });
      }

      // Upgrade Guest Account
      if (path === '/account/upgrade-guest' && request.method === 'POST') {
        const body = (await request.json().catch(() => ({}))) as {
          playerId?: string;
          username?: string;
          password?: string;
        };

        const playerId = body.playerId || url.searchParams.get('playerId') || '';
        if (!playerId) return Response.json({ success: false, error: 'Missing playerId' }, { status: 400 });

        const accCursor = this.ctx.storage.sql.exec(`SELECT * FROM accounts WHERE id = ?`, playerId);
        let acc: Record<string, unknown> | null = null;
        for (const r of accCursor) { acc = r; break; }

        if (!acc) return Response.json({ success: false, error: 'Account not found.' }, { status: 404 });
        if ((acc.is_guest as number) !== 1) {
          return Response.json({ success: false, error: 'This account is already a registered account.' }, { status: 400 });
        }

        const val = validateUsername(body.username || '');
        if (!val.valid) return Response.json({ success: false, error: val.error }, { status: 400 });

        const newUsername = body.username!.trim();
        const password = body.password || '';
        if (password.length < 6) {
          return Response.json({ success: false, error: 'Password must be at least 6 characters.' }, { status: 400 });
        }

        // Check if username taken by another account
        const existCursor = this.ctx.storage.sql.exec(`SELECT id FROM accounts WHERE username = ? COLLATE NOCASE AND id != ?`, newUsername, playerId);
        for (const _ of existCursor) {
          return Response.json({ success: false, error: 'Username is already taken.' }, { status: 409 });
        }

        const salt = crypto.randomUUID().replace(/-/g, '');
        const passHash = await hashPassword(password, salt);
        const recoveryCode = generateRecoveryCode();

        this.ctx.storage.sql.exec(
          `UPDATE accounts
           SET username = ?, password_hash = ?, salt = ?, is_guest = 0, recovery_code = ?, username_changes_left = 3
           WHERE id = ?`,
          newUsername,
          passHash,
          salt,
          recoveryCode,
          playerId
        );

        this.ctx.storage.sql.exec(
          `UPDATE profiles SET bio = 'Celestial serpent awakened.', updated_at = ? WHERE player_id = ?`,
          Date.now(),
          playerId
        );

        const profile = this.getProfileByPlayerId(playerId)!;

        return Response.json({
          success: true,
          account: {
            playerId,
            numericId: acc.numeric_id as number,
            username: newUsername,
            isGuest: false,
          },
          recoveryCode,
          profile
        });
      }

      // Account Recovery
      if (path === '/account/recover' && request.method === 'POST') {
        const body = (await request.json().catch(() => ({}))) as {
          numericIdOrUsername?: string;
          recoveryCode?: string;
          newPassword?: string;
        };

        const target = body.numericIdOrUsername?.trim() || '';
        const code = body.recoveryCode?.trim().toUpperCase() || '';
        const newPassword = body.newPassword || '';

        if (!target || !code || newPassword.length < 6) {
          return Response.json({ success: false, error: 'Invalid recovery parameters or password too short.' }, { status: 400 });
        }

        const accCursor = this.ctx.storage.sql.exec(
          `SELECT * FROM accounts WHERE username = ? COLLATE NOCASE OR numeric_id = ?`,
          target,
          parseInt(target, 10) || -1
        );

        let acc: Record<string, unknown> | null = null;
        for (const r of accCursor) { acc = r; break; }

        if (!acc) return Response.json({ success: false, error: 'Account not found.' }, { status: 404 });

        const actualCode = (acc.recovery_code as string || '').toUpperCase();
        if (actualCode !== code) {
          return Response.json({ success: false, error: 'Invalid recovery code.' }, { status: 403 });
        }

        const salt = crypto.randomUUID().replace(/-/g, '');
        const passHash = await hashPassword(newPassword, salt);
        const newRecoveryCode = generateRecoveryCode();

        this.ctx.storage.sql.exec(
          `UPDATE accounts SET password_hash = ?, salt = ?, recovery_code = ? WHERE id = ?`,
          passHash,
          salt,
          newRecoveryCode,
          acc.id as string
        );

        return Response.json({
          success: true,
          message: 'Account password updated successfully.',
          recoveryCode: newRecoveryCode
        });
      }

      // Change Username
      if (path === '/account/change-username' && request.method === 'POST') {
        const body = (await request.json().catch(() => ({}))) as { playerId?: string; newUsername?: string };
        const playerId = body.playerId || url.searchParams.get('playerId') || '';
        const val = validateUsername(body.newUsername || '');
        if (!val.valid) return Response.json({ success: false, error: val.error }, { status: 400 });

        const newName = body.newUsername!.trim();

        const accCursor = this.ctx.storage.sql.exec(`SELECT * FROM accounts WHERE id = ?`, playerId);
        let acc: Record<string, unknown> | null = null;
        for (const r of accCursor) { acc = r; break; }
        if (!acc) return Response.json({ success: false, error: 'Account not found.' }, { status: 404 });

        const left = (acc.username_changes_left as number) || 0;
        if (left <= 0) {
          return Response.json({ success: false, error: 'No username changes remaining on this account.' }, { status: 403 });
        }

        const clashCursor = this.ctx.storage.sql.exec(`SELECT id FROM accounts WHERE username = ? COLLATE NOCASE AND id != ?`, newName, playerId);
        for (const _ of clashCursor) {
          return Response.json({ success: false, error: 'Username is already taken.' }, { status: 409 });
        }

        this.ctx.storage.sql.exec(
          `UPDATE accounts SET username = ?, username_changes_left = username_changes_left - 1 WHERE id = ?`,
          newName,
          playerId
        );

        // Also update leaderboards & friends cache
        this.ctx.storage.sql.exec(`UPDATE leaderboards SET username = ? WHERE player_id = ?`, newName, playerId);
        this.ctx.storage.sql.exec(`UPDATE friends SET friend_username = ? WHERE friend_id = ?`, newName, playerId);

        const updatedProfile = this.getProfileByPlayerId(playerId)!;

        return Response.json({
          success: true,
          username: newName,
          usernameChangesLeft: left - 1,
          profile: updatedProfile
        });
      }

      // ---------------------------------------------------------
      // 2. PROFILE & CUSTOMIZATION ENDPOINTS
      // ---------------------------------------------------------

      // Get Profile
      if (path === '/profile/get' && request.method === 'GET') {
        const playerId = url.searchParams.get('playerId');
        const numericId = parseInt(url.searchParams.get('numericId') || '-1', 10);
        const username = url.searchParams.get('username');

        let accCursor;
        if (numericId > 0) {
          accCursor = this.ctx.storage.sql.exec(`SELECT * FROM accounts WHERE numeric_id = ?`, numericId);
        } else if (username) {
          accCursor = this.ctx.storage.sql.exec(`SELECT * FROM accounts WHERE username = ? COLLATE NOCASE`, username);
        } else if (playerId) {
          accCursor = this.ctx.storage.sql.exec(`SELECT * FROM accounts WHERE id = ?`, playerId);
        } else {
          return Response.json({ success: false, error: 'Missing playerId, numericId, or username' }, { status: 400 });
        }

        let acc: Record<string, unknown> | null = null;
        for (const r of accCursor) { acc = r; break; }
        if (!acc) return Response.json({ success: false, error: 'Player profile not found.' }, { status: 404 });

        const profile = this.getProfileByPlayerId(acc.id as string);
        return Response.json({ success: true, profile });
      }

      // Update Profile (Avatar, Frame, Banner, Bio, Squad)
      if (path === '/profile/update' && request.method === 'POST') {
        const body = (await request.json().catch(() => ({}))) as {
          playerId?: string;
          avatarId?: number;
          frameId?: number;
          bannerId?: number;
          bio?: string;
          squadName?: string;
        };

        const playerId = body.playerId || url.searchParams.get('playerId') || '';
        if (!playerId) return Response.json({ success: false, error: 'Missing playerId' }, { status: 400 });

        const bio = filterProfanity((body.bio || '').substring(0, 120));
        const squad = filterProfanity((body.squadName || '').substring(0, 24));

        this.ctx.storage.sql.exec(
          `UPDATE profiles
           SET avatar_id = COALESCE(?, avatar_id),
               frame_id = COALESCE(?, frame_id),
               banner_id = COALESCE(?, banner_id),
               bio = CASE WHEN ? IS NOT NULL THEN ? ELSE bio END,
               squad_name = CASE WHEN ? IS NOT NULL THEN ? ELSE squad_name END,
               updated_at = ?
           WHERE player_id = ?`,
          body.avatarId ?? null,
          body.frameId ?? null,
          body.bannerId ?? null,
          body.bio !== undefined ? bio : null,
          body.bio !== undefined ? bio : null,
          body.squadName !== undefined ? squad : null,
          body.squadName !== undefined ? squad : null,
          Date.now(),
          playerId
        );

        const profile = this.getProfileByPlayerId(playerId);
        return Response.json({ success: true, profile });
      }

      // Search Profiles by Numeric ID or Username
      if (path === '/profile/search' && request.method === 'GET') {
        const query = url.searchParams.get('query')?.trim() || '';
        if (!query) return Response.json({ success: true, count: 0, results: [] });

        const num = parseInt(query, 10);
        let cursor;
        if (!isNaN(num) && query.length >= 4) {
          cursor = this.ctx.storage.sql.exec(
            `SELECT * FROM accounts WHERE numeric_id = ? OR CAST(numeric_id AS TEXT) LIKE ? LIMIT 10`,
            num,
            `%${query}%`
          );
        } else {
          cursor = this.ctx.storage.sql.exec(
            `SELECT * FROM accounts WHERE username LIKE ? LIMIT 10`,
            `%${query}%`
          );
        }

        const results: AccountProfile[] = [];
        for (const acc of cursor) {
          const prof = this.getProfileByPlayerId(acc.id as string);
          if (prof) results.push(prof);
        }

        return Response.json({ success: true, count: results.length, results });
      }

      // Privacy Settings
      if (path === '/profile/privacy' && request.method === 'GET') {
        const playerId = url.searchParams.get('playerId') || '';
        const cursor = this.ctx.storage.sql.exec(`SELECT * FROM privacy_settings WHERE player_id = ?`, playerId);
        let row: Record<string, unknown> | null = null;
        for (const r of cursor) { row = r; break; }

        const privacy: PrivacySettings = {
          allowInvites: (row?.allow_invites as number) ?? 0,
          allowMessages: (row?.allow_messages as number) ?? 0,
          showOnlineStatus: ((row?.show_online_status as number) ?? 1) === 1,
        };

        return Response.json({ success: true, privacy });
      }

      if (path === '/profile/privacy' && request.method === 'POST') {
        const body = (await request.json().catch(() => ({}))) as {
          playerId?: string;
          allowInvites?: number;
          allowMessages?: number;
          showOnlineStatus?: boolean;
        };

        const playerId = body.playerId || url.searchParams.get('playerId') || '';
        if (!playerId) return Response.json({ success: false, error: 'Missing playerId' }, { status: 400 });

        this.ctx.storage.sql.exec(
          `INSERT OR REPLACE INTO privacy_settings (player_id, allow_invites, allow_messages, show_online_status)
           VALUES (?, ?, ?, ?)`,
          playerId,
          body.allowInvites ?? 0,
          body.allowMessages ?? 0,
          body.showOnlineStatus ? 1 : 0
        );

        return Response.json({ success: true });
      }

      // Block / Unblock Player
      if (path === '/profile/block' && request.method === 'POST') {
        const body = (await request.json().catch(() => ({}))) as { playerId?: string; targetId?: string };
        const playerId = body.playerId || url.searchParams.get('playerId') || '';
        const targetId = body.targetId || '';
        if (!playerId || !targetId) return Response.json({ success: false, error: 'Missing parameters' }, { status: 400 });

        this.ctx.storage.sql.exec(
          `INSERT OR IGNORE INTO blocked_players (player_id, blocked_id, created_at) VALUES (?, ?, ?)`,
          playerId,
          targetId,
          Date.now()
        );

        // Also remove friend if was friends
        this.ctx.storage.sql.exec(`DELETE FROM friends WHERE player_id = ? AND friend_id = ?`, playerId, targetId);

        return Response.json({ success: true });
      }

      if (path === '/profile/unblock' && request.method === 'POST') {
        const body = (await request.json().catch(() => ({}))) as { playerId?: string; targetId?: string };
        const playerId = body.playerId || url.searchParams.get('playerId') || '';
        const targetId = body.targetId || '';
        if (!playerId || !targetId) return Response.json({ success: false, error: 'Missing parameters' }, { status: 400 });

        this.ctx.storage.sql.exec(`DELETE FROM blocked_players WHERE player_id = ? AND blocked_id = ?`, playerId, targetId);
        return Response.json({ success: true });
      }

      if (path === '/profile/blocklist' && request.method === 'GET') {
        const playerId = url.searchParams.get('playerId') || '';
        const cursor = this.ctx.storage.sql.exec(
          `SELECT b.blocked_id, b.created_at, a.username, a.numeric_id
           FROM blocked_players b
           LEFT JOIN accounts a ON a.id = b.blocked_id
           WHERE b.player_id = ?`,
          playerId
        );

        const list: BlockedUser[] = [];
        for (const row of cursor) {
          list.push({
            blockedId: row.blocked_id as string,
            username: (row.username as string) || 'Drifter',
            numericId: (row.numeric_id as number) || 0,
            blockedAt: row.created_at as number,
          });
        }

        return Response.json({ success: true, count: list.length, blockedUsers: list });
      }

      // Report Player
      if (path === '/profile/report' && request.method === 'POST') {
        const body = (await request.json().catch(() => ({}))) as {
          reporterId?: string;
          targetId?: string;
          reason?: string;
          details?: string;
        };

        const reporterId = body.reporterId || url.searchParams.get('playerId') || 'anonymous';
        const targetId = body.targetId || '';
        const reason = body.reason || 'unspecified';
        const details = (body.details || '').substring(0, 250);

        if (!targetId) return Response.json({ success: false, error: 'Target player required' }, { status: 400 });

        this.ctx.storage.sql.exec(
          `INSERT INTO player_reports (reporter_id, target_id, reason, details, created_at)
           VALUES (?, ?, ?, ?, ?)`,
          reporterId,
          targetId,
          reason,
          details,
          Date.now()
        );

        return Response.json({ success: true, message: 'Report received. Our moderation guardians will review this case.' });
      }

      // ---------------------------------------------------------
      // 3. LEADERBOARDS ENDPOINTS
      // ---------------------------------------------------------
      if (path === '/submit-score' && request.method === 'POST') {
        const body = (await request.json().catch(() => ({}))) as Partial<ScoreSubmissionPayload & { playerId: string; username: string }>;
        const playerId = body.playerId || url.searchParams.get('playerId') || '';
        const username = body.username || url.searchParams.get('username') || '';
        const score = body.score;
        const length = body.length || 50;
        const evolutionPath = body.evolutionPath || 0;

        if (!playerId || !username || typeof score !== 'number' || score < 0) {
          return Response.json({ success: false, error: 'Invalid score submission payload' }, { status: 400 });
        }

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

        // Update profile stats if higher
        this.ctx.storage.sql.exec(
          `UPDATE profiles
           SET highest_score = MAX(highest_score, ?),
               highest_length = MAX(highest_length, ?),
               matches_played = matches_played + 1,
               exp = exp + ?,
               level = 1 + (exp / 1000),
               updated_at = ?
           WHERE player_id = ?`,
          Math.floor(score),
          Math.floor(length),
          Math.floor(score / 10),
          submittedAt,
          playerId
        );

        return Response.json({ success: true, submittedAt });
      }

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

      // ---------------------------------------------------------
      // 4. FRIENDS & PARTIES ENDPOINTS
      // ---------------------------------------------------------
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
            isOnline: true
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

      // ---------------------------------------------------------
      // 5. CLOUD SAVE ENDPOINTS
      // ---------------------------------------------------------
      if (path === '/save/cloud' && request.method === 'POST') {
        const body = (await request.json().catch(() => ({}))) as CloudSaveData;
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
