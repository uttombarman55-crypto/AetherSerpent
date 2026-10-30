export interface Vector3Data {
  x: number;
  y: number;
  z: number;
}

export interface QuaternionData {
  x: number;
  y: number;
  z: number;
  w: number;
}

export type CoopRole = 'solo' | 'coop_head' | 'coop_tail';

export interface SerpentNetworkState {
  headPosition: Vector3Data;
  headRotation: QuaternionData;
  speed: number;
  isBoosting: boolean;
  evolutionPath: number; // 0=Primal, 1=Titan, 2=Celestial, 3=Abyssal
  segmentCount: number;
  segmentPositions: Vector3Data[];
  health: number;
  hunger: number;
  energy: number;
  score: number;
  lastUpdatedTimestamp: number;
}

export interface ClientInputPayload {
  role: CoopRole;
  moveInput: { x: number; y: number };
  verticalInput: number;
  isSprinting: boolean;
  isAbilityTriggered: boolean;
  // Tail-specific inputs for Player B
  tailWhipTriggered?: boolean;
  tailStabilizeAxis?: number;
  clientTimestamp: number;
}

export interface PreyConsumptionPayload {
  preyId: string;
  preyType: number;
  claimedPosition: Vector3Data;
  clientTimestamp: number;
}

// Network Message envelope
export interface NetworkMessage<T = unknown> {
  type: string;
  payload: T;
  timestamp: number;
  senderId?: string;
}

// Authentication types
export interface JWTPayload {
  sub: string; // Player ID
  username: string;
  role: string;
  iat: number;
  exp: number;
}

export interface AuthResponse {
  success: boolean;
  accessToken: string;
  refreshToken: string;
  playerId: string;
  username: string;
  expiresIn: number;
}

export interface MatchmakingResponse {
  success: boolean;
  roomId: string;
  assignedRole: CoopRole;
  wsUrl: string;
}
