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

// Leaderboard Types
export interface LeaderboardEntry {
  playerId: string;
  username: string;
  score: number;
  length: number;
  evolutionPath: number;
  submittedAt: number;
}

export interface ScoreSubmissionPayload {
  score: number;
  length: number;
  evolutionPath: number;
}

// Social & Friends Types
export interface FriendInfo {
  playerId: string;
  username: string;
  addedAt: number;
  isOnline: boolean;
}

export interface PartyMemberInfo {
  playerId: string;
  username: string;
  isLeader: boolean;
  joinedAt: number;
}

export interface PartyInfo {
  partyId: string;
  leaderId: string;
  leaderName: string;
  members: PartyMemberInfo[];
  createdAt: number;
}

// Cloud Save Types
export interface CloudSaveData {
  playerId: string;
  saveVersion: number;
  saveDataJson: string;
  checksum: string;
  updatedAt: number;
}

// System Status Types
export interface VersionCheckResponse {
  clientVersion: string;
  minSupportedVersion: string;
  latestVersion: string;
  isUpdateRequired: boolean;
  isMaintenanceActive: boolean;
  maintenanceMessage?: string;
}

// Free Fire Style Account & Profile Types
export interface AccountProfile {
  playerId: string;
  numericId: number;
  username: string;
  isGuest: boolean;
  avatarId: number;
  frameId: number;
  bannerId: number;
  bio: string;
  squadName: string;
  level: number;
  exp: number;
  matchesPlayed: number;
  totalKills: number;
  highestScore: number;
  highestLength: number;
  favoriteEvolution: number;
  usernameChangesLeft: number;
  createdAt: number;
}

export interface AccountAuthResponse extends AuthResponse {
  numericId: number;
  isGuest: boolean;
  recoveryCode?: string;
  profile: AccountProfile;
}

export interface RegisterRequest {
  username: string;
  password: string;
  deviceId: string;
  turnstileToken?: string;
}

export interface LoginRequest {
  username: string;
  password: string;
  deviceId?: string;
}

export interface GuestLoginRequest {
  deviceId: string;
  turnstileToken?: string;
}

export interface UpgradeGuestRequest {
  username: string;
  password: string;
  recoveryCode: string;
}

export interface RecoverAccountRequest {
  numericIdOrUsername: string;
  recoveryCode: string;
  newPassword: string;
}

export interface ProfileUpdateRequest {
  avatarId?: number;
  frameId?: number;
  bannerId?: number;
  bio?: string;
  squadName?: string;
}

export interface PrivacySettings {
  allowInvites: number; // 0=Everyone, 1=Friends Only, 2=Nobody
  allowMessages: number; // 0=Everyone, 1=Friends Only, 2=Nobody
  showOnlineStatus: boolean;
}

export interface BlockedUser {
  blockedId: string;
  username: string;
  numericId: number;
  blockedAt: number;
}

export interface ReportPlayerRequest {
  targetId: string;
  reason: string;
  details?: string;
}

