import { DurableObject } from 'cloudflare:workers';
import {
  SerpentNetworkState,
  ClientInputPayload,
  PreyConsumptionPayload,
  CoopRole,
  NetworkMessage,
  Vector3Data,
  QuaternionData
} from './types';

interface Env {
  GAME_ROOM: DurableObjectNamespace;
  WORLD_ZONE: DurableObjectNamespace;
  JWT_SECRET: string;
}

interface PlayerSession {
  playerId: string;
  username: string;
  role: CoopRole;
  lastMessageTime: number;
  messageCount: number;
}

export class GameRoomDO extends DurableObject {
  private serpentState: SerpentNetworkState;
  private players: Map<WebSocket, PlayerSession> = new Map();
  private lastTickTime: number = Date.now();
  private tickIntervalMs: number = 50; // 20 ticks per second

  // Co-op Player Inputs
  private latestHeadInput: ClientInputPayload | null = null;
  private latestTailInput: ClientInputPayload | null = null;

  // Active Orbs for server-authoritative collision
  private activeOrbs: Map<string, Vector3Data> = new Map();

  constructor(ctx: DurableObjectState, env: Env) {
    super(ctx, env);

    // Initialize SQLite Tables in Durable Object
    this.ctx.storage.sql.exec(`
      CREATE TABLE IF NOT EXISTS game_sessions (
        id TEXT PRIMARY KEY,
        created_at INTEGER,
        updated_at INTEGER,
        max_length INTEGER,
        final_score INTEGER,
        evolution_path INTEGER
      );

      CREATE TABLE IF NOT EXISTS cheat_logs (
        id INTEGER PRIMARY KEY AUTOINCREMENT,
        player_id TEXT,
        violation_type TEXT,
        details TEXT,
        timestamp INTEGER
      );
    `);

    // Initial Authoritative Serpent State
    this.serpentState = {
      headPosition: { x: 0, y: 3.5, z: 0 },
      headRotation: { x: 0, y: 0, z: 0, w: 1 },
      speed: 10.0,
      isBoosting: false,
      evolutionPath: 0,
      segmentCount: 50,
      segmentPositions: this.generateInitialSpine({ x: 0, y: 3.5, z: 0 }, 50, 0.85),
      health: 100,
      hunger: 100,
      energy: 100,
      score: 0,
      lastUpdatedTimestamp: Date.now()
    };

    // Seed authoritative orbs
    this.seedAuthoritativeOrbs();

    // Start simulation loop via Alarm
    this.ctx.storage.getAlarm().then((alarm) => {
      if (!alarm) {
        this.ctx.storage.setAlarm(Date.now() + this.tickIntervalMs);
      }
    });
  }

  private seedAuthoritativeOrbs() {
    for (let i = 0; i < 24; i++) {
      const id = `orb_${i}`;
      this.activeOrbs.set(id, {
        x: (Math.random() - 0.5) * 80,
        y: 2.5 + Math.random() * 12,
        z: (Math.random() - 0.5) * 80
      });
    }
  }

  private generateInitialSpine(head: Vector3Data, count: number, spacing: number): Vector3Data[] {
    const list: Vector3Data[] = [];
    for (let i = 1; i <= count; i++) {
      list.push({ x: head.x, y: head.y, z: head.z - (i * spacing) });
    }
    return list;
  }

  /**
   * Handle incoming HTTP / WebSocket upgrade request.
   */
  async fetch(request: Request): Promise<Response> {
    const url = new URL(request.url);

    if (url.pathname.endsWith('/ws')) {
      const upgradeHeader = request.headers.get('Upgrade');
      if (!upgradeHeader || upgradeHeader !== 'websocket') {
        return new Response('Expected WebSocket upgrade', { status: 426 });
      }

      const playerId = url.searchParams.get('playerId') || `guest_${Math.random().toString(36).substring(7)}`;
      const requestedRole = (url.searchParams.get('role') as CoopRole) || 'solo';
      const username = url.searchParams.get('username') || 'AetherDrifter';

      const pair = new WebSocketPair();
      const [clientWs, serverWs] = Object.values(pair);

      // Assign co-op role based on room capacity
      const assignedRole = this.determineRoleAssignment(requestedRole);

      // Modern Cloudflare Hibernation API: accept and tag WebSocket
      this.ctx.acceptWebSocket(serverWs, [playerId, assignedRole]);

      const session: PlayerSession = {
        playerId,
        username,
        role: assignedRole,
        lastMessageTime: Date.now(),
        messageCount: 0
      };
      this.players.set(serverWs, session);

      // Send initial join packet
      serverWs.send(JSON.stringify({
        type: 'ROOM_JOINED',
        payload: {
          playerId,
          assignedRole,
          state: this.serpentState,
          orbs: Array.from(this.activeOrbs.entries()).map(([id, pos]) => ({ id, pos }))
        },
        timestamp: Date.now()
      }));

      // Ensure alarm is ticking
      const currentAlarm = await this.ctx.storage.getAlarm();
      if (!currentAlarm) {
        await this.ctx.storage.setAlarm(Date.now() + this.tickIntervalMs);
      }

      return new Response(null, { status: 101, webSocket: clientWs });
    }

    return new Response('GameRoomDO Operational', { status: 200 });
  }

  private determineRoleAssignment(requested: CoopRole): CoopRole {
    if (requested === 'solo') return 'solo';

    let hasHead = false;
    let hasTail = false;

    for (const session of this.players.values()) {
      if (session.role === 'coop_head') hasHead = true;
      if (session.role === 'coop_tail') hasTail = true;
    }

    if (requested === 'coop_head' && !hasHead) return 'coop_head';
    if (requested === 'coop_tail' && !hasTail) return 'coop_tail';
    if (!hasHead) return 'coop_head';
    if (!hasTail) return 'coop_tail';

    return 'solo';
  }

  /**
   * WebSocket Hibernation API: Called when a message arrives.
   */
  async webSocketMessage(ws: WebSocket, message: string | ArrayBuffer): Promise<void> {
    const session = this.players.get(ws);
    if (!session) return;

    // Rate Limiting (max 40 messages / sec per player)
    const now = Date.now();
    if (now - session.lastMessageTime < 1000) {
      session.messageCount++;
      if (session.messageCount > 40) {
        this.logCheatViolation(session.playerId, 'RATE_LIMIT_EXCEEDED', 'Exceeded 40 msgs/sec');
        return;
      }
    } else {
      session.lastMessageTime = now;
      session.messageCount = 1;
    }

    try {
      const text = typeof message === 'string' ? message : new TextDecoder().decode(message);
      const data: NetworkMessage = JSON.parse(text);

      switch (data.type) {
        case 'INPUT_UPDATE':
          this.handleClientInput(session, data.payload as ClientInputPayload);
          break;

        case 'EAT_PREY_REQUEST':
          this.handlePreyRequest(ws, session, data.payload as PreyConsumptionPayload);
          break;

        case 'PING':
          ws.send(JSON.stringify({ type: 'PONG', timestamp: Date.now(), clientTime: data.timestamp }));
          break;
      }
    } catch {
      // Ignore malformed packets
    }
  }

  private handleClientInput(session: PlayerSession, payload: ClientInputPayload) {
    if (session.role === 'coop_head' || session.role === 'solo') {
      this.latestHeadInput = payload;
    }

    if (session.role === 'coop_tail') {
      this.latestTailInput = payload;
    }
  }

  private handlePreyRequest(ws: WebSocket, session: PlayerSession, payload: PreyConsumptionPayload) {
    const orb = this.activeOrbs.get(payload.preyId);
    if (!orb) {
      this.logCheatViolation(session.playerId, 'IMPOSSIBLE_GROWTH', `Orb ${payload.preyId} does not exist`);
      return;
    }

    // Distance validation: head must be within collision radius
    const dx = orb.x - this.serpentState.headPosition.x;
    const dy = orb.y - this.serpentState.headPosition.y;
    const dz = orb.z - this.serpentState.headPosition.z;
    const distSq = (dx * dx) + (dy * dy) + (dz * dz);

    if (distSq > 16.0) { // Radius > 4 units is suspicious
      this.logCheatViolation(session.playerId, 'GHOST_FEEDING', `Attempted to eat orb from distance ${Math.sqrt(distSq).toFixed(1)}`);
      return;
    }

    // Validated consumption!
    this.activeOrbs.delete(payload.preyId);
    this.serpentState.segmentCount += 2;
    this.serpentState.score += 200;
    this.serpentState.hunger = Math.min(100, this.serpentState.hunger + 30);

    // Broadcast authoritative consumption
    this.broadcast({
      type: 'PREY_CONSUMED',
      payload: {
        preyId: payload.preyId,
        newSegmentCount: this.serpentState.segmentCount,
        newScore: this.serpentState.score
      },
      timestamp: Date.now()
    });

    // Respawn new orb in arena
    const newId = `orb_${Date.now()}`;
    const newPos = {
      x: (Math.random() - 0.5) * 85,
      y: 2.5 + Math.random() * 14,
      z: (Math.random() - 0.5) * 85
    };
    this.activeOrbs.set(newId, newPos);

    this.broadcast({
      type: 'ORB_SPAWNED',
      payload: { id: newId, pos: newPos },
      timestamp: Date.now()
    });
  }

  /**
   * Periodic Simulation Tick (Alarm-driven 20Hz Loop)
   */
  async alarm(): Promise<void> {
    const now = Date.now();
    const dt = Math.min((now - this.lastTickTime) / 1000, 0.1);
    this.lastTickTime = now;

    // Simulate Authoritative Serpent Mechanics
    this.simulateAuthoritativeTick(dt);

    // Broadcast Sync State to All Connected Players
    this.broadcastTickSync();

    // Schedule next tick alarm if any players connected
    if (this.ctx.getWebSockets().length > 0) {
      await this.ctx.storage.setAlarm(Date.now() + this.tickIntervalMs);
    }
  }

  private simulateAuthoritativeTick(dt: number) {
    let moveX = 0;
    let vertical = 0;
    let sprint = false;

    if (this.latestHeadInput) {
      moveX = this.latestHeadInput.moveInput.x;
      vertical = this.latestHeadInput.verticalInput;
      sprint = this.latestHeadInput.isSprinting;
    }

    // Co-op Tail Influence: Player B stabilization and tail whip
    if (this.latestTailInput) {
      if (this.latestTailInput.tailStabilizeAxis) {
        // Tail stabilizes drift by counteracting turn radius
        moveX += this.latestTailInput.tailStabilizeAxis * 0.45;
      }
      if (this.latestTailInput.tailWhipTriggered) {
        // Tail whip grants momentary surge
        sprint = true;
      }
    }

    // Speed calculation
    const baseSpeed = sprint ? 18.0 : 10.0;
    this.serpentState.isBoosting = sprint;
    this.serpentState.speed = baseSpeed;

    // Server-Authoritative Head Movement Simulation
    // Yaw steering
    const turnRate = 95.0 * dt * moveX * (Math.PI / 180);
    // Simple 2D heading update
    const currentRot = this.serpentState.headRotation;
    const currentHeading = Math.atan2(2 * (currentRot.w * currentRot.y + currentRot.x * currentRot.z), 1 - 2 * (currentRot.y * currentRot.y + currentRot.z * currentRot.z));
    const newHeading = currentHeading + turnRate;

    const halfAngle = newHeading * 0.5;
    this.serpentState.headRotation = {
      x: 0,
      y: Math.sin(halfAngle),
      z: 0,
      w: Math.cos(halfAngle)
    };

    // Forward displacement
    const forwardX = Math.sin(newHeading);
    const forwardZ = Math.cos(newHeading);

    const prevPos = { ...this.serpentState.headPosition };
    this.serpentState.headPosition.x += forwardX * this.serpentState.speed * dt;
    this.serpentState.headPosition.z += forwardZ * this.serpentState.speed * dt;
    this.serpentState.headPosition.y = Math.max(1.0, Math.min(22.0, this.serpentState.headPosition.y + (vertical * 8.0 * dt)));

    // Anti-Cheat: Validate Speed Limit
    const deltaDist = Math.sqrt(
      Math.pow(this.serpentState.headPosition.x - prevPos.x, 2) +
      Math.pow(this.serpentState.headPosition.z - prevPos.z, 2)
    );
    const maxAllowedDist = (this.serpentState.speed * dt) * 1.5; // 50% tolerance for network lag
    if (deltaDist > maxAllowedDist + 0.5) {
      this.logCheatViolation('SERVER_AUTH', 'SPEED_EXCEEDED', `Speed exceeded limit: delta=${deltaDist.toFixed(2)}`);
      this.serpentState.headPosition = prevPos; // Rollback
    }

    // Spine Vertebrae Verlet distance constraints
    const spacing = 0.85;
    let anchor = this.serpentState.headPosition;

    if (this.serpentState.segmentPositions.length !== this.serpentState.segmentCount) {
      this.serpentState.segmentPositions = this.generateInitialSpine(this.serpentState.headPosition, this.serpentState.segmentCount, spacing);
    }

    for (let i = 0; i < this.serpentState.segmentPositions.length; i++) {
      const seg = this.serpentState.segmentPositions[i];
      const dx = seg.x - anchor.x;
      const dy = seg.y - anchor.y;
      const dz = seg.z - anchor.z;
      const dist = Math.sqrt((dx * dx) + (dy * dy) + (dz * dz));

      if (dist > 0.0001) {
        seg.x = anchor.x + (dx / dist) * spacing;
        seg.y = anchor.y + (dy / dist) * spacing;
        seg.z = anchor.z + (dz / dist) * spacing;
      }
      anchor = seg;
    }

    // Metabolism
    this.serpentState.hunger = Math.max(0, this.serpentState.hunger - (1.5 * dt));
    this.serpentState.lastUpdatedTimestamp = Date.now();
  }

  private broadcastTickSync() {
    const sockets = this.ctx.getWebSockets();
    if (sockets.length === 0) return;

    const payload = JSON.stringify({
      type: 'TICK_SYNC',
      payload: this.serpentState,
      timestamp: Date.now()
    });

    for (const ws of sockets) {
      try {
        ws.send(payload);
      } catch {
        // Socket closed
      }
    }
  }

  private broadcast(msg: NetworkMessage) {
    const payload = JSON.stringify(msg);
    for (const ws of this.ctx.getWebSockets()) {
      try {
        ws.send(payload);
      } catch {
        // Socket closed
      }
    }
  }

  /**
   * WebSocket Hibernation API: Called when connection closes.
   */
  async webSocketClose(ws: WebSocket, code: number, reason: string, wasClean: boolean): Promise<void> {
    const session = this.players.get(ws);
    if (session) {
      console.log(`[GameRoomDO] Player disconnected: ${session.username} (${session.role})`);
      this.players.delete(ws);

      // Save session stats into SQLite
      this.ctx.storage.sql.exec(
        `INSERT OR REPLACE INTO game_sessions (id, created_at, updated_at, max_length, final_score, evolution_path)
         VALUES (?, ?, ?, ?, ?, ?);`,
        session.playerId,
        session.lastMessageTime,
        Date.now(),
        this.serpentState.segmentCount,
        this.serpentState.score,
        this.serpentState.evolutionPath
      );

      this.broadcast({
        type: 'PLAYER_DISCONNECTED',
        payload: { playerId: session.playerId, role: session.role },
        timestamp: Date.now()
      });
    }
  }

  async webSocketError(ws: WebSocket, error: unknown): Promise<void> {
    console.error('[GameRoomDO] WebSocket error:', error);
  }

  private logCheatViolation(playerId: string, violation: string, details: string) {
    console.warn(`[ANTI-CHEAT] ${violation} from ${playerId}: ${details}`);
    this.ctx.storage.sql.exec(
      `INSERT INTO cheat_logs (player_id, violation_type, details, timestamp) VALUES (?, ?, ?, ?);`,
      playerId,
      violation,
      details,
      Date.now()
    );
  }
}
