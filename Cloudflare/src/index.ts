import { GameRoomDO } from './GameRoomDO';
import { WorldZoneDO } from './WorldZoneDO';
import { SocialHubDO } from './SocialHubDO';
import { AuthService } from './auth';
import { getDashboardHtml } from './dashboard';
import { AuthResponse, MatchmakingResponse, CoopRole, VersionCheckResponse } from './types';

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

function checkRateLimit(ip: string, maxRequests: number = 60, windowMs: number = 60000): boolean {
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
    if (!checkRateLimit(clientIp, 120, 60000)) {
      return new Response(JSON.stringify({ error: 'Rate limit exceeded. Please wait.' }), {
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
          message: 'All servers nominal. Co-op and Leaderboards active.'
        }, { headers: { 'Access-Control-Allow-Origin': '*' } });
      }

      // 5. Player Authentication with Turnstile & JWT
      if (url.pathname === '/api/auth/login' && request.method === 'POST') {
        const body = (await request.json().catch(() => ({}))) as { username?: string; turnstileToken?: string };
        const username = body.username?.trim() || `Serpent_${Math.floor(Math.random() * 9000 + 1000)}`;
        const turnstileToken = body.turnstileToken || request.headers.get('X-Turnstile-Token') || 'test-token';

        // Cloudflare Turnstile Verification
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

        // Sign Access Token (15 minutes)
        const accessToken = await AuthService.signJWT(
          {
            sub: playerId,
            username,
            role: 'player',
            iat: nowInSeconds,
            exp: nowInSeconds + (15 * 60),
          },
          env.JWT_SECRET
        );

        // Sign Refresh Token (7 days)
        const refreshToken = await AuthService.signJWT(
          {
            sub: playerId,
            username,
            role: 'player',
            iat: nowInSeconds,
            exp: nowInSeconds + (7 * 24 * 60 * 60),
          },
          env.JWT_REFRESH_SECRET
        );

        const response: AuthResponse = {
          success: true,
          accessToken,
          refreshToken,
          playerId,
          username,
          expiresIn: 900,
        };

        return Response.json(response, {
          headers: {
            'Access-Control-Allow-Origin': '*',
            'Content-Type': 'application/json',
          },
        });
      }

      // 6. Refresh Token Rotation
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
          {
            sub: payload.sub,
            username: payload.username,
            role: 'player',
            iat: nowInSeconds,
            exp: nowInSeconds + (15 * 60),
          },
          env.JWT_SECRET
        );

        const newRefreshToken = await AuthService.signJWT(
          {
            sub: payload.sub,
            username: payload.username,
            role: 'player',
            iat: nowInSeconds,
            exp: nowInSeconds + (7 * 24 * 60 * 60),
          },
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

      // 7. Logout Endpoint
      if (url.pathname === '/api/auth/logout' && request.method === 'POST') {
        return Response.json({ success: true, message: 'Session terminated' }, {
          headers: { 'Access-Control-Allow-Origin': '*' }
        });
      }

      // 8. Matchmaking / Room Allocation
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

      // 9. GameRoom WebSocket Upgrades
      const roomMatch = url.pathname.match(/^\/api\/room\/([^/]+)\/ws$/);
      if (roomMatch) {
        const roomId = roomMatch[1];
        const doId = env.GAME_ROOM.idFromName(roomId);
        const stub = env.GAME_ROOM.get(doId);
        return stub.fetch(request);
      }

      // 10. World Zone Shards
      const zoneMatch = url.pathname.match(/^\/api\/zone\/([^/]+)\/ws$/);
      if (zoneMatch) {
        const zoneId = zoneMatch[1];
        const doId = env.WORLD_ZONE.idFromName(zoneId);
        const stub = env.WORLD_ZONE.get(doId);
        return stub.fetch(request);
      }

      // 11. Social Hub (Leaderboards, Friends, Parties, Cloud Saves)
      if (
        url.pathname.startsWith('/api/leaderboard/') ||
        url.pathname.startsWith('/api/social/') ||
        url.pathname.startsWith('/api/save/')
      ) {
        // Authenticate request token for score submission, friends, party, saves
        const authHeader = request.headers.get('Authorization');
        const token = authHeader?.replace('Bearer ', '') || url.searchParams.get('token');
        let playerPayload: { sub: string; username: string } | null = null;

        if (token) {
          playerPayload = await AuthService.verifyJWT(token, env.JWT_SECRET);
        }

        // Leaderboard reads are public; mutations require auth
        const isPublicRead = request.method === 'GET' && url.pathname.startsWith('/api/leaderboard/');
        if (!isPublicRead && !playerPayload) {
          return Response.json({ error: 'Authorization required for this endpoint.' }, {
            status: 401,
            headers: { 'Access-Control-Allow-Origin': '*' }
          });
        }

        // Forward to the singleton SocialHubDO
        const socialId = env.SOCIAL_HUB.idFromName('GLOBAL_SOCIAL_HUB');
        const stub = env.SOCIAL_HUB.get(socialId);

        // Normalize internal path
        let internalPath = url.pathname.replace('/api/leaderboard', '/leaderboard')
                                       .replace('/api/social', '')
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
        const res = await stub.fetch(proxyReq);

        // Return with CORS headers
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
