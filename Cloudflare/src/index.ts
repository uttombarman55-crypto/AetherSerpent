import { GameRoomDO } from './GameRoomDO';
import { WorldZoneDO } from './WorldZoneDO';
import { SocialHubDO } from './SocialHubDO';
import { AuthService } from './auth';
import { getDashboardHtml } from './dashboard';
import {
  AuthResponse,
  MatchmakingResponse,
  CoopRole,
  VersionCheckResponse,
  AccountAuthResponse
} from './types';

export { GameRoomDO, WorldZoneDO, SocialHubDO };

interface Env {
  GAME_ROOM: DurableObjectNamespace;
  WORLD_ZONE: DurableObjectNamespace;
  SOCIAL_HUB: DurableObjectNamespace;
  ENVIRONMENT: string;
  JWT_SECRET: string;
  JWT_REFRESH_SECRET: string;
  TURNSTILE_SECRET_KEY: string;
  ALLOWED_ORIGINS: string;
}

// In-Memory IP Rate Limiter for Worker edge instances
const rateLimitMap = new Map<string, { count: number; resetTime: number }>();

function checkRateLimit(ip: string, maxRequests: number = 120, windowMs: number = 60000): boolean {
  const now = Date.now();
  const entry = rateLimitMap.get(ip);

  if (!entry || now > entry.resetTime) {
    rateLimitMap.set(ip, { count: 1, resetTime: now + windowMs });
    return true;
  }

  if (entry.count >= maxRequests) {
    return false;
  }

  entry.count++;
  return true;
}

export default {
  async fetch(request: Request, env: Env): Promise<Response> {
    const url = new URL(request.url);
    const clientIp = request.headers.get('CF-Connecting-IP') || '127.0.0.1';

    // 1. CORS Preflight
    if (request.method === 'OPTIONS') {
      return new Response(null, {
        headers: {
          'Access-Control-Allow-Origin': '*',
          'Access-Control-Allow-Methods': 'GET, POST, OPTIONS',
          'Access-Control-Allow-Headers': 'Content-Type, Authorization, X-Turnstile-Token',
        },
      });
    }

    // 2. Global Rate Limiter Check
    if (!checkRateLimit(clientIp, 180, 60000)) {
      return new Response(JSON.stringify({ error: 'Rate limit exceeded. Please wait a moment.' }), {
        status: 429,
        headers: {
          'Access-Control-Allow-Origin': '*',
          'Content-Type': 'application/json'
        },
      });
    }

    try {
      // 3. Health & Status Check / Celestial Edge Dashboard
      if (url.pathname === '/') {
        const acceptsHtml = request.headers.get('Accept')?.includes('text/html') || false;
        if (acceptsHtml) {
          return new Response(getDashboardHtml(), {
            headers: {
              'Content-Type': 'text/html; charset=utf-8',
              'Access-Control-Allow-Origin': '*',
            },
          });
        }

        return Response.json({
          status: 'online',
          service: 'AetherSerpent_EternalCoil Backend',
          environment: env.ENVIRONMENT || 'production',
          version: '1.0.0',
          timestamp: Date.now(),
        }, {
          headers: { 'Access-Control-Allow-Origin': '*' }
        });
      }

      if (url.pathname === '/api/health') {
        return Response.json({
          status: 'online',
          service: 'AetherSerpent_EternalCoil Backend',
          environment: env.ENVIRONMENT || 'production',
          version: '1.0.0',
          timestamp: Date.now(),
        }, {
          headers: { 'Access-Control-Allow-Origin': '*' }
        });
      }

      // 4. System Status & Version Check
      if (url.pathname === '/api/system/version-check') {
        const clientVer = url.searchParams.get('clientVersion') || '1.0.0';
        const response: VersionCheckResponse = {
          clientVersion: clientVer,
          minSupportedVersion: '1.0.0',
          latestVersion: '1.0.0',
          isUpdateRequired: false,
          isMaintenanceActive: false,
          maintenanceMessage: 'All celestial servers operating normally.'
        };
        return Response.json(response, { headers: { 'Access-Control-Allow-Origin': '*' } });
      }

      if (url.pathname === '/api/system/maintenance') {
        return Response.json({
          maintenance: false,
          message: 'All servers nominal. Co-op, Account and Leaderboards active.'
        }, { headers: { 'Access-Control-Allow-Origin': '*' } });
      }

      // ---------------------------------------------------------
      // 5. FREE FIRE STYLE ACCOUNT & IDENTITY SYSTEM
      // ---------------------------------------------------------
      const socialId = env.SOCIAL_HUB.idFromName('GLOBAL_SOCIAL_HUB');
      const socialStub = env.SOCIAL_HUB.get(socialId);

      // Register / Login / Guest-Login / Upgrade-Guest
      if (
        (url.pathname === '/api/account/register' ||
         url.pathname === '/api/account/login' ||
         url.pathname === '/api/account/guest-login' ||
         url.pathname === '/api/account/upgrade-guest') &&
        request.method === 'POST'
      ) {
        const body = (await request.json().catch(() => ({}))) as Record<string, unknown>;
        const turnstileToken = (body.turnstileToken as string) || request.headers.get('X-Turnstile-Token') || 'test-token';

        // Optional Turnstile Bot check on registration and guest account creation
        if (url.pathname === '/api/account/register' || url.pathname === '/api/account/guest-login') {
          const isTurnstileValid = await AuthService.verifyTurnstile(
            turnstileToken,
            env.TURNSTILE_SECRET_KEY,
            clientIp
          );
          if (!isTurnstileValid) {
            return Response.json({ success: false, error: 'Security verification failed.' }, {
              status: 403,
              headers: { 'Access-Control-Allow-Origin': '*' }
            });
          }
        }

        // Forward to SocialHubDO SQLite table
        const internalPath = url.pathname.replace('/api', '');
        const internalUrl = new URL(request.url);
        internalUrl.pathname = internalPath;

        // If upgrade-guest, verify existing JWT token first
        if (url.pathname === '/api/account/upgrade-guest') {
          const authHeader = request.headers.get('Authorization');
          const token = authHeader?.replace('Bearer ', '') || url.searchParams.get('token');
          if (!token) return Response.json({ success: false, error: 'Authorization required to upgrade.' }, { status: 401 });
          const payload = await AuthService.verifyJWT(token, env.JWT_SECRET);
          if (!payload) return Response.json({ success: false, error: 'Invalid session token.' }, { status: 401 });
          internalUrl.searchParams.set('playerId', payload.sub);
          body.playerId = payload.sub;
        }

        const subReq = new Request(internalUrl.toString(), {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify(body)
        });

        const doRes = await socialStub.fetch(subReq);
        const resData = (await doRes.json()) as Record<string, unknown>;

        if (!resData.success) {
          return Response.json(resData, {
            status: doRes.status,
            headers: { 'Access-Control-Allow-Origin': '*' }
          });
        }

        const account = resData.account as { playerId: string; username: string; numericId: number; isGuest: boolean };
        const nowInSeconds = Math.floor(Date.now() / 1000);

        // Sign Access Token (15 minutes)
        const accessToken = await AuthService.signJWT(
          {
            sub: account.playerId,
            username: account.username,
            role: 'player',
            iat: nowInSeconds,
            exp: nowInSeconds + (15 * 60),
          },
          env.JWT_SECRET
        );

        // Sign Refresh Token (7 days)
        const refreshToken = await AuthService.signJWT(
          {
            sub: account.playerId,
            username: account.username,
            role: 'player',
            iat: nowInSeconds,
            exp: nowInSeconds + (7 * 24 * 60 * 60),
          },
          env.JWT_REFRESH_SECRET
        );

        const fullResponse: AccountAuthResponse = {
          success: true,
          accessToken,
          refreshToken,
          playerId: account.playerId,
          username: account.username,
          numericId: account.numericId,
          isGuest: account.isGuest,
          recoveryCode: resData.recoveryCode as string | undefined,
          expiresIn: 900,
          profile: resData.profile as any,
        };

        return Response.json(fullResponse, {
          headers: {
            'Access-Control-Allow-Origin': '*',
            'Content-Type': 'application/json',
          },
        });
      }

      // Legacy simple login for backward compatibility
      if (url.pathname === '/api/auth/login' && request.method === 'POST') {
        const body = (await request.json().catch(() => ({}))) as { username?: string; turnstileToken?: string };
        const username = body.username?.trim() || `Serpent_${Math.floor(Math.random() * 9000 + 1000)}`;
        const turnstileToken = body.turnstileToken || request.headers.get('X-Turnstile-Token') || 'test-token';

        const isTurnstileValid = await AuthService.verifyTurnstile(
          turnstileToken,
          env.TURNSTILE_SECRET_KEY,
          clientIp
        );

        if (!isTurnstileValid) {
          return Response.json({ success: false, error: 'Turnstile security verification failed.' }, {
            status: 403,
            headers: { 'Access-Control-Allow-Origin': '*' }
          });
        }

        const playerId = `usr_${crypto.randomUUID().substring(0, 8)}`;
        const nowInSeconds = Math.floor(Date.now() / 1000);

        const accessToken = await AuthService.signJWT(
          { sub: playerId, username, role: 'player', iat: nowInSeconds, exp: nowInSeconds + (15 * 60) },
          env.JWT_SECRET
        );

        const refreshToken = await AuthService.signJWT(
          { sub: playerId, username, role: 'player', iat: nowInSeconds, exp: nowInSeconds + (7 * 24 * 60 * 60) },
          env.JWT_REFRESH_SECRET
        );

        return Response.json({
          success: true,
          accessToken,
          refreshToken,
          playerId,
          username,
          expiresIn: 900,
        }, { headers: { 'Access-Control-Allow-Origin': '*' } });
      }

      // Refresh Token Rotation
      if (url.pathname === '/api/auth/refresh' && request.method === 'POST') {
        const body = (await request.json().catch(() => ({}))) as { refreshToken?: string };
        const oldRefresh = body.refreshToken;
        if (!oldRefresh) {
          return Response.json({ success: false, error: 'Missing refreshToken' }, {
            status: 400,
            headers: { 'Access-Control-Allow-Origin': '*' }
          });
        }

        const payload = await AuthService.verifyJWT(oldRefresh, env.JWT_REFRESH_SECRET);
        if (!payload) {
          return Response.json({ success: false, error: 'Invalid or expired refresh token' }, {
            status: 401,
            headers: { 'Access-Control-Allow-Origin': '*' }
          });
        }

        const nowInSeconds = Math.floor(Date.now() / 1000);
        const newAccessToken = await AuthService.signJWT(
          { sub: payload.sub, username: payload.username, role: 'player', iat: nowInSeconds, exp: nowInSeconds + (15 * 60) },
          env.JWT_SECRET
        );

        const newRefreshToken = await AuthService.signJWT(
          { sub: payload.sub, username: payload.username, role: 'player', iat: nowInSeconds, exp: nowInSeconds + (7 * 24 * 60 * 60) },
          env.JWT_REFRESH_SECRET
        );

        return Response.json({
          success: true,
          accessToken: newAccessToken,
          refreshToken: newRefreshToken,
          playerId: payload.sub,
          username: payload.username,
          expiresIn: 900,
        }, { headers: { 'Access-Control-Allow-Origin': '*' } });
      }

      // Account Recovery (No token required)
      if (url.pathname === '/api/account/recover' && request.method === 'POST') {
        const internalUrl = new URL(request.url);
        internalUrl.pathname = '/account/recover';
        const proxyReq = new Request(internalUrl.toString(), request);
        const res = await socialStub.fetch(proxyReq);
        const headers = new Headers(res.headers);
        headers.set('Access-Control-Allow-Origin', '*');
        return new Response(res.body, { status: res.status, headers });
      }

      // Change Username (Requires Token)
      if (url.pathname === '/api/account/change-username' && request.method === 'POST') {
        const authHeader = request.headers.get('Authorization');
        const token = authHeader?.replace('Bearer ', '') || url.searchParams.get('token');
        if (!token) return Response.json({ success: false, error: 'Authorization required' }, { status: 401 });

        const payload = await AuthService.verifyJWT(token, env.JWT_SECRET);
        if (!payload) return Response.json({ success: false, error: 'Invalid or expired token' }, { status: 401 });

        const internalUrl = new URL(request.url);
        internalUrl.pathname = '/account/change-username';
        internalUrl.searchParams.set('playerId', payload.sub);

        const proxyReq = new Request(internalUrl.toString(), request);
        const res = await socialStub.fetch(proxyReq);
        const resData = (await res.json()) as Record<string, unknown>;

        if (!resData.success) {
          return Response.json(resData, { status: res.status, headers: { 'Access-Control-Allow-Origin': '*' } });
        }

        const newUsername = resData.username as string;
        const nowInSeconds = Math.floor(Date.now() / 1000);

        // Sign updated tokens with the new username
        const newAccessToken = await AuthService.signJWT(
          { sub: payload.sub, username: newUsername, role: 'player', iat: nowInSeconds, exp: nowInSeconds + (15 * 60) },
          env.JWT_SECRET
        );
        const newRefreshToken = await AuthService.signJWT(
          { sub: payload.sub, username: newUsername, role: 'player', iat: nowInSeconds, exp: nowInSeconds + (7 * 24 * 60 * 60) },
          env.JWT_REFRESH_SECRET
        );

        resData.accessToken = newAccessToken;
        resData.refreshToken = newRefreshToken;

        return Response.json(resData, { headers: { 'Access-Control-Allow-Origin': '*' } });
      }

      // Profile Endpoints
      if (url.pathname.startsWith('/api/profile/')) {
        const authHeader = request.headers.get('Authorization');
        const token = authHeader?.replace('Bearer ', '') || url.searchParams.get('token');
        let playerPayload: { sub: string; username: string } | null = null;
        if (token) {
          playerPayload = await AuthService.verifyJWT(token, env.JWT_SECRET);
        }

        // Public read endpoints: /get, /search
        const isPublic = url.pathname === '/api/profile/get' || url.pathname === '/api/profile/search';
        if (!isPublic && !playerPayload) {
          return Response.json({ success: false, error: 'Authorization required for this profile action.' }, {
            status: 401,
            headers: { 'Access-Control-Allow-Origin': '*' }
          });
        }

        const internalUrl = new URL(request.url);
        internalUrl.pathname = url.pathname.replace('/api', '');
        if (playerPayload) {
          internalUrl.searchParams.set('playerId', playerPayload.sub);
          internalUrl.searchParams.set('username', playerPayload.username);
        }

        const proxyReq = new Request(internalUrl.toString(), request);
        const res = await socialStub.fetch(proxyReq);
        const headers = new Headers(res.headers);
        headers.set('Access-Control-Allow-Origin', '*');
        return new Response(res.body, { status: res.status, headers });
      }

      // Logout Endpoint
      if (url.pathname === '/api/auth/logout' && request.method === 'POST') {
        return Response.json({ success: true, message: 'Session terminated' }, {
          headers: { 'Access-Control-Allow-Origin': '*' }
        });
      }

      // 6. Matchmaking / Room Allocation
      if (url.pathname === '/api/matchmaking/find-or-create' && request.method === 'POST') {
        const authHeader = request.headers.get('Authorization');
        const token = authHeader?.replace('Bearer ', '') || url.searchParams.get('token');
        if (!token) {
          return Response.json({ error: 'Missing authorization token.' }, {
            status: 401,
            headers: { 'Access-Control-Allow-Origin': '*' }
          });
        }

        const payload = await AuthService.verifyJWT(token, env.JWT_SECRET);
        if (!payload) {
          return Response.json({ error: 'Invalid or expired JWT.' }, {
            status: 401,
            headers: { 'Access-Control-Allow-Origin': '*' }
          });
        }

        const body = (await request.json().catch(() => ({}))) as {
          preferredRoomId?: string;
          requestedRole?: CoopRole;
        };

        const roomId = body.preferredRoomId?.trim() || `ROOM_${Math.floor(Math.random() * 9000 + 1000)}`;
        const role = body.requestedRole || 'solo';

        const matchResponse: MatchmakingResponse = {
          success: true,
          roomId,
          assignedRole: role,
          wsUrl: `/api/room/${roomId}/ws?playerId=${payload.sub}&username=${encodeURIComponent(payload.username)}&role=${role}`,
        };

        return Response.json(matchResponse, {
          headers: { 'Access-Control-Allow-Origin': '*' },
        });
      }

      // 7. GameRoom WebSocket Upgrades
      const roomMatch = url.pathname.match(/^\/api\/room\/([^/]+)\/ws$/);
      if (roomMatch) {
        const roomId = roomMatch[1];
        const doId = env.GAME_ROOM.idFromName(roomId);
        const stub = env.GAME_ROOM.get(doId);
        return stub.fetch(request);
      }

      // 8. World Zone Shards
      const zoneMatch = url.pathname.match(/^\/api\/zone\/([^/]+)\/ws$/);
      if (zoneMatch) {
        const zoneId = zoneMatch[1];
        const doId = env.WORLD_ZONE.idFromName(zoneId);
        const stub = env.WORLD_ZONE.get(doId);
        return stub.fetch(request);
      }

      // 8.5. Real-Time Chat WebSocket Upgrade
      if (url.pathname === '/api/chat/ws') {
        const token = url.searchParams.get('token') || request.headers.get('Authorization')?.replace('Bearer ', '');
        let playerPayload: { sub: string; username: string } | null = null;
        if (token) {
          playerPayload = await AuthService.verifyJWT(token, env.JWT_SECRET);
        }

        const internalUrl = new URL(request.url);
        internalUrl.pathname = '/chat/ws';
        if (playerPayload) {
          internalUrl.searchParams.set('playerId', playerPayload.sub);
          internalUrl.searchParams.set('username', playerPayload.username);
        }

        const reqProxy = new Request(internalUrl.toString(), request);
        return socialStub.fetch(reqProxy);
      }

      // 8.6. Real-Time Voice Chat & WebRTC Signaling WebSocket Upgrade
      if (url.pathname === '/api/voice/ws') {
        const token = url.searchParams.get('token') || request.headers.get('Authorization')?.replace('Bearer ', '');
        let playerPayload: { sub: string; username: string } | null = null;
        if (token) {
          playerPayload = await AuthService.verifyJWT(token, env.JWT_SECRET);
        }

        const internalUrl = new URL(request.url);
        internalUrl.pathname = '/voice/ws';
        if (playerPayload) {
          internalUrl.searchParams.set('playerId', playerPayload.sub);
          internalUrl.searchParams.set('username', playerPayload.username);
        }

        const reqProxy = new Request(internalUrl.toString(), request);
        return socialStub.fetch(reqProxy);
      }

      // 9. Social Hub (Leaderboards, Friends, Parties, Chat, Voice, Notifications, Cloud Saves)
      if (
        url.pathname.startsWith('/api/leaderboard/') ||
        url.pathname.startsWith('/api/social/') ||
        url.pathname.startsWith('/api/chat/') ||
        url.pathname.startsWith('/api/voice/') ||
        url.pathname.startsWith('/api/notifications/') ||
        url.pathname.startsWith('/api/presence/') ||
        url.pathname.startsWith('/api/save/')
      ) {
        const authHeader = request.headers.get('Authorization');
        const token = authHeader?.replace('Bearer ', '') || url.searchParams.get('token');
        let playerPayload: { sub: string; username: string } | null = null;

        if (token) {
          playerPayload = await AuthService.verifyJWT(token, env.JWT_SECRET);
        }

        const isPublicRead = request.method === 'GET' && (
          url.pathname.startsWith('/api/leaderboard/') ||
          url.pathname === '/api/social/clan/list' ||
          url.pathname.startsWith('/api/chat/history') ||
          url.pathname.startsWith('/api/voice/status')
        );
        if (!isPublicRead && !playerPayload) {
          return Response.json({ error: 'Authorization required for this endpoint.' }, {
            status: 401,
            headers: { 'Access-Control-Allow-Origin': '*' }
          });
        }

        let internalPath = url.pathname.replace('/api/leaderboard', '/leaderboard')
                                       .replace('/api/social', '')
                                       .replace('/api/chat', '/chat')
                                       .replace('/api/voice', '/voice')
                                       .replace('/api/notifications', '/notifications')
                                       .replace('/api/presence', '/presence')
                                       .replace('/api/save', '/save');
        if (url.pathname === '/api/leaderboard/submit') {
          internalPath = '/submit-score';
        }

        const internalUrl = new URL(request.url);
        internalUrl.pathname = internalPath;
        if (playerPayload) {
          internalUrl.searchParams.set('playerId', playerPayload.sub);
          internalUrl.searchParams.set('username', playerPayload.username);
        }

        const proxyReq = new Request(internalUrl.toString(), request);
        const res = await socialStub.fetch(proxyReq);

        const newHeaders = new Headers(res.headers);
        newHeaders.set('Access-Control-Allow-Origin', '*');
        return new Response(res.body, {
          status: res.status,
          headers: newHeaders,
        });
      }

      return new Response('Endpoint not found', {
        status: 404,
        headers: { 'Access-Control-Allow-Origin': '*' }
      });
    } catch (err: unknown) {
      const errorMessage = err instanceof Error ? err.message : String(err);
      return Response.json({ error: 'Internal Server Error', details: errorMessage }, {
        status: 500,
        headers: { 'Access-Control-Allow-Origin': '*' }
      });
    }
  },
};
