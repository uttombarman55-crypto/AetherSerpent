import { DurableObject } from 'cloudflare:workers';
import {
  LeaderboardEntry,
  ScoreSubmissionPayload,
  FriendInfo,
  FriendRequestInfo,
  FriendDetailInfo,
  PartyInfo,
  PartyMemberInfo,
  SquadInfo,
  SquadMemberInfo,
  SquadInviteInfo,
  SquadChatMessage,
  ClanInfo,
  ClanMemberInfo,
  ClanJoinRequestInfo,
  ClanChatMessage,
  CloudSaveData,
  AccountProfile,
  PrivacySettings,
  BlockedUser,
  ChatChannel,
  ChatMessage,
  SendChatRequest,
  ChatHistoryResponse,
  VoiceChannel,
  VoiceUserState,
  VoiceSignalPayload,
  VoiceAudioPacket
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

export const EMOJI_MAP: Record<string, string> = {
  ':fire:': '🔥',
  ':snake:': '🐍',
  ':serpent:': '🐍',
  ':gg:': '🏆',
  ':crown:': '👑',
  ':heart:': '❤️',
  ':love:': '❤️',
  ':trophy:': '🏆',
  ':skull:': '💀',
  ':dead:': '💀',
  ':lightning:': '⚡',
  ':volt:': '⚡',
  ':shield:': '🛡️',
  ':star:': '⭐',
  ':dragon:': '🐉',
  ':gem:': '💎',
  ':diamond:': '💎',
  ':sword:': '⚔️',
  ':battle:': '⚔️',
  ':target:': '🎯',
  ':cool:': '😎',
  ':smile:': '😊',
  ':cry:': '😭',
  ':angry:': '😡',
  ':thumbsup:': '👍',
  ':clap:': '👏',
  ':party:': '🎉',
  ':sparkles:': '✨',
  ':aether:': '🌌',
  ':void:': '🕳️'
};

export function parseEmojis(text: string): string {
  if (!text) return '';
  let result = text;
  for (const [shortcut, emoji] of Object.entries(EMOJI_MAP)) {
    result = result.replaceAll(shortcut, emoji);
  }
  return result;
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
  private chatSockets = new Map<WebSocket, { playerId: string; username: string; numericId: number; clanId?: string; squadId?: string }>();
  private voiceSockets = new Map<WebSocket, VoiceUserState>();
  private lastGlobalChatTimes = new Map<string, number>();
  private lastChatTimes = new Map<string, number>();
  private lastMessageHashes = new Map<string, number>();

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

      CREATE TABLE IF NOT EXISTS friend_requests (
        id INTEGER PRIMARY KEY AUTOINCREMENT,
        sender_id TEXT NOT NULL,
        sender_name TEXT NOT NULL,
        sender_numeric_id INTEGER NOT NULL,
        receiver_id TEXT NOT NULL,
        receiver_name TEXT NOT NULL,
        status TEXT NOT NULL DEFAULT 'pending',
        created_at INTEGER NOT NULL
      );

      CREATE TABLE IF NOT EXISTS player_presence (
        player_id TEXT PRIMARY KEY,
        status TEXT NOT NULL DEFAULT 'online',
        in_match_room TEXT,
        last_seen_at INTEGER NOT NULL
      );

      CREATE TABLE IF NOT EXISTS squads (
        squad_id TEXT PRIMARY KEY,
        name TEXT NOT NULL,
        tag TEXT NOT NULL,
        leader_id TEXT NOT NULL,
        leader_name TEXT NOT NULL,
        max_members INTEGER DEFAULT 5,
        is_open INTEGER DEFAULT 1,
        squad_xp INTEGER DEFAULT 0,
        squad_level INTEGER DEFAULT 1,
        match_room_id TEXT,
        created_at INTEGER NOT NULL
      );

      CREATE TABLE IF NOT EXISTS squad_members (
        squad_id TEXT NOT NULL,
        player_id TEXT NOT NULL,
        username TEXT NOT NULL,
        numeric_id INTEGER NOT NULL,
        role TEXT NOT NULL DEFAULT 'member',
        is_ready INTEGER DEFAULT 0,
        joined_at INTEGER NOT NULL,
        PRIMARY KEY (squad_id, player_id)
      );

      CREATE TABLE IF NOT EXISTS squad_invites (
        id INTEGER PRIMARY KEY AUTOINCREMENT,
        squad_id TEXT NOT NULL,
        squad_name TEXT NOT NULL,
        squad_tag TEXT NOT NULL,
        inviter_id TEXT NOT NULL,
        inviter_name TEXT NOT NULL,
        target_id TEXT NOT NULL,
        status TEXT DEFAULT 'pending',
        created_at INTEGER NOT NULL
      );

      CREATE TABLE IF NOT EXISTS squad_chat (
        id INTEGER PRIMARY KEY AUTOINCREMENT,
        squad_id TEXT NOT NULL,
        sender_id TEXT NOT NULL,
        sender_name TEXT NOT NULL,
        message TEXT NOT NULL,
        sent_at INTEGER NOT NULL
      );

      CREATE TABLE IF NOT EXISTS clans (
        clan_id TEXT PRIMARY KEY,
        name TEXT NOT NULL UNIQUE,
        tag TEXT NOT NULL,
        leader_id TEXT NOT NULL,
        leader_name TEXT NOT NULL,
        description TEXT NOT NULL,
        announcement TEXT NOT NULL,
        badge_id INTEGER DEFAULT 1,
        max_members INTEGER DEFAULT 50,
        min_level INTEGER DEFAULT 1,
        is_open INTEGER DEFAULT 1,
        clan_xp INTEGER DEFAULT 0,
        clan_level INTEGER DEFAULT 1,
        created_at INTEGER NOT NULL
      );

      CREATE TABLE IF NOT EXISTS clan_members (
        clan_id TEXT NOT NULL,
        player_id TEXT NOT NULL,
        username TEXT NOT NULL,
        numeric_id INTEGER NOT NULL,
        rank TEXT NOT NULL DEFAULT 'member',
        contribution INTEGER DEFAULT 0,
        joined_at INTEGER NOT NULL,
        PRIMARY KEY (clan_id, player_id)
      );

      CREATE TABLE IF NOT EXISTS clan_join_requests (
        id INTEGER PRIMARY KEY AUTOINCREMENT,
        clan_id TEXT NOT NULL,
        player_id TEXT NOT NULL,
        username TEXT NOT NULL,
        numeric_id INTEGER NOT NULL,
        level INTEGER DEFAULT 1,
        status TEXT DEFAULT 'pending',
        created_at INTEGER NOT NULL
      );

      CREATE TABLE IF NOT EXISTS clan_chat (
        id INTEGER PRIMARY KEY AUTOINCREMENT,
        clan_id TEXT NOT NULL,
        sender_id TEXT NOT NULL,
        sender_name TEXT NOT NULL,
        sender_rank TEXT NOT NULL,
        message TEXT NOT NULL,
        sent_at INTEGER NOT NULL
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

      CREATE TABLE IF NOT EXISTS chat_messages (
        id INTEGER PRIMARY KEY AUTOINCREMENT,
        channel TEXT NOT NULL,
        sender_id TEXT NOT NULL,
        sender_name TEXT NOT NULL,
        sender_numeric_id INTEGER NOT NULL,
        clan_tag TEXT,
        squad_tag TEXT,
        recipient_id TEXT,
        recipient_name TEXT,
        target_group_id TEXT,
        message TEXT NOT NULL,
        timestamp INTEGER NOT NULL
      );

      CREATE INDEX IF NOT EXISTS idx_chat_channel ON chat_messages(channel, timestamp DESC);
      CREATE INDEX IF NOT EXISTS idx_chat_target ON chat_messages(target_group_id, timestamp DESC);
      CREATE INDEX IF NOT EXISTS idx_chat_whisper ON chat_messages(sender_id, recipient_id, timestamp DESC);
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

  private async processChatMessage(
    sender: { playerId: string; username: string; numericId: number },
    req: SendChatRequest
  ): Promise<{ success: boolean; chatMessage?: ChatMessage; error?: string }> {
    const channel = req.channel || 'global';
    let rawMsg = ((req.message || (req as any).text) || '').trim();
    if (!rawMsg) return { success: false, error: 'Message cannot be empty.' };
    if (rawMsg.length > 250) rawMsg = rawMsg.substring(0, 250);

    const now = Date.now();

    // Filter profanity & parse emojis upfront
    let cleanMsg = filterProfanity(rawMsg);
    cleanMsg = parseEmojis(cleanMsg);

    // Global Slow Mode (5s cooldown) via SQLite (persistent across DO hibernation)
    if (channel === 'global') {
      const lastGlobalCursor = this.ctx.storage.sql.exec(
        `SELECT timestamp FROM chat_messages WHERE sender_id = ? AND channel = 'global' ORDER BY timestamp DESC LIMIT 1`,
        sender.playerId
      );
      for (const r of lastGlobalCursor) {
        const lastGlobal = r.timestamp as number;
        const elapsed = (now - lastGlobal) / 1000;
        if (elapsed < 5.0) {
          const remaining = (5.0 - elapsed).toFixed(1);
          return { success: false, error: `Global Chat slow mode active. Wait ${remaining}s.` };
        }
        break;
      }
    } else {
      // General 0.8s rate limit via SQLite
      const lastChatCursor = this.ctx.storage.sql.exec(
        `SELECT timestamp FROM chat_messages WHERE sender_id = ? ORDER BY timestamp DESC LIMIT 1`,
        sender.playerId
      );
      for (const r of lastChatCursor) {
        const lastChat = r.timestamp as number;
        if (now - lastChat < 800) {
          return { success: false, error: 'You are chatting too fast.' };
        }
        break;
      }
    }

    // Spam duplicate detection (same player, same channel, same message within 15 seconds) via SQLite
    const recentDupCursor = this.ctx.storage.sql.exec(
      `SELECT id FROM chat_messages
       WHERE sender_id = ? AND channel = ? AND LOWER(message) = LOWER(?) AND timestamp > ? LIMIT 1`,
      sender.playerId, channel, cleanMsg, now - 15000
    );
    for (const _ of recentDupCursor) {
      return { success: false, error: 'Duplicate message blocked to prevent spam.' };
    }

    // Lookup sender's clan tag & squad tag
    let clanTag: string | undefined;
    let squadTag: string | undefined;

    const clanCursor = this.ctx.storage.sql.exec(
      `SELECT c.tag FROM clan_members cm JOIN clans c ON c.clan_id = cm.clan_id WHERE cm.player_id = ?`,
      sender.playerId
    );
    for (const c of clanCursor) { clanTag = c.tag as string; break; }

    const squadCursor = this.ctx.storage.sql.exec(
      `SELECT s.tag FROM squad_members sm JOIN squads s ON s.squad_id = sm.squad_id WHERE sm.player_id = ?`,
      sender.playerId
    );
    for (const s of squadCursor) { squadTag = s.tag as string; break; }

    // Whisper Channel validation
    let recipientId = req.recipientId;
    let recipientName = req.recipientName;
    const recipientNumId = req.recipientNumericId || (req as any).targetNumericId;
    const recipientUName = req.recipientName || (req as any).targetName;
    if (channel === 'whisper') {
      if (!recipientId && recipientNumId) {
        const rCursor = this.ctx.storage.sql.exec(`SELECT id, username FROM accounts WHERE numeric_id = ?`, recipientNumId);
        for (const r of rCursor) { recipientId = r.id as string; recipientName = r.username as string; break; }
      } else if (!recipientId && recipientUName) {
        const rCursor = this.ctx.storage.sql.exec(`SELECT id, username FROM accounts WHERE username = ? COLLATE NOCASE`, recipientUName);
        for (const r of rCursor) { recipientId = r.id as string; recipientName = r.username as string; break; }
      }

      if (!recipientId) return { success: false, error: 'Recipient player not found for whisper.' };
      if (recipientId === sender.playerId) return { success: false, error: 'Cannot whisper to yourself.' };

      // Blocked check
      const blockCheck = this.ctx.storage.sql.exec(
        `SELECT * FROM blocked_players WHERE player_id = ? AND blocked_id = ?`,
        recipientId, sender.playerId
      );
      for (const _ of blockCheck) {
        return { success: false, error: 'Message could not be delivered to this player.' };
      }

      // Privacy check
      const privCheck = this.ctx.storage.sql.exec(
        `SELECT allow_messages FROM privacy_settings WHERE player_id = ?`,
        recipientId
      );
      for (const p of privCheck) {
        if ((p.allow_messages as number) === 2) {
          return { success: false, error: 'This player has private whispers disabled.' };
        }
        if ((p.allow_messages as number) === 1) {
          const fCheck = this.ctx.storage.sql.exec(
            `SELECT * FROM friends WHERE player_id = ? AND friend_id = ?`,
            recipientId, sender.playerId
          );
          let isFriend = false;
          for (const _ of fCheck) { isFriend = true; break; }
          if (!isFriend) {
            return { success: false, error: 'This player only accepts private whispers from friends.' };
          }
        }
      }
    }

    let targetGroupId = req.roomId || req.squadId || req.clanId;
    if (channel === 'squad' && !targetGroupId) {
      const smCursor = this.ctx.storage.sql.exec(`SELECT squad_id FROM squad_members WHERE player_id = ?`, sender.playerId);
      for (const sm of smCursor) { targetGroupId = sm.squad_id as string; break; }
    }
    if (channel === 'clan' && !targetGroupId) {
      const cmCursor = this.ctx.storage.sql.exec(`SELECT clan_id FROM clan_members WHERE player_id = ?`, sender.playerId);
      for (const cm of cmCursor) { targetGroupId = cm.clan_id as string; break; }
    }

    // Save to SQLite chat_messages table
    this.ctx.storage.sql.exec(
      `INSERT INTO chat_messages (channel, sender_id, sender_name, sender_numeric_id, clan_tag, squad_tag, recipient_id, recipient_name, target_group_id, message, timestamp)
       VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)`,
      channel,
      sender.playerId,
      sender.username,
      sender.numericId,
      clanTag || null,
      squadTag || null,
      recipientId || null,
      recipientName || null,
      targetGroupId || null,
      cleanMsg,
      now
    );

    const lastIdCursor = this.ctx.storage.sql.exec(`SELECT last_insert_rowid() AS id`);
    let msgId = now;
    for (const r of lastIdCursor) { msgId = (r.id as number) || now; break; }

    const chatMessage: ChatMessage = {
      id: msgId,
      channel,
      senderId: sender.playerId,
      senderName: sender.username,
      senderNumericId: sender.numericId,
      clanTag,
      squadTag,
      recipientId,
      recipientName,
      targetGroupId,
      message: cleanMsg,
      timestamp: now,
    };

    // Broadcast to connected WebSockets
    this.broadcastChat(chatMessage);

    return { success: true, chatMessage };
  }

  private broadcastChat(msg: ChatMessage) {
    const payloadStr = JSON.stringify({ type: 'CHAT_BROADCAST', payload: msg, timestamp: Date.now() });

    for (const [ws, info] of this.chatSockets.entries()) {
      try {
        if (msg.channel === 'global') {
          ws.send(payloadStr);
        } else if (msg.channel === 'whisper') {
          if (info.playerId === msg.senderId || info.playerId === msg.recipientId) {
            ws.send(payloadStr);
          }
        } else if (msg.channel === 'squad') {
          if (info.playerId === msg.senderId || (msg.targetGroupId && info.squadId === msg.targetGroupId)) {
            ws.send(payloadStr);
          }
        } else if (msg.channel === 'clan') {
          if (info.playerId === msg.senderId || (msg.targetGroupId && info.clanId === msg.targetGroupId)) {
            ws.send(payloadStr);
          }
        } else if (msg.channel === 'match') {
          ws.send(payloadStr);
        }
      } catch {
        this.chatSockets.delete(ws);
      }
    }
  }

  private broadcastVoice(sender: VoiceUserState, payload: unknown, excludeWs?: WebSocket): void {
    const jsonStr = JSON.stringify(payload);
    for (const [ws, peerState] of this.voiceSockets.entries()) {
      if (ws === excludeWs) continue;
      if (peerState.channel !== sender.channel) continue;

      if (sender.channel === 'squad') {
        if (peerState.squadId && peerState.squadId === sender.squadId) {
          try { ws.send(jsonStr); } catch { this.voiceSockets.delete(ws); }
        }
      } else if (sender.channel === 'match') {
        if (peerState.roomId && peerState.roomId === sender.roomId) {
          try { ws.send(jsonStr); } catch { this.voiceSockets.delete(ws); }
        }
      }
    }
  }

  async webSocketMessage(ws: WebSocket, message: string | ArrayBuffer): Promise<void> {
    try {
      const text = typeof message === 'string' ? message : new TextDecoder().decode(message);
      const data = JSON.parse(text);

      if (data.type === 'PING') {
        ws.send(JSON.stringify({ type: 'PONG', timestamp: Date.now() }));
        return;
      }

      // Voice WebSocket handling
      const vSession = this.voiceSockets.get(ws);
      if (vSession) {
        if (data.type === 'VOICE_PING') {
          ws.send(JSON.stringify({ type: 'VOICE_PONG', timestamp: Date.now() }));
          return;
        }

        if (data.type === 'VOICE_CHANNEL_SET') {
          const oldChannel = vSession.channel;
          const newChannel = (data.channel || 'off') as VoiceChannel;
          vSession.channel = newChannel;
          if (data.squadId) vSession.squadId = data.squadId;
          if (data.roomId) vSession.roomId = data.roomId;

          // Notify old channel peers of leave
          if (oldChannel !== 'off') {
            this.broadcastVoice({ ...vSession, channel: oldChannel }, {
              type: 'VOICE_LEAVE',
              playerId: vSession.playerId,
              channel: oldChannel,
              timestamp: Date.now()
            }, ws);
          }

          // If joining new channel, collect current peers and send roster
          const currentPeers: VoiceUserState[] = [];
          if (newChannel !== 'off') {
            for (const [otherWs, state] of this.voiceSockets.entries()) {
              if (otherWs !== ws && state.channel === newChannel) {
                if (newChannel === 'squad' && state.squadId === vSession.squadId) {
                  currentPeers.push(state);
                } else if (newChannel === 'match' && state.roomId === vSession.roomId) {
                  currentPeers.push(state);
                }
              }
            }

            // Broadcast join to new peers
            this.broadcastVoice(vSession, {
              type: 'VOICE_JOIN',
              userState: vSession,
              timestamp: Date.now()
            }, ws);
          }

          ws.send(JSON.stringify({
            type: 'VOICE_ROSTER',
            channel: newChannel,
            peers: currentPeers,
            timestamp: Date.now()
          }));
          return;
        }

        if (data.type === 'VOICE_STATE_UPDATE') {
          if (typeof data.isSpeaking === 'boolean') vSession.isSpeaking = data.isSpeaking;
          if (typeof data.isMuted === 'boolean') vSession.isMuted = data.isMuted;
          if (typeof data.isDeafened === 'boolean') vSession.isDeafened = data.isDeafened;
          if (typeof data.volumeLevel === 'number') vSession.volumeLevel = data.volumeLevel;

          this.broadcastVoice(vSession, {
            type: 'VOICE_STATE_SYNC',
            userState: vSession,
            timestamp: Date.now()
          }, ws);
          return;
        }

        // WebRTC SDP Offer / Answer / ICE Candidates Relay
        if (data.type === 'VOICE_SIGNAL') {
          const targetPeerId = data.targetPeerId as string;
          const signalPayload = {
            type: 'VOICE_SIGNAL',
            senderId: vSession.playerId,
            senderName: vSession.username,
            senderNumericId: vSession.numericId,
            signalType: data.signalType,
            sdp: data.sdp,
            candidate: data.candidate,
            timestamp: Date.now()
          };

          if (targetPeerId) {
            for (const [targetWs, pState] of this.voiceSockets.entries()) {
              if (pState.playerId === targetPeerId) {
                targetWs.send(JSON.stringify(signalPayload));
                break;
              }
            }
          } else {
            this.broadcastVoice(vSession, signalPayload, ws);
          }
          return;
        }

        // Low-Latency Audio Stream Frame Relay
        if (data.type === 'VOICE_FRAME') {
          // If sender is muted, do not forward audio
          if (vSession.isMuted) return;

          const audioPayload = {
            type: 'VOICE_FRAME',
            senderId: vSession.playerId,
            senderName: vSession.username,
            channel: vSession.channel,
            squadId: vSession.squadId,
            roomId: vSession.roomId,
            sequence: data.sequence || 0,
            audioBase64: data.audioBase64 || '',
            position: data.position,
            timestamp: Date.now()
          };

          const audioStr = JSON.stringify(audioPayload);
          for (const [otherWs, peerState] of this.voiceSockets.entries()) {
            if (otherWs === ws) continue;
            // Skip sending audio to deafened players
            if (peerState.isDeafened) continue;
            if (peerState.channel !== vSession.channel) continue;

            if (vSession.channel === 'squad') {
              if (peerState.squadId && peerState.squadId === vSession.squadId) {
                try { otherWs.send(audioStr); } catch { this.voiceSockets.delete(otherWs); }
              }
            } else if (vSession.channel === 'match') {
              if (peerState.roomId && peerState.roomId === vSession.roomId) {
                try { otherWs.send(audioStr); } catch { this.voiceSockets.delete(otherWs); }
              }
            }
          }
          return;
        }

        if (data.type === 'VOICE_REPORT') {
          const targetId = data.targetId as string;
          const reason = (data.reason as string) || 'Voice Harassment';
          const details = (data.details as string) || 'In-game voice abuse report';
          if (targetId) {
            this.ctx.storage.sql.exec(
              `INSERT INTO player_reports (reporter_id, target_id, reason, details, created_at) VALUES (?, ?, ?, ?, ?)`,
              vSession.playerId, targetId, reason, details, Date.now()
            );
            ws.send(JSON.stringify({ type: 'VOICE_REPORT_ACK', success: true, timestamp: Date.now() }));
          }
          return;
        }
      }

      // Chat WebSocket handling
      if (data.type === 'CHAT_SEND') {
        const session = this.chatSockets.get(ws);
        if (!session) return;
        const res = await this.processChatMessage(session, data.payload as SendChatRequest);
        if (!res.success) {
          ws.send(JSON.stringify({ type: 'CHAT_ERROR', error: res.error, timestamp: Date.now() }));
        }
      }
    } catch {
      // Ignore malformed packet
    }
  }

  async webSocketClose(ws: WebSocket): Promise<void> {
    this.chatSockets.delete(ws);
    const vSession = this.voiceSockets.get(ws);
    if (vSession) {
      this.voiceSockets.delete(ws);
      this.broadcastVoice(vSession, {
        type: 'VOICE_LEAVE',
        playerId: vSession.playerId,
        channel: vSession.channel,
        timestamp: Date.now()
      });
    }
  }

  async fetch(request: Request): Promise<Response> {
    const url = new URL(request.url);
    const path = url.pathname;

    try {
      // ---------------------------------------------------------
      // 0. REAL-TIME CHAT WEBSOCKET & REST ENDPOINTS
      // ---------------------------------------------------------

      // WebSocket Upgrade for Real-Time Chat
      if (path === '/chat/ws') {
        const upgradeHeader = request.headers.get('Upgrade');
        if (!upgradeHeader || upgradeHeader.toLowerCase() !== 'websocket') {
          return new Response('Expected Upgrade: websocket', { status: 426 });
        }

        const playerId = url.searchParams.get('playerId') || `usr_${crypto.randomUUID().substring(0, 8)}`;
        const username = url.searchParams.get('username') || 'Drifter';
        const numCursor = this.ctx.storage.sql.exec(`SELECT numeric_id FROM accounts WHERE id = ?`, playerId);
        let numericId = 0;
        for (const n of numCursor) { numericId = (n.numeric_id as number) || 0; break; }

        let clanId: string | undefined;
        let squadId: string | undefined;
        const cmCursor = this.ctx.storage.sql.exec(`SELECT clan_id FROM clan_members WHERE player_id = ?`, playerId);
        for (const cm of cmCursor) { clanId = cm.clan_id as string; break; }
        const smCursor = this.ctx.storage.sql.exec(`SELECT squad_id FROM squad_members WHERE player_id = ?`, playerId);
        for (const sm of smCursor) { squadId = sm.squad_id as string; break; }

        const pair = new WebSocketPair();
        const [client, server] = Object.values(pair);

        this.ctx.acceptWebSocket(server);
        this.chatSockets.set(server, { playerId, username, numericId, clanId, squadId });

        server.send(JSON.stringify({
          type: 'CHAT_CONNECTED',
          playerId,
          username,
          numericId,
          timestamp: Date.now()
        }));

        return new Response(null, { status: 101, webSocket: client });
      }

      // WebSocket Upgrade for Real-Time Voice Chat & WebRTC Signaling
      if (path === '/voice/ws') {
        const upgradeHeader = request.headers.get('Upgrade');
        if (!upgradeHeader || upgradeHeader.toLowerCase() !== 'websocket') {
          return new Response('Expected Upgrade: websocket', { status: 426 });
        }

        const playerId = url.searchParams.get('playerId') || `usr_${crypto.randomUUID().substring(0, 8)}`;
        const username = url.searchParams.get('username') || 'Drifter';
        const numCursor = this.ctx.storage.sql.exec(`SELECT numeric_id FROM accounts WHERE id = ?`, playerId);
        let numericId = 0;
        for (const n of numCursor) { numericId = (n.numeric_id as number) || 0; break; }

        let squadId = url.searchParams.get('squadId') || '';
        if (!squadId) {
          const smCursor = this.ctx.storage.sql.exec(`SELECT squad_id FROM squad_members WHERE player_id = ?`, playerId);
          for (const sm of smCursor) { squadId = sm.squad_id as string; break; }
        }

        const roomId = url.searchParams.get('roomId') || '';
        const initialChannel = (url.searchParams.get('channel') || (squadId ? 'squad' : (roomId ? 'match' : 'off'))) as VoiceChannel;

        const pair = new WebSocketPair();
        const [client, server] = Object.values(pair);

        this.ctx.acceptWebSocket(server);

        const initialUserState: VoiceUserState = {
          playerId,
          username,
          numericId,
          channel: initialChannel,
          isSpeaking: false,
          isMuted: false,
          isDeafened: false,
          volumeLevel: 0,
          squadId: squadId || undefined,
          roomId: roomId || undefined,
        };

        this.voiceSockets.set(server, initialUserState);

        server.send(JSON.stringify({
          type: 'VOICE_CONNECTED',
          userState: initialUserState,
          timestamp: Date.now()
        }));

        // Send current roster in same channel / squad
        const currentPeers: VoiceUserState[] = [];
        for (const [otherWs, state] of this.voiceSockets.entries()) {
          if (otherWs !== server && state.channel === initialChannel) {
            if (initialChannel === 'squad' && state.squadId === squadId) {
              currentPeers.push(state);
            } else if (initialChannel === 'match' && state.roomId === roomId) {
              currentPeers.push(state);
            }
          }
        }

        server.send(JSON.stringify({
          type: 'VOICE_ROSTER',
          channel: initialChannel,
          peers: currentPeers,
          timestamp: Date.now()
        }));

        // Broadcast VOICE_JOIN to other peers in channel
        this.broadcastVoice(initialUserState, {
          type: 'VOICE_JOIN',
          userState: initialUserState,
          timestamp: Date.now()
        }, server);

        return new Response(null, { status: 101, webSocket: client });
      }

      // REST: Voice Status & Roster
      if (path === '/voice/status' && request.method === 'GET') {
        const playerId = url.searchParams.get('playerId') || '';
        const channel = (url.searchParams.get('channel') || 'squad') as VoiceChannel;
        let squadId = url.searchParams.get('squadId') || '';
        if (!squadId && playerId) {
          const smCursor = this.ctx.storage.sql.exec(`SELECT squad_id FROM squad_members WHERE player_id = ?`, playerId);
          for (const sm of smCursor) { squadId = sm.squad_id as string; break; }
        }

        const peers: VoiceUserState[] = [];
        for (const [_, state] of this.voiceSockets.entries()) {
          if (state.channel === channel) {
            if (channel === 'squad' && state.squadId === squadId) {
              peers.push(state);
            } else if (channel === 'match') {
              peers.push(state);
            }
          }
        }

        return Response.json({
          success: true,
          channel,
          squadId,
          activeCount: peers.length,
          speakingCount: peers.filter(p => p.isSpeaking).length,
          peers
        });
      }

      // REST: Report Voice Abuse
      if (path === '/voice/report' && request.method === 'POST') {
        const body = (await request.json().catch(() => ({}))) as {
          targetId?: string;
          reason?: string;
          details?: string;
        };
        const reporterId = url.searchParams.get('playerId') || '';
        const targetId = body.targetId || '';
        const reason = body.reason || 'Voice Abuse';
        const details = body.details || '';

        if (!reporterId || !targetId) {
          return Response.json({ success: false, error: 'Missing parameters' }, { status: 400 });
        }

        this.ctx.storage.sql.exec(
          `INSERT INTO player_reports (reporter_id, target_id, reason, details, created_at)
           VALUES (?, ?, ?, ?, ?)`,
          reporterId, targetId, reason, details, Date.now()
        );

        return Response.json({ success: true, message: 'Voice abuse report submitted for review.' });
      }

      // REST: Send Chat Message
      if (path === '/chat/send' && request.method === 'POST') {
        const body = (await request.json().catch(() => ({}))) as SendChatRequest & { playerId?: string; username?: string };
        const playerId = body.playerId || url.searchParams.get('playerId') || '';
        if (!playerId) return Response.json({ success: false, error: 'Unauthorized' }, { status: 401 });

        const accCursor = this.ctx.storage.sql.exec(`SELECT username, numeric_id FROM accounts WHERE id = ?`, playerId);
        let uName = body.username || 'Drifter';
        let numId = 0;
        for (const a of accCursor) { uName = (a.username as string) || uName; numId = (a.numeric_id as number) || 0; break; }

        const res = await this.processChatMessage({ playerId, username: uName, numericId: numId }, body);
        if (!res.success) {
          return Response.json({ success: false, error: res.error }, { status: 400 });
        }
        return Response.json({ success: true, message: res.chatMessage });
      }

      // REST: Chat History
      if (path === '/chat/history' && request.method === 'GET') {
        const channel = (url.searchParams.get('channel') || 'global') as ChatChannel;
        const targetId = url.searchParams.get('targetId');
        const peerId = url.searchParams.get('peerId');
        const playerId = url.searchParams.get('playerId') || '';
        const limit = Math.min(Math.max(parseInt(url.searchParams.get('limit') || '50', 10), 1), 100);

        let cursor;
        if (channel === 'whisper') {
          let actualPeerId = peerId;
          const peerNumericId = url.searchParams.get('peerNumericId') || url.searchParams.get('targetNumericId');
          if (!actualPeerId && peerNumericId) {
            const pCursor = this.ctx.storage.sql.exec(`SELECT id FROM accounts WHERE numeric_id = ?`, parseInt(peerNumericId, 10));
            for (const p of pCursor) { actualPeerId = p.id as string; break; }
          }
          if (!playerId || !actualPeerId) return Response.json({ success: true, channel, count: 0, messages: [] });
          cursor = this.ctx.storage.sql.exec(
            `SELECT * FROM chat_messages
             WHERE channel = 'whisper'
               AND ((sender_id = ? AND recipient_id = ?) OR (sender_id = ? AND recipient_id = ?))
             ORDER BY timestamp DESC LIMIT ?`,
            playerId, actualPeerId, actualPeerId, playerId, limit
          );
        } else if (channel === 'squad') {
          let sqId = targetId;
          if (!sqId && playerId) {
            const sqCursor = this.ctx.storage.sql.exec(`SELECT squad_id FROM squad_members WHERE player_id = ?`, playerId);
            for (const s of sqCursor) { sqId = s.squad_id as string; break; }
          }
          if (!sqId) return Response.json({ success: true, channel, count: 0, messages: [] });
          cursor = this.ctx.storage.sql.exec(
            `SELECT * FROM chat_messages WHERE channel = 'squad' AND target_group_id = ? ORDER BY timestamp DESC LIMIT ?`,
            sqId, limit
          );
        } else if (channel === 'clan') {
          let clId = targetId;
          if (!clId && playerId) {
            const clCursor = this.ctx.storage.sql.exec(`SELECT clan_id FROM clan_members WHERE player_id = ?`, playerId);
            for (const c of clCursor) { clId = c.clan_id as string; break; }
          }
          if (!clId) return Response.json({ success: true, channel, count: 0, messages: [] });
          cursor = this.ctx.storage.sql.exec(
            `SELECT * FROM chat_messages WHERE channel = 'clan' AND target_group_id = ? ORDER BY timestamp DESC LIMIT ?`,
            clId, limit
          );
        } else if (targetId) {
          cursor = this.ctx.storage.sql.exec(
            `SELECT * FROM chat_messages WHERE channel = ? AND target_group_id = ? ORDER BY timestamp DESC LIMIT ?`,
            channel, targetId, limit
          );
        } else {
          cursor = this.ctx.storage.sql.exec(
            `SELECT * FROM chat_messages WHERE channel = ? ORDER BY timestamp DESC LIMIT ?`,
            channel, limit
          );
        }

        const messages: ChatMessage[] = [];
        for (const r of cursor) {
          messages.push({
            id: r.id as number,
            channel: r.channel as ChatChannel,
            senderId: r.sender_id as string,
            senderName: r.sender_name as string,
            senderNumericId: r.sender_numeric_id as number,
            clanTag: (r.clan_tag as string) || undefined,
            squadTag: (r.squad_tag as string) || undefined,
            recipientId: (r.recipient_id as string) || undefined,
            recipientName: (r.recipient_name as string) || undefined,
            targetGroupId: (r.target_group_id as string) || undefined,
            message: r.message as string,
            timestamp: r.timestamp as number,
          });
        }

        messages.reverse();
        return Response.json({ success: true, channel, count: messages.length, messages });
      }
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
        const body = (await request.json().catch(() => ({}))) as { playerId?: string; targetId?: string; targetNumericId?: number };
        const playerId = body.playerId || url.searchParams.get('playerId') || '';
        let targetId = body.targetId || '';
        if (!targetId && body.targetNumericId) {
          const tCursor = this.ctx.storage.sql.exec(`SELECT id FROM accounts WHERE numeric_id = ?`, body.targetNumericId);
          for (const t of tCursor) { targetId = t.id as string; break; }
        }
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
      // 4. FRIENDS SYSTEM (REQUESTS, PRESENCE & LAST LOGIN)
      // ---------------------------------------------------------

      // Presence Update
      if (path === '/presence/update' && request.method === 'POST') {
        const body = (await request.json().catch(() => ({}))) as { status?: string; matchRoomId?: string };
        const playerId = (body as any).playerId || url.searchParams.get('playerId') || '';
        const status = body.status || 'online';
        const matchRoomId = body.matchRoomId || null;
        if (!playerId) return Response.json({ success: false, error: 'Missing playerId' }, { status: 400 });

        this.ctx.storage.sql.exec(
          `INSERT OR REPLACE INTO player_presence (player_id, status, in_match_room, last_seen_at)
           VALUES (?, ?, ?, ?)`,
          playerId,
          status,
          matchRoomId,
          Date.now()
        );

        return Response.json({ success: true, status });
      }

      // Friends List with Online / In-Match / Offline & Last Login
      if (path === '/friends/list' && request.method === 'GET') {
        const playerId = url.searchParams.get('playerId');
        if (!playerId) return Response.json({ success: false, error: 'Missing playerId' }, { status: 400 });

        const cursor = this.ctx.storage.sql.exec(
          `SELECT f.friend_id, f.friend_username, f.created_at,
                  a.numeric_id, a.last_login_at,
                  p.status AS presence_status, p.in_match_room, p.last_seen_at,
                  sq.squad_id, sq.tag AS squad_tag,
                  cl.clan_id, cl.tag AS clan_tag
           FROM friends f
           LEFT JOIN accounts a ON a.id = f.friend_id
           LEFT JOIN player_presence p ON p.player_id = f.friend_id
           LEFT JOIN squad_members sm ON sm.player_id = f.friend_id
           LEFT JOIN squads sq ON sq.squad_id = sm.squad_id
           LEFT JOIN clan_members cm ON cm.player_id = f.friend_id
           LEFT JOIN clans cl ON cl.clan_id = cm.clan_id
           WHERE f.player_id = ?`,
          playerId
        );

        const now = Date.now();
        const friends: FriendDetailInfo[] = [];
        for (const row of cursor) {
          const lastSeen = (row.last_seen_at as number) || 0;
          const presenceStatus = (row.presence_status as string) || 'offline';
          const isRecentlyActive = (now - lastSeen) < 120000; // within 2 minutes

          let calculatedStatus: 'online' | 'in_match' | 'offline' = 'offline';
          if (isRecentlyActive && presenceStatus === 'in_match') {
            calculatedStatus = 'in_match';
          } else if (isRecentlyActive && presenceStatus !== 'offline') {
            calculatedStatus = 'online';
          }

          friends.push({
            playerId: row.friend_id as string,
            username: (row.friend_username as string) || 'Drifter',
            numericId: (row.numeric_id as number) || 0,
            isOnline: calculatedStatus !== 'offline',
            status: calculatedStatus,
            lastLoginAt: (row.last_login_at as number) || (row.created_at as number),
            addedAt: row.created_at as number,
            currentSquadId: (row.squad_id as string) || undefined,
            squadTag: (row.squad_tag as string) || undefined,
            clanTag: (row.clan_tag as string) || undefined,
          });
        }

        return Response.json({ success: true, count: friends.length, friends });
      }

      // Send Friend Request
      if (path === '/friends/request' && request.method === 'POST') {
        const body = (await request.json().catch(() => ({}))) as {
          playerId?: string;
          targetPlayerId?: string;
          targetNumericId?: number;
          targetUsername?: string;
        };
        const senderId = body.playerId || url.searchParams.get('playerId') || '';
        const targetId = body.targetPlayerId || '';
        const targetNum = body.targetNumericId || 0;
        const targetUser = body.targetUsername || '';

        if (!senderId) return Response.json({ success: false, error: 'Unauthorized' }, { status: 401 });

        // Find receiver account
        let targetCursor;
        if (targetId) {
          targetCursor = this.ctx.storage.sql.exec(`SELECT * FROM accounts WHERE id = ?`, targetId);
        } else if (targetNum > 0) {
          targetCursor = this.ctx.storage.sql.exec(`SELECT * FROM accounts WHERE numeric_id = ?`, targetNum);
        } else if (targetUser) {
          targetCursor = this.ctx.storage.sql.exec(`SELECT * FROM accounts WHERE username = ? COLLATE NOCASE`, targetUser);
        } else {
          return Response.json({ success: false, error: 'Target player required' }, { status: 400 });
        }

        let receiver: Record<string, unknown> | null = null;
        for (const r of targetCursor) { receiver = r; break; }
        if (!receiver) return Response.json({ success: false, error: 'Target player not found' }, { status: 404 });
        if (receiver.id === senderId) return Response.json({ success: false, error: 'Cannot add yourself as friend' }, { status: 400 });

        // Check privacy & blocklist
        const blockCursor = this.ctx.storage.sql.exec(`SELECT * FROM blocked_players WHERE player_id = ? AND blocked_id = ?`, receiver.id, senderId);
        for (const _ of blockCursor) {
          return Response.json({ success: false, error: 'Cannot send friend request to this player.' }, { status: 403 });
        }

        const privCursor = this.ctx.storage.sql.exec(`SELECT allow_invites FROM privacy_settings WHERE player_id = ?`, receiver.id);
        for (const p of privCursor) {
          if ((p.allow_invites as number) === 2) { // Nobody
            return Response.json({ success: false, error: 'Player does not accept friend requests.' }, { status: 403 });
          }
        }

        // Get sender details
        const senderCursor = this.ctx.storage.sql.exec(`SELECT username, numeric_id FROM accounts WHERE id = ?`, senderId);
        let senderAcc: Record<string, unknown> | null = null;
        for (const s of senderCursor) { senderAcc = s; break; }
        const senderName = (senderAcc?.username as string) || url.searchParams.get('username') || 'Drifter';
        const senderNumericId = (senderAcc?.numeric_id as number) || 0;

        // Check already friends
        const friendCheck = this.ctx.storage.sql.exec(`SELECT * FROM friends WHERE player_id = ? AND friend_id = ?`, senderId, receiver.id);
        for (const _ of friendCheck) {
          return Response.json({ success: false, error: 'Already friends with this player.' }, { status: 409 });
        }

        this.ctx.storage.sql.exec(
          `INSERT INTO friend_requests (sender_id, sender_name, sender_numeric_id, receiver_id, receiver_name, status, created_at)
           VALUES (?, ?, ?, ?, ?, 'pending', ?)`,
          senderId,
          senderName,
          senderNumericId,
          receiver.id as string,
          receiver.username as string,
          Date.now()
        );

        return Response.json({ success: true, message: `Friend request sent to ${receiver.username}.` });
      }

      // Incoming & Outgoing Friend Requests
      if (path === '/friends/requests' && request.method === 'GET') {
        const playerId = url.searchParams.get('playerId');
        if (!playerId) return Response.json({ success: false, error: 'Missing playerId' }, { status: 400 });

        const incomingCursor = this.ctx.storage.sql.exec(
          `SELECT id, sender_id, sender_name, sender_numeric_id, receiver_id, receiver_name, status, created_at
           FROM friend_requests
           WHERE receiver_id = ? AND status = 'pending'
           ORDER BY created_at DESC`,
          playerId
        );

        const incoming: FriendRequestInfo[] = [];
        for (const row of incomingCursor) {
          incoming.push({
            id: row.id as number,
            senderId: row.sender_id as string,
            senderName: row.sender_name as string,
            senderNumericId: row.sender_numeric_id as number,
            receiverId: row.receiver_id as string,
            receiverName: row.receiver_name as string,
            status: row.status as 'pending',
            createdAt: row.created_at as number,
          });
        }

        return Response.json({ success: true, incoming });
      }

      // Respond to Friend Request (Accept / Reject)
      if (path === '/friends/respond' && request.method === 'POST') {
        const body = (await request.json().catch(() => ({}))) as {
          playerId?: string;
          requestId?: number;
          action?: 'accept' | 'reject';
        };
        const playerId = body.playerId || url.searchParams.get('playerId') || '';
        const requestId = body.requestId;
        const action = body.action || 'accept';

        if (!playerId || !requestId) return Response.json({ success: false, error: 'Invalid parameters' }, { status: 400 });

        const reqCursor = this.ctx.storage.sql.exec(
          `SELECT * FROM friend_requests WHERE id = ? AND receiver_id = ? AND status = 'pending'`,
          requestId,
          playerId
        );
        let reqRow: Record<string, unknown> | null = null;
        for (const r of reqCursor) { reqRow = r; break; }
        if (!reqRow) return Response.json({ success: false, error: 'Friend request not found or resolved' }, { status: 404 });

        if (action === 'accept') {
          const now = Date.now();
          this.ctx.storage.sql.exec(
            `INSERT OR REPLACE INTO friends (player_id, friend_id, friend_username, created_at) VALUES (?, ?, ?, ?)`,
            reqRow.receiver_id as string,
            reqRow.sender_id as string,
            reqRow.sender_name as string,
            now
          );
          this.ctx.storage.sql.exec(
            `INSERT OR REPLACE INTO friends (player_id, friend_id, friend_username, created_at) VALUES (?, ?, ?, ?)`,
            reqRow.sender_id as string,
            reqRow.receiver_id as string,
            reqRow.receiver_name as string,
            now
          );
          this.ctx.storage.sql.exec(`UPDATE friend_requests SET status = 'accepted' WHERE id = ?`, requestId);
          return Response.json({ success: true, message: 'Friend request accepted.' });
        } else {
          this.ctx.storage.sql.exec(`UPDATE friend_requests SET status = 'rejected' WHERE id = ?`, requestId);
          return Response.json({ success: true, message: 'Friend request rejected.' });
        }
      }

      // Remove Friend
      if (path === '/friends/remove' && request.method === 'POST') {
        const body = (await request.json().catch(() => ({}))) as { playerId?: string; friendId?: string };
        const playerId = body.playerId || url.searchParams.get('playerId') || '';
        const friendId = body.friendId || '';
        if (!playerId || !friendId) return Response.json({ success: false, error: 'Invalid parameters' }, { status: 400 });

        this.ctx.storage.sql.exec(`DELETE FROM friends WHERE player_id = ? AND friend_id = ?`, playerId, friendId);
        this.ctx.storage.sql.exec(`DELETE FROM friends WHERE player_id = ? AND friend_id = ?`, friendId, playerId);
        return Response.json({ success: true, message: 'Friend removed.' });
      }

      // Legacy Add Friend backward compatibility
      if (path === '/friends/add' && request.method === 'POST') {
        const body = (await request.json().catch(() => ({}))) as { playerId?: string; friendId?: string; friendUsername?: string };
        const playerId = body.playerId || url.searchParams.get('playerId') || '';
        const friendId = body.friendId || '';
        const friendUsername = body.friendUsername || 'AetherDrifter';
        if (!playerId || !friendId) return Response.json({ success: false, error: 'Invalid parameters' }, { status: 400 });

        this.ctx.storage.sql.exec(
          `INSERT OR REPLACE INTO friends (player_id, friend_id, friend_username, created_at) VALUES (?, ?, ?, ?)`,
          playerId, friendId, friendUsername, Date.now()
        );
        return Response.json({ success: true });
      }

      // ---------------------------------------------------------
      // 5. FREE FIRE SQUAD SYSTEM (4-5 PLAYERS LOBBY & CHAT)
      // ---------------------------------------------------------

      // Create Squad
      if (path === '/squad/create' && request.method === 'POST') {
        const body = (await request.json().catch(() => ({}))) as {
          playerId?: string;
          username?: string;
          name?: string;
          tag?: string;
        };
        const playerId = body.playerId || url.searchParams.get('playerId') || '';
        const username = body.username || url.searchParams.get('username') || 'AetherLeader';
        const name = filterProfanity((body.name || `${username}'s Squad`).substring(0, 24));
        const tag = (body.tag || 'AES').trim().toUpperCase().replace(/[^A-Z0-9]/g, '').substring(0, 5);

        if (!playerId) return Response.json({ success: false, error: 'Unauthorized' }, { status: 401 });

        // Leave existing squad
        this.ctx.storage.sql.exec(`DELETE FROM squad_members WHERE player_id = ?`, playerId);

        const squadId = `SQD_${crypto.randomUUID().substring(0, 6).toUpperCase()}`;
        const now = Date.now();

        // Fetch sender numeric ID
        const accCursor = this.ctx.storage.sql.exec(`SELECT numeric_id FROM accounts WHERE id = ?`, playerId);
        let numericId = 0;
        for (const a of accCursor) { numericId = (a.numeric_id as number) || 0; break; }

        this.ctx.storage.sql.exec(
          `INSERT INTO squads (squad_id, name, tag, leader_id, leader_name, max_members, is_open, squad_xp, squad_level, created_at)
           VALUES (?, ?, ?, ?, ?, 5, 1, 0, 1, ?)`,
          squadId, name, tag, playerId, username, now
        );

        this.ctx.storage.sql.exec(
          `INSERT INTO squad_members (squad_id, player_id, username, numeric_id, role, is_ready, joined_at)
           VALUES (?, ?, ?, ?, 'leader', 1, ?)`,
          squadId, playerId, username, numericId, now
        );

        return Response.json({
          success: true,
          squad: {
            squadId,
            name,
            tag,
            leaderId: playerId,
            leaderName: username,
            maxMembers: 5,
            isOpen: true,
            squadXp: 0,
            squadLevel: 1,
            members: [{
              playerId,
              username,
              numericId,
              role: 'leader',
              isReady: true,
              joinedAt: now
            }],
            createdAt: now
          }
        });
      }

      // Get My Current Squad
      if (path === '/squad/my' && request.method === 'GET') {
        const playerId = url.searchParams.get('playerId');
        if (!playerId) return Response.json({ success: false, error: 'Missing playerId' }, { status: 400 });

        const memCursor = this.ctx.storage.sql.exec(`SELECT squad_id FROM squad_members WHERE player_id = ?`, playerId);
        let currentSquadId: string | null = null;
        for (const m of memCursor) { currentSquadId = m.squad_id as string; break; }

        if (!currentSquadId) {
          return Response.json({ success: true, hasSquad: false, squad: null });
        }

        const squadCursor = this.ctx.storage.sql.exec(`SELECT * FROM squads WHERE squad_id = ?`, currentSquadId);
        let sRow: Record<string, unknown> | null = null;
        for (const s of squadCursor) { sRow = s; break; }
        if (!sRow) return Response.json({ success: true, hasSquad: false, squad: null });

        const membersCursor = this.ctx.storage.sql.exec(
          `SELECT player_id, username, numeric_id, role, is_ready, joined_at FROM squad_members WHERE squad_id = ? ORDER BY role DESC, joined_at ASC`,
          currentSquadId
        );
        const members: SquadMemberInfo[] = [];
        for (const row of membersCursor) {
          members.push({
            playerId: row.player_id as string,
            username: row.username as string,
            numericId: row.numeric_id as number,
            role: row.role as 'leader' | 'member',
            isReady: (row.is_ready as number) === 1,
            joinedAt: row.joined_at as number,
          });
        }

        const squad: SquadInfo = {
          squadId: sRow.squad_id as string,
          name: sRow.name as string,
          tag: sRow.tag as string,
          leaderId: sRow.leader_id as string,
          leaderName: sRow.leader_name as string,
          maxMembers: (sRow.max_members as number) || 5,
          isOpen: (sRow.is_open as number) === 1,
          squadXp: (sRow.squad_xp as number) || 0,
          squadLevel: (sRow.squad_level as number) || 1,
          matchRoomId: (sRow.match_room_id as string) || undefined,
          members,
          createdAt: sRow.created_at as number,
        };

        return Response.json({ success: true, hasSquad: true, squad });
      }

      // Quick Invite to Squad (from Friends list or ID)
      if (path === '/squad/invite' && request.method === 'POST') {
        const body = (await request.json().catch(() => ({}))) as {
          playerId?: string;
          targetPlayerId?: string;
        };
        const playerId = body.playerId || url.searchParams.get('playerId') || '';
        const targetId = body.targetPlayerId || '';

        if (!playerId || !targetId) return Response.json({ success: false, error: 'Missing parameters' }, { status: 400 });

        // Find current squad
        const memCursor = this.ctx.storage.sql.exec(`SELECT squad_id FROM squad_members WHERE player_id = ?`, playerId);
        let squadId: string | null = null;
        for (const m of memCursor) { squadId = m.squad_id as string; break; }
        if (!squadId) return Response.json({ success: false, error: 'You are not in a squad' }, { status: 400 });

        const squadCursor = this.ctx.storage.sql.exec(`SELECT name, tag FROM squads WHERE squad_id = ?`, squadId);
        let sRow: Record<string, unknown> | null = null;
        for (const s of squadCursor) { sRow = s; break; }

        const senderCursor = this.ctx.storage.sql.exec(`SELECT username FROM accounts WHERE id = ?`, playerId);
        let senderName = 'SquadLeader';
        for (const s of senderCursor) { senderName = s.username as string; break; }

        this.ctx.storage.sql.exec(
          `INSERT INTO squad_invites (squad_id, squad_name, squad_tag, inviter_id, inviter_name, target_id, status, created_at)
           VALUES (?, ?, ?, ?, ?, ?, 'pending', ?)`,
          squadId,
          (sRow?.name as string) || 'Squad',
          (sRow?.tag as string) || 'AES',
          playerId,
          senderName,
          targetId,
          Date.now()
        );

        return Response.json({ success: true, message: 'Squad invite delivered!' });
      }

      // Get Incoming Squad Invites
      if (path === '/squad/invites' && request.method === 'GET') {
        const playerId = url.searchParams.get('playerId');
        if (!playerId) return Response.json({ success: false, error: 'Missing playerId' }, { status: 400 });

        const cursor = this.ctx.storage.sql.exec(
          `SELECT id, squad_id, squad_name, squad_tag, inviter_id, inviter_name, status, created_at
           FROM squad_invites WHERE target_id = ? AND status = 'pending' ORDER BY created_at DESC LIMIT 10`,
          playerId
        );

        const invites: SquadInviteInfo[] = [];
        for (const row of cursor) {
          invites.push({
            id: row.id as number,
            squadId: row.squad_id as string,
            squadName: row.squad_name as string,
            squadTag: row.squad_tag as string,
            inviterId: row.inviter_id as string,
            inviterName: row.inviter_name as string,
            status: row.status as 'pending',
            createdAt: row.created_at as number,
          });
        }

        return Response.json({ success: true, invites });
      }

      // Respond to Squad Invite (Accept / Reject)
      if (path === '/squad/respond-invite' && request.method === 'POST') {
        const body = (await request.json().catch(() => ({}))) as {
          playerId?: string;
          inviteId?: number;
          action?: 'accept' | 'reject';
        };
        const playerId = body.playerId || url.searchParams.get('playerId') || '';
        const inviteId = body.inviteId;
        const action = body.action || 'accept';

        if (!playerId || !inviteId) return Response.json({ success: false, error: 'Invalid parameters' }, { status: 400 });

        const invCursor = this.ctx.storage.sql.exec(`SELECT * FROM squad_invites WHERE id = ? AND target_id = ? AND status = 'pending'`, inviteId, playerId);
        let invRow: Record<string, unknown> | null = null;
        for (const i of invCursor) { invRow = i; break; }
        if (!invRow) return Response.json({ success: false, error: 'Invite not found or expired' }, { status: 404 });

        if (action === 'accept') {
          const squadId = invRow.squad_id as string;
          // Check squad count
          const countCursor = this.ctx.storage.sql.exec(`SELECT COUNT(*) AS count FROM squad_members WHERE squad_id = ?`, squadId);
          let count = 0;
          for (const c of countCursor) { count = c.count as number; break; }
          if (count >= 5) return Response.json({ success: false, error: 'Squad lobby is already full (max 5 players).' }, { status: 409 });

          // Leave existing squad
          this.ctx.storage.sql.exec(`DELETE FROM squad_members WHERE player_id = ?`, playerId);

          // Get player details
          const accCursor = this.ctx.storage.sql.exec(`SELECT username, numeric_id FROM accounts WHERE id = ?`, playerId);
          let uName = 'Drifter';
          let numId = 0;
          for (const a of accCursor) { uName = a.username as string; numId = a.numeric_id as number; break; }

          this.ctx.storage.sql.exec(
            `INSERT INTO squad_members (squad_id, player_id, username, numeric_id, role, is_ready, joined_at)
             VALUES (?, ?, ?, ?, 'member', 0, ?)`,
            squadId, playerId, uName, numId, Date.now()
          );

          this.ctx.storage.sql.exec(`UPDATE squad_invites SET status = 'accepted' WHERE id = ?`, inviteId);
          return Response.json({ success: true, squadId });
        } else {
          this.ctx.storage.sql.exec(`UPDATE squad_invites SET status = 'rejected' WHERE id = ?`, inviteId);
          return Response.json({ success: true, message: 'Squad invite rejected.' });
        }
      }

      // Ready System in Squad Lobby
      if (path === '/squad/ready' && request.method === 'POST') {
        const body = (await request.json().catch(() => ({}))) as { playerId?: string; isReady?: boolean };
        const playerId = body.playerId || url.searchParams.get('playerId') || '';
        const isReady = body.isReady ? 1 : 0;
        if (!playerId) return Response.json({ success: false, error: 'Unauthorized' }, { status: 401 });

        this.ctx.storage.sql.exec(`UPDATE squad_members SET is_ready = ? WHERE player_id = ?`, isReady, playerId);
        return Response.json({ success: true, isReady: isReady === 1 });
      }

      // Direct Join Squad
      if (path === '/squad/join' && request.method === 'POST') {
        const body = (await request.json().catch(() => ({}))) as { playerId?: string; squadId?: string };
        const playerId = body.playerId || url.searchParams.get('playerId') || '';
        const squadId = body.squadId || '';

        if (!playerId || !squadId) return Response.json({ success: false, error: 'Invalid parameters' }, { status: 400 });

        const sCursor = this.ctx.storage.sql.exec(`SELECT * FROM squads WHERE squad_id = ?`, squadId);
        let sRow: Record<string, unknown> | null = null;
        for (const s of sCursor) { sRow = s; break; }
        if (!sRow) return Response.json({ success: false, error: 'Squad not found' }, { status: 404 });

        const countCursor = this.ctx.storage.sql.exec(`SELECT COUNT(*) AS count FROM squad_members WHERE squad_id = ?`, squadId);
        let count = 0;
        for (const c of countCursor) { count = c.count as number; break; }
        if (count >= 5) return Response.json({ success: false, error: 'Squad is full' }, { status: 409 });

        this.ctx.storage.sql.exec(`DELETE FROM squad_members WHERE player_id = ?`, playerId);

        const accCursor = this.ctx.storage.sql.exec(`SELECT username, numeric_id FROM accounts WHERE id = ?`, playerId);
        let uName = 'Drifter';
        let numId = 0;
        for (const a of accCursor) { uName = a.username as string; numId = a.numeric_id as number; break; }

        this.ctx.storage.sql.exec(
          `INSERT INTO squad_members (squad_id, player_id, username, numeric_id, role, is_ready, joined_at)
           VALUES (?, ?, ?, ?, 'member', 0, ?)`,
          squadId, playerId, uName, numId, Date.now()
        );

        return Response.json({ success: true, squadId });
      }

      // Leave Squad
      if (path === '/squad/leave' && request.method === 'POST') {
        const body = (await request.json().catch(() => ({}))) as { playerId?: string };
        const playerId = body.playerId || url.searchParams.get('playerId') || '';
        if (!playerId) return Response.json({ success: false, error: 'Unauthorized' }, { status: 401 });

        const memCursor = this.ctx.storage.sql.exec(`SELECT squad_id, role FROM squad_members WHERE player_id = ?`, playerId);
        let squadId: string | null = null;
        let role = 'member';
        for (const m of memCursor) { squadId = m.squad_id as string; role = m.role as string; break; }

        if (squadId) {
          this.ctx.storage.sql.exec(`DELETE FROM squad_members WHERE squad_id = ? AND player_id = ?`, squadId, playerId);
          if (role === 'leader') {
            // Promote next member
            const nextCursor = this.ctx.storage.sql.exec(`SELECT player_id, username FROM squad_members WHERE squad_id = ? LIMIT 1`, squadId);
            let nextPlayer: Record<string, unknown> | null = null;
            for (const n of nextCursor) { nextPlayer = n; break; }

            if (nextPlayer) {
              this.ctx.storage.sql.exec(`UPDATE squad_members SET role = 'leader', is_ready = 1 WHERE squad_id = ? AND player_id = ?`, squadId, nextPlayer.player_id as string);
              this.ctx.storage.sql.exec(`UPDATE squads SET leader_id = ?, leader_name = ? WHERE squad_id = ?`, nextPlayer.player_id as string, nextPlayer.username as string, squadId);
            } else {
              this.ctx.storage.sql.exec(`DELETE FROM squads WHERE squad_id = ?`, squadId);
            }
          }
        }

        return Response.json({ success: true, message: 'Left squad lobby.' });
      }

      // Squad Leader Kick Member
      if (path === '/squad/kick' && request.method === 'POST') {
        const body = (await request.json().catch(() => ({}))) as { playerId?: string; targetPlayerId?: string };
        const playerId = body.playerId || url.searchParams.get('playerId') || '';
        const targetId = body.targetPlayerId || '';

        const memCursor = this.ctx.storage.sql.exec(`SELECT squad_id, role FROM squad_members WHERE player_id = ?`, playerId);
        let squadId: string | null = null;
        let role = 'member';
        for (const m of memCursor) { squadId = m.squad_id as string; role = m.role as string; break; }
        if (role !== 'leader') return Response.json({ success: false, error: 'Only squad leader can kick members.' }, { status: 403 });

        this.ctx.storage.sql.exec(`DELETE FROM squad_members WHERE squad_id = ? AND player_id = ?`, squadId, targetId);
        return Response.json({ success: true, message: 'Member removed from squad.' });
      }

      // Squad Leader Promote Member
      if (path === '/squad/promote' && request.method === 'POST') {
        const body = (await request.json().catch(() => ({}))) as { playerId?: string; targetPlayerId?: string };
        const playerId = body.playerId || url.searchParams.get('playerId') || '';
        const targetId = body.targetPlayerId || '';

        const memCursor = this.ctx.storage.sql.exec(`SELECT squad_id, role FROM squad_members WHERE player_id = ?`, playerId);
        let squadId: string | null = null;
        let role = 'member';
        for (const m of memCursor) { squadId = m.squad_id as string; role = m.role as string; break; }
        if (role !== 'leader') return Response.json({ success: false, error: 'Only squad leader can promote members.' }, { status: 403 });

        const targetAcc = this.ctx.storage.sql.exec(`SELECT username FROM accounts WHERE id = ?`, targetId);
        let targetName = 'Leader';
        for (const t of targetAcc) { targetName = t.username as string; break; }

        this.ctx.storage.sql.exec(`UPDATE squad_members SET role = 'member' WHERE squad_id = ? AND player_id = ?`, squadId, playerId);
        this.ctx.storage.sql.exec(`UPDATE squad_members SET role = 'leader' WHERE squad_id = ? AND player_id = ?`, squadId, targetId);
        this.ctx.storage.sql.exec(`UPDATE squads SET leader_id = ?, leader_name = ? WHERE squad_id = ?`, targetId, targetName, squadId);

        return Response.json({ success: true, message: 'Squad leadership transferred.' });
      }

      // Squad Leader Settings
      if (path === '/squad/settings' && request.method === 'POST') {
        const body = (await request.json().catch(() => ({}))) as { playerId?: string; name?: string; tag?: string; isOpen?: boolean };
        const playerId = body.playerId || url.searchParams.get('playerId') || '';

        const memCursor = this.ctx.storage.sql.exec(`SELECT squad_id, role FROM squad_members WHERE player_id = ?`, playerId);
        let squadId: string | null = null;
        let role = 'member';
        for (const m of memCursor) { squadId = m.squad_id as string; role = m.role as string; break; }
        if (role !== 'leader') return Response.json({ success: false, error: 'Only squad leader can change settings.' }, { status: 403 });

        const name = body.name ? filterProfanity(body.name.substring(0, 24)) : null;
        const tag = body.tag ? body.tag.trim().toUpperCase().replace(/[^A-Z0-9]/g, '').substring(0, 5) : null;
        const isOpen = body.isOpen !== undefined ? (body.isOpen ? 1 : 0) : null;

        this.ctx.storage.sql.exec(
          `UPDATE squads
           SET name = COALESCE(?, name),
               tag = COALESCE(?, tag),
               is_open = COALESCE(?, is_open)
           WHERE squad_id = ?`,
          name, tag, isOpen, squadId
        );

        return Response.json({ success: true, message: 'Squad settings updated.' });
      }

      // Transition from Squad Lobby to Match!
      if (path === '/squad/start-match' && request.method === 'POST') {
        const body = (await request.json().catch(() => ({}))) as { playerId?: string; mode?: string };
        const playerId = body.playerId || url.searchParams.get('playerId') || '';

        const memCursor = this.ctx.storage.sql.exec(`SELECT squad_id, role FROM squad_members WHERE player_id = ?`, playerId);
        let squadId: string | null = null;
        let role = 'member';
        for (const m of memCursor) { squadId = m.squad_id as string; role = m.role as string; break; }
        if (role !== 'leader') return Response.json({ success: false, error: 'Only squad leader can launch match.' }, { status: 403 });

        // Verify all members ready
        const unreadyCursor = this.ctx.storage.sql.exec(`SELECT username FROM squad_members WHERE squad_id = ? AND is_ready = 0`, squadId);
        let unreadyName: string | null = null;
        for (const u of unreadyCursor) { unreadyName = u.username as string; break; }
        if (unreadyName) {
          return Response.json({ success: false, error: `Waiting for ${unreadyName} to click READY!` }, { status: 400 });
        }

        // Generate squad match room ID
        const matchRoomId = `ROOM_SQD_${crypto.randomUUID().substring(0, 6).toUpperCase()}`;

        // Reward squad XP (+120 XP) and level up
        this.ctx.storage.sql.exec(
          `UPDATE squads
           SET match_room_id = ?,
               squad_xp = squad_xp + 120,
               squad_level = 1 + ((squad_xp + 120) / 1000)
           WHERE squad_id = ?`,
          matchRoomId,
          squadId
        );

        // Update player presence for all squad members to in_match
        this.ctx.storage.sql.exec(
          `UPDATE player_presence
           SET status = 'in_match', in_match_room = ?, last_seen_at = ?
           WHERE player_id IN (SELECT player_id FROM squad_members WHERE squad_id = ?)`,
          matchRoomId, Date.now(), squadId
        );

        return Response.json({
          success: true,
          matchRoomId,
          wsUrl: `/ws/room/${matchRoomId}?role=squad`,
          message: 'Match launched! Transitioning squad to gameplay.'
        });
      }

      // Squad Chat
      if (path === '/squad/chat' && request.method === 'POST') {
        const body = (await request.json().catch(() => ({}))) as { playerId?: string; message?: string };
        const playerId = body.playerId || url.searchParams.get('playerId') || '';
        const msg = filterProfanity((body.message || '').substring(0, 150));
        if (!playerId || !msg.trim()) return Response.json({ success: false, error: 'Empty message' }, { status: 400 });

        const memCursor = this.ctx.storage.sql.exec(`SELECT squad_id, username FROM squad_members WHERE player_id = ?`, playerId);
        let squadId: string | null = null;
        let uName = 'Drifter';
        for (const m of memCursor) { squadId = m.squad_id as string; uName = m.username as string; break; }
        if (!squadId) return Response.json({ success: false, error: 'Not in a squad' }, { status: 400 });

        const now = Date.now();
        this.ctx.storage.sql.exec(
          `INSERT INTO squad_chat (squad_id, sender_id, sender_name, message, sent_at) VALUES (?, ?, ?, ?, ?)`,
          squadId, playerId, uName, msg, now
        );

        return Response.json({ success: true, sentAt: now });
      }

      if (path === '/squad/chat' && request.method === 'GET') {
        const playerId = url.searchParams.get('playerId');
        if (!playerId) return Response.json({ success: false, error: 'Missing playerId' }, { status: 400 });

        const memCursor = this.ctx.storage.sql.exec(`SELECT squad_id FROM squad_members WHERE player_id = ?`, playerId);
        let squadId: string | null = null;
        for (const m of memCursor) { squadId = m.squad_id as string; break; }
        if (!squadId) return Response.json({ success: true, messages: [] });

        const chatCursor = this.ctx.storage.sql.exec(
          `SELECT id, squad_id, sender_id, sender_name, message, sent_at
           FROM squad_chat WHERE squad_id = ? ORDER BY sent_at DESC LIMIT 30`,
          squadId
        );

        const messages: SquadChatMessage[] = [];
        for (const row of chatCursor) {
          messages.push({
            id: row.id as number,
            squadId: row.squad_id as string,
            senderId: row.sender_id as string,
            senderName: row.sender_name as string,
            message: row.message as string,
            sentAt: row.sent_at as number,
          });
        }

        messages.reverse();
        return Response.json({ success: true, messages });
      }

      // ---------------------------------------------------------
      // 6. FREE FIRE CLAN / GUILD SYSTEM (UP TO 50 PLAYERS)
      // ---------------------------------------------------------

      // Create Clan
      if (path === '/clan/create' && request.method === 'POST') {
        const body = (await request.json().catch(() => ({}))) as {
          playerId?: string;
          username?: string;
          name?: string;
          tag?: string;
          description?: string;
          badgeId?: number;
          minLevel?: number;
          isOpen?: boolean;
        };
        const playerId = body.playerId || url.searchParams.get('playerId') || '';
        const username = body.username || url.searchParams.get('username') || 'AetherLeader';
        const name = filterProfanity((body.name || '').trim().substring(0, 24));
        const tag = (body.tag || '').trim().toUpperCase().replace(/[^A-Z0-9]/g, '').substring(0, 5);

        if (!name || name.length < 3) return Response.json({ success: false, error: 'Clan name must be at least 3 characters.' }, { status: 400 });
        if (!tag || tag.length < 2) return Response.json({ success: false, error: 'Clan tag must be 2-5 characters.' }, { status: 400 });

        // Check unique clan name
        const existCursor = this.ctx.storage.sql.exec(`SELECT clan_id FROM clans WHERE name = ? COLLATE NOCASE`, name);
        for (const _ of existCursor) {
          return Response.json({ success: false, error: 'A clan with this name already exists.' }, { status: 409 });
        }

        // Leave existing clan
        this.ctx.storage.sql.exec(`DELETE FROM clan_members WHERE player_id = ?`, playerId);

        const clanId = `CLN_${crypto.randomUUID().substring(0, 6).toUpperCase()}`;
        const now = Date.now();
        const desc = filterProfanity((body.description || 'United under the celestial serpent.').substring(0, 150));
        const badge = body.badgeId || 1;
        const minLvl = body.minLevel || 1;
        const isOpen = body.isOpen !== false ? 1 : 0;

        const accCursor = this.ctx.storage.sql.exec(`SELECT numeric_id FROM accounts WHERE id = ?`, playerId);
        let numericId = 0;
        for (const a of accCursor) { numericId = (a.numeric_id as number) || 0; break; }

        this.ctx.storage.sql.exec(
          `INSERT INTO clans (clan_id, name, tag, leader_id, leader_name, description, announcement, badge_id, max_members, min_level, is_open, clan_xp, clan_level, created_at)
           VALUES (?, ?, ?, ?, ?, ?, 'Welcome to our celestial guild! Compete in matches to gain Clan XP.', ?, 50, ?, ?, 0, 1, ?)`,
          clanId, name, tag, playerId, username, desc, badge, minLvl, isOpen, now
        );

        this.ctx.storage.sql.exec(
          `INSERT INTO clan_members (clan_id, player_id, username, numeric_id, rank, contribution, joined_at)
           VALUES (?, ?, ?, ?, 'leader', 500, ?)`,
          clanId, playerId, username, numericId, now
        );

        return Response.json({
          success: true,
          clan: {
            clanId,
            name,
            tag,
            leaderId: playerId,
            leaderName: username,
            description: desc,
            announcement: 'Welcome to our celestial guild! Compete in matches to gain Clan XP.',
            badgeId: badge,
            memberCount: 1,
            maxMembers: 50,
            minLevel: minLvl,
            isOpen: isOpen === 1,
            clanXp: 0,
            clanLevel: 1,
            createdAt: now,
          }
        });
      }

      // Get My Current Clan
      if (path === '/clan/my' && request.method === 'GET') {
        const playerId = url.searchParams.get('playerId');
        if (!playerId) return Response.json({ success: false, error: 'Missing playerId' }, { status: 400 });

        const memCursor = this.ctx.storage.sql.exec(`SELECT clan_id, rank FROM clan_members WHERE player_id = ?`, playerId);
        let clanId: string | null = null;
        let myRank = 'member';
        for (const m of memCursor) { clanId = m.clan_id as string; myRank = m.rank as string; break; }
        if (!clanId) return Response.json({ success: true, hasClan: false, clan: null });

        const clanCursor = this.ctx.storage.sql.exec(`SELECT * FROM clans WHERE clan_id = ?`, clanId);
        let cRow: Record<string, unknown> | null = null;
        for (const c of clanCursor) { cRow = c; break; }
        if (!cRow) return Response.json({ success: true, hasClan: false, clan: null });

        const membersCursor = this.ctx.storage.sql.exec(
          `SELECT cm.player_id, cm.username, cm.numeric_id, cm.rank, cm.contribution, cm.joined_at,
                  p.status AS presence_status, p.last_seen_at
           FROM clan_members cm
           LEFT JOIN player_presence p ON p.player_id = cm.player_id
           WHERE cm.clan_id = ?
           ORDER BY CASE cm.rank WHEN 'leader' THEN 1 WHEN 'officer' THEN 2 ELSE 3 END, cm.contribution DESC`,
          clanId
        );

        const now = Date.now();
        let onlineCount = 0;
        const members: ClanMemberInfo[] = [];

        for (const row of membersCursor) {
          const lastSeen = (row.last_seen_at as number) || 0;
          const status = (row.presence_status as string) || 'offline';
          const isOnline = (now - lastSeen < 120000) && status !== 'offline';
          if (isOnline) onlineCount++;

          members.push({
            playerId: row.player_id as string,
            username: row.username as string,
            numericId: row.numeric_id as number,
            rank: row.rank as 'leader' | 'officer' | 'member',
            contribution: (row.contribution as number) || 0,
            isOnline,
            joinedAt: row.joined_at as number,
          });
        }

        const clan: ClanInfo = {
          clanId: cRow.clan_id as string,
          name: cRow.name as string,
          tag: cRow.tag as string,
          leaderId: cRow.leader_id as string,
          leaderName: cRow.leader_name as string,
          description: cRow.description as string,
          announcement: cRow.announcement as string,
          badgeId: (cRow.badge_id as number) || 1,
          memberCount: members.length,
          maxMembers: (cRow.max_members as number) || 50,
          minLevel: (cRow.min_level as number) || 1,
          isOpen: (cRow.is_open as number) === 1,
          clanXp: (cRow.clan_xp as number) || 0,
          clanLevel: (cRow.clan_level as number) || 1,
          members,
          onlineCount,
          createdAt: cRow.created_at as number,
        };

        return Response.json({ success: true, hasClan: true, myRank, clan });
      }

      // Browse / Search Public Clans
      if (path === '/clan/list' && request.method === 'GET') {
        const query = url.searchParams.get('search')?.trim() || '';
        const limit = Math.min(Math.max(parseInt(url.searchParams.get('limit') || '20', 10), 1), 50);

        let cursor;
        if (query) {
          cursor = this.ctx.storage.sql.exec(
            `SELECT c.*, COUNT(cm.player_id) AS member_count
             FROM clans c
             LEFT JOIN clan_members cm ON cm.clan_id = c.clan_id
             WHERE c.name LIKE ? OR c.tag LIKE ?
             GROUP BY c.clan_id
             ORDER BY member_count DESC, c.clan_xp DESC
             LIMIT ?`,
            `%${query}%`, `%${query}%`, limit
          );
        } else {
          cursor = this.ctx.storage.sql.exec(
            `SELECT c.*, COUNT(cm.player_id) AS member_count
             FROM clans c
             LEFT JOIN clan_members cm ON cm.clan_id = c.clan_id
             GROUP BY c.clan_id
             ORDER BY member_count DESC, c.clan_xp DESC
             LIMIT ?`,
            limit
          );
        }

        const clans: ClanInfo[] = [];
        for (const row of cursor) {
          clans.push({
            clanId: row.clan_id as string,
            name: row.name as string,
            tag: row.tag as string,
            leaderId: row.leader_id as string,
            leaderName: row.leader_name as string,
            description: row.description as string,
            announcement: row.announcement as string,
            badgeId: (row.badge_id as number) || 1,
            memberCount: (row.member_count as number) || 1,
            maxMembers: (row.max_members as number) || 50,
            minLevel: (row.min_level as number) || 1,
            isOpen: (row.is_open as number) === 1,
            clanXp: (row.clan_xp as number) || 0,
            clanLevel: (row.clan_level as number) || 1,
            createdAt: row.created_at as number,
          });
        }

        return Response.json({ success: true, count: clans.length, clans });
      }

      // Join Clan (Instant if open, or request)
      if (path === '/clan/join' && request.method === 'POST') {
        const body = (await request.json().catch(() => ({}))) as { playerId?: string; clanId?: string };
        const playerId = body.playerId || url.searchParams.get('playerId') || '';
        const clanId = body.clanId || '';

        if (!playerId || !clanId) return Response.json({ success: false, error: 'Missing parameters' }, { status: 400 });

        const clanCursor = this.ctx.storage.sql.exec(`SELECT * FROM clans WHERE clan_id = ?`, clanId);
        let cRow: Record<string, unknown> | null = null;
        for (const c of clanCursor) { cRow = c; break; }
        if (!cRow) return Response.json({ success: false, error: 'Clan not found' }, { status: 404 });

        // Check member capacity
        const countCursor = this.ctx.storage.sql.exec(`SELECT COUNT(*) AS count FROM clan_members WHERE clan_id = ?`, clanId);
        let count = 0;
        for (const c of countCursor) { count = c.count as number; break; }
        if (count >= ((cRow.max_members as number) || 50)) {
          return Response.json({ success: false, error: 'Clan has reached maximum membership capacity (50 members).' }, { status: 409 });
        }

        // Get player profile info
        const profCursor = this.ctx.storage.sql.exec(
          `SELECT a.username, a.numeric_id, p.level FROM accounts a LEFT JOIN profiles p ON p.player_id = a.id WHERE a.id = ?`,
          playerId
        );
        let uName = 'Drifter';
        let numId = 0;
        let lvl = 1;
        for (const p of profCursor) {
          uName = p.username as string;
          numId = (p.numeric_id as number) || 0;
          lvl = (p.level as number) || 1;
          break;
        }

        if (lvl < ((cRow.min_level as number) || 1)) {
          return Response.json({ success: false, error: `Minimum level required to join is Level ${cRow.min_level}.` }, { status: 403 });
        }

        const isOpen = (cRow.is_open as number) === 1;
        if (isOpen) {
          // Instant Join: Leave previous clan
          this.ctx.storage.sql.exec(`DELETE FROM clan_members WHERE player_id = ?`, playerId);
          this.ctx.storage.sql.exec(
            `INSERT INTO clan_members (clan_id, player_id, username, numeric_id, rank, contribution, joined_at)
             VALUES (?, ?, ?, ?, 'member', 100, ?)`,
            clanId, playerId, uName, numId, Date.now()
          );
          return Response.json({ success: true, joined: true, message: `Welcome to ${cRow.name}!` });
        } else {
          // Requires Approval
          this.ctx.storage.sql.exec(
            `INSERT INTO clan_join_requests (clan_id, player_id, username, numeric_id, level, status, created_at)
             VALUES (?, ?, ?, ?, ?, 'pending', ?)`,
            clanId, playerId, uName, numId, lvl, Date.now()
          );
          return Response.json({ success: true, joined: false, message: 'Join request submitted to Clan Officers.' });
        }
      }

      // View Clan Join Requests (Leader / Officer)
      if (path === '/clan/requests' && request.method === 'GET') {
        const playerId = url.searchParams.get('playerId');
        if (!playerId) return Response.json({ success: false, error: 'Missing playerId' }, { status: 400 });

        const memCursor = this.ctx.storage.sql.exec(`SELECT clan_id, rank FROM clan_members WHERE player_id = ?`, playerId);
        let clanId: string | null = null;
        let rank = 'member';
        for (const m of memCursor) { clanId = m.clan_id as string; rank = m.rank as string; break; }
        if (rank !== 'leader' && rank !== 'officer') {
          return Response.json({ success: false, error: 'Officer permissions required.' }, { status: 403 });
        }

        const reqCursor = this.ctx.storage.sql.exec(
          `SELECT id, clan_id, player_id, username, numeric_id, level, status, created_at
           FROM clan_join_requests WHERE clan_id = ? AND status = 'pending' ORDER BY created_at DESC`,
          clanId
        );

        const requests: ClanJoinRequestInfo[] = [];
        for (const row of reqCursor) {
          requests.push({
            id: row.id as number,
            clanId: row.clan_id as string,
            playerId: row.player_id as string,
            username: row.username as string,
            numericId: row.numeric_id as number,
            level: row.level as number,
            status: row.status as 'pending',
            createdAt: row.created_at as number,
          });
        }

        return Response.json({ success: true, requests });
      }

      // Respond to Clan Join Request (Accept / Reject)
      if (path === '/clan/respond-request' && request.method === 'POST') {
        const body = (await request.json().catch(() => ({}))) as {
          playerId?: string;
          requestId?: number;
          action?: 'accept' | 'reject';
        };
        const playerId = body.playerId || url.searchParams.get('playerId') || '';
        const requestId = body.requestId;
        const action = body.action || 'accept';

        const memCursor = this.ctx.storage.sql.exec(`SELECT clan_id, rank FROM clan_members WHERE player_id = ?`, playerId);
        let clanId: string | null = null;
        let rank = 'member';
        for (const m of memCursor) { clanId = m.clan_id as string; rank = m.rank as string; break; }
        if (rank !== 'leader' && rank !== 'officer') {
          return Response.json({ success: false, error: 'Officer permissions required.' }, { status: 403 });
        }

        const reqCursor = this.ctx.storage.sql.exec(`SELECT * FROM clan_join_requests WHERE id = ? AND clan_id = ? AND status = 'pending'`, requestId, clanId);
        let reqRow: Record<string, unknown> | null = null;
        for (const r of reqCursor) { reqRow = r; break; }
        if (!reqRow) return Response.json({ success: false, error: 'Request not found' }, { status: 404 });

        if (action === 'accept') {
          this.ctx.storage.sql.exec(`DELETE FROM clan_members WHERE player_id = ?`, reqRow.player_id as string);
          this.ctx.storage.sql.exec(
            `INSERT INTO clan_members (clan_id, player_id, username, numeric_id, rank, contribution, joined_at)
             VALUES (?, ?, ?, ?, 'member', 100, ?)`,
            clanId, reqRow.player_id as string, reqRow.username as string, reqRow.numeric_id as number, Date.now()
          );
          this.ctx.storage.sql.exec(`UPDATE clan_join_requests SET status = 'accepted' WHERE id = ?`, requestId);
          return Response.json({ success: true, message: 'Player accepted into clan!' });
        } else {
          this.ctx.storage.sql.exec(`UPDATE clan_join_requests SET status = 'rejected' WHERE id = ?`, requestId);
          return Response.json({ success: true, message: 'Join request rejected.' });
        }
      }

      // Leave Clan
      if (path === '/clan/leave' && request.method === 'POST') {
        const body = (await request.json().catch(() => ({}))) as { playerId?: string };
        const playerId = body.playerId || url.searchParams.get('playerId') || '';

        const memCursor = this.ctx.storage.sql.exec(`SELECT clan_id, rank FROM clan_members WHERE player_id = ?`, playerId);
        let clanId: string | null = null;
        let rank = 'member';
        for (const m of memCursor) { clanId = m.clan_id as string; rank = m.rank as string; break; }
        if (!clanId) return Response.json({ success: false, error: 'Not in a clan' }, { status: 400 });

        if (rank === 'leader') {
          const others = this.ctx.storage.sql.exec(`SELECT player_id, username FROM clan_members WHERE clan_id = ? AND player_id != ? ORDER BY CASE rank WHEN 'officer' THEN 1 ELSE 2 END LIMIT 1`, clanId, playerId);
          let nextLead: Record<string, unknown> | null = null;
          for (const o of others) { nextLead = o; break; }

          if (nextLead) {
            this.ctx.storage.sql.exec(`UPDATE clan_members SET rank = 'leader' WHERE clan_id = ? AND player_id = ?`, clanId, nextLead.player_id as string);
            this.ctx.storage.sql.exec(`UPDATE clans SET leader_id = ?, leader_name = ? WHERE clan_id = ?`, nextLead.player_id as string, nextLead.username as string, clanId);
          } else {
            this.ctx.storage.sql.exec(`DELETE FROM clans WHERE clan_id = ?`, clanId);
          }
        }

        this.ctx.storage.sql.exec(`DELETE FROM clan_members WHERE clan_id = ? AND player_id = ?`, clanId, playerId);
        return Response.json({ success: true, message: 'Left clan.' });
      }

      // Clan Kick Member
      if (path === '/clan/kick' && request.method === 'POST') {
        const body = (await request.json().catch(() => ({}))) as { playerId?: string; targetPlayerId?: string };
        const playerId = body.playerId || url.searchParams.get('playerId') || '';
        const targetId = body.targetPlayerId || '';

        const memCursor = this.ctx.storage.sql.exec(`SELECT clan_id, rank FROM clan_members WHERE player_id = ?`, playerId);
        let clanId: string | null = null;
        let callerRank = 'member';
        for (const m of memCursor) { clanId = m.clan_id as string; callerRank = m.rank as string; break; }

        if (callerRank !== 'leader' && callerRank !== 'officer') {
          return Response.json({ success: false, error: 'Officer permissions required.' }, { status: 403 });
        }

        const targetCursor = this.ctx.storage.sql.exec(`SELECT rank FROM clan_members WHERE clan_id = ? AND player_id = ?`, clanId, targetId);
        let targetRank: string | null = null;
        for (const t of targetCursor) { targetRank = t.rank as string; break; }
        if (!targetRank) return Response.json({ success: false, error: 'Target not in clan.' }, { status: 404 });

        if (callerRank === 'officer' && (targetRank === 'officer' || targetRank === 'leader')) {
          return Response.json({ success: false, error: 'Officers cannot kick other officers or the leader.' }, { status: 403 });
        }

        this.ctx.storage.sql.exec(`DELETE FROM clan_members WHERE clan_id = ? AND player_id = ?`, clanId, targetId);
        return Response.json({ success: true, message: 'Member kicked from clan.' });
      }

      // Clan Leader Set Rank (Officer / Member)
      if (path === '/clan/set-rank' && request.method === 'POST') {
        const body = (await request.json().catch(() => ({}))) as { playerId?: string; targetPlayerId?: string; rank?: 'officer' | 'member' };
        const playerId = body.playerId || url.searchParams.get('playerId') || '';
        const targetId = body.targetPlayerId || '';
        const newRank = body.rank || 'member';

        const memCursor = this.ctx.storage.sql.exec(`SELECT clan_id, rank FROM clan_members WHERE player_id = ?`, playerId);
        let clanId: string | null = null;
        let callerRank = 'member';
        for (const m of memCursor) { clanId = m.clan_id as string; callerRank = m.rank as string; break; }
        if (callerRank !== 'leader') return Response.json({ success: false, error: 'Only clan leader can promote/demote ranks.' }, { status: 403 });

        this.ctx.storage.sql.exec(`UPDATE clan_members SET rank = ? WHERE clan_id = ? AND player_id = ?`, newRank, clanId, targetId);
        return Response.json({ success: true, message: `Rank updated to ${newRank}.` });
      }

      // Clan Announcement
      if (path === '/clan/announcement' && request.method === 'POST') {
        const body = (await request.json().catch(() => ({}))) as { playerId?: string; announcement?: string };
        const playerId = body.playerId || url.searchParams.get('playerId') || '';
        const announcement = filterProfanity((body.announcement || '').substring(0, 200));

        const memCursor = this.ctx.storage.sql.exec(`SELECT clan_id, rank FROM clan_members WHERE player_id = ?`, playerId);
        let clanId: string | null = null;
        let callerRank = 'member';
        for (const m of memCursor) { clanId = m.clan_id as string; callerRank = m.rank as string; break; }
        if (callerRank !== 'leader' && callerRank !== 'officer') {
          return Response.json({ success: false, error: 'Officer permissions required.' }, { status: 403 });
        }

        this.ctx.storage.sql.exec(`UPDATE clans SET announcement = ? WHERE clan_id = ?`, announcement, clanId);
        return Response.json({ success: true, announcement });
      }

      // Clan Chat
      if (path === '/clan/chat' && request.method === 'POST') {
        const body = (await request.json().catch(() => ({}))) as { playerId?: string; message?: string };
        const playerId = body.playerId || url.searchParams.get('playerId') || '';
        const msg = filterProfanity((body.message || '').substring(0, 150));
        if (!playerId || !msg.trim()) return Response.json({ success: false, error: 'Empty message' }, { status: 400 });

        const memCursor = this.ctx.storage.sql.exec(`SELECT clan_id, username, rank FROM clan_members WHERE player_id = ?`, playerId);
        let clanId: string | null = null;
        let uName = 'Drifter';
        let rank = 'member';
        for (const m of memCursor) { clanId = m.clan_id as string; uName = m.username as string; rank = m.rank as string; break; }
        if (!clanId) return Response.json({ success: false, error: 'Not in a clan' }, { status: 400 });

        const now = Date.now();
        this.ctx.storage.sql.exec(
          `INSERT INTO clan_chat (clan_id, sender_id, sender_name, sender_rank, message, sent_at) VALUES (?, ?, ?, ?, ?, ?)`,
          clanId, playerId, uName, rank, msg, now
        );

        // Award clan XP (+10) for activity
        this.ctx.storage.sql.exec(
          `UPDATE clans SET clan_xp = clan_xp + 10, clan_level = 1 + ((clan_xp + 10) / 2500) WHERE clan_id = ?`,
          clanId
        );

        return Response.json({ success: true, sentAt: now });
      }

      if (path === '/clan/chat' && request.method === 'GET') {
        const playerId = url.searchParams.get('playerId');
        if (!playerId) return Response.json({ success: false, error: 'Missing playerId' }, { status: 400 });

        const memCursor = this.ctx.storage.sql.exec(`SELECT clan_id FROM clan_members WHERE player_id = ?`, playerId);
        let clanId: string | null = null;
        for (const m of memCursor) { clanId = m.clan_id as string; break; }
        if (!clanId) return Response.json({ success: true, messages: [] });

        const chatCursor = this.ctx.storage.sql.exec(
          `SELECT id, clan_id, sender_id, sender_name, sender_rank, message, sent_at
           FROM clan_chat WHERE clan_id = ? ORDER BY sent_at DESC LIMIT 50`,
          clanId
        );

        const messages: ClanChatMessage[] = [];
        for (const row of chatCursor) {
          messages.push({
            id: row.id as number,
            clanId: row.clan_id as string,
            senderId: row.sender_id as string,
            senderName: row.sender_name as string,
            senderRank: row.sender_rank as string,
            message: row.message as string,
            sentAt: row.sent_at as number,
          });
        }

        messages.reverse();
        return Response.json({ success: true, messages });
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
