import { GameRoomDO } from './GameRoomDO';
import { WorldZoneDO } from './WorldZoneDO';
import { AuthService } from './auth';
import { AuthResponse, MatchmakingResponse, CoopRole } from './types';

export { GameRoomDO, WorldZoneDO };

interface Env {
  GAME_ROOM: DurableObjectNamespace;
  WORLD_ZONE: DurableObjectNamespace;
  ENVIRONMENT: string;
  JWT_SECRET: string;
  JWT_REFRESH_SECRET: string;
  TURNSTILE_SECRET_KEY: string;
  ALLOWED_ORIGINS: string;
}

// In-Memory IP Rate Limiter for Worker edge instances
const rateLimitMap = new Map<string, { count: number; resetTime: number }>();

function checkRateLimit(ip: string, maxRequests: number = 30, windowMs: number = 60000): boolean {
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
    if (!checkRateLimit(clientIp, 60, 60000)) {
      return new Response(JSON.stringify({ error: 'Rate limit exceeded. Please wait.' }), {
        status: 429,
        headers: { 'Content-Type': 'application/json' },
      });
    }

    try {
      // 3. Health & Status Check
      if (url.pathname === '/' || url.pathname === '/api/health') {
        return Response.json({
          status: 'online',
          service: 'AetherSerpent_EternalCoil Backend',
          environment: env.ENVIRONMENT || 'production',
          timestamp: Date.now(),
        });
      }

      // 4. Player Authentication with Turnstile & JWT
      if (url.pathname === '/api/auth/login' && request.method === 'POST') {
        const body = (await request.json()) as { username?: string; turnstileToken?: string };
        const username = body.username?.trim() || `Serpent_${Math.floor(Math.random() * 9000 + 1000)}`;
        const turnstileToken = body.turnstileToken || request.headers.get('X-Turnstile-Token') || 'test-token';

        // Cloudflare Turnstile Verification
        const isTurnstileValid = await AuthService.verifyTurnstile(
          turnstileToken,
          env.TURNSTILE_SECRET_KEY,
          clientIp
        );

        if (!isTurnstileValid) {
          return Response.json({ success: false, error: 'Turnstile security check failed.' }, { status: 403 });
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

      // 5. Matchmaking / Room Allocation
      if (url.pathname === '/api/matchmaking/find-or-create' && request.method === 'POST') {
        // Authenticate request token
        const authHeader = request.headers.get('Authorization');
        const token = authHeader?.replace('Bearer ', '') || url.searchParams.get('token');
        if (!token) {
          return Response.json({ error: 'Missing authorization token.' }, { status: 401 });
        }

        const payload = await AuthService.verifyJWT(token, env.JWT_SECRET);
        if (!payload) {
          return Response.json({ error: 'Invalid or expired JWT.' }, { status: 401 });
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

      // 6. GameRoom WebSocket Upgrades
      const roomMatch = url.pathname.match(/^\/api\/room\/([^/]+)\/ws$/);
      if (roomMatch) {
        const roomId = roomMatch[1];
        const doId = env.GAME_ROOM.idFromName(roomId);
        const stub = env.GAME_ROOM.get(doId);
        return stub.fetch(request);
      }

      // 7. World Zone Shards
      const zoneMatch = url.pathname.match(/^\/api\/zone\/([^/]+)\/ws$/);
      if (zoneMatch) {
        const zoneId = zoneMatch[1];
        const doId = env.WORLD_ZONE.idFromName(zoneId);
        const stub = env.WORLD_ZONE.get(doId);
        return stub.fetch(request);
      }

      return new Response('Endpoint not found', { status: 404 });
    } catch (err: unknown) {
      const errorMessage = err instanceof Error ? err.message : String(err);
      return Response.json({ error: 'Internal Server Error', details: errorMessage }, { status: 500 });
    }
  },
};
