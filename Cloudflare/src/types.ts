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

export interface FriendRequestInfo {
  id: number;
  senderId: string;
  senderName: string;
  senderNumericId: number;
  receiverId: string;
  receiverName: string;
  status: 'pending' | 'accepted' | 'rejected';
  createdAt: number;
}

export interface FriendDetailInfo {
  playerId: string;
  username: string;
  numericId: number;
  isOnline: boolean;
  status: 'online' | 'in_match' | 'offline';
  lastLoginAt: number;
  addedAt: number;
  currentSquadId?: string;
  squadTag?: string;
  clanTag?: string;
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

// Free Fire Squad Types (4-5 players)
export interface SquadMemberInfo {
  playerId: string;
  username: string;
  numericId: number;
  role: 'leader' | 'member';
  isReady: boolean;
  joinedAt: number;
}

export interface SquadChatMessage {
  id: number;
  squadId: string;
  senderId: string;
  senderName: string;
  message: string;
  sentAt: number;
}

export interface SquadInviteInfo {
  id: number;
  squadId: string;
  squadName: string;
  squadTag: string;
  inviterId: string;
  inviterName: string;
  status: 'pending' | 'accepted' | 'rejected';
  createdAt: number;
}

export interface SquadInfo {
  squadId: string;
  name: string;
  tag: string;
  leaderId: string;
  leaderName: string;
  maxMembers: number;
  isOpen: boolean;
  squadXp: number;
  squadLevel: number;
  matchRoomId?: string;
  members: SquadMemberInfo[];
  createdAt: number;
}

// Free Fire Clan / Guild Types (up to 50 players)
export interface ClanMemberInfo {
  playerId: string;
  username: string;
  numericId: number;
  rank: 'leader' | 'officer' | 'member';
  contribution: number;
  isOnline: boolean;
  joinedAt: number;
}

export interface ClanChatMessage {
  id: number;
  clanId: string;
  senderId: string;
  senderName: string;
  senderRank: string;
  message: string;
  sentAt: number;
}

export interface ClanJoinRequestInfo {
  id: number;
  clanId: string;
  playerId: string;
  username: string;
  numericId: number;
  level: number;
  status: 'pending' | 'accepted' | 'rejected';
  createdAt: number;
}

export interface ClanInfo {
  clanId: string;
  name: string;
  tag: string;
  leaderId: string;
  leaderName: string;
  description: string;
  announcement: string;
  badgeId: number;
  memberCount: number;
  maxMembers: number;
  minLevel: number;
  isOpen: boolean;
  clanXp: number;
  clanLevel: number;
  members?: ClanMemberInfo[];
  onlineCount?: number;
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

// ==========================================
// REAL-TIME MULTI-CHANNEL CHAT SYSTEM
// ==========================================
export type ChatChannel = 'global' | 'squad' | 'clan' | 'match' | 'whisper' | 'system';

export interface ChatMessage {
  id: number;
  channel: ChatChannel;
  senderId: string;
  senderName: string;
  senderNumericId: number;
  clanTag?: string;
  squadTag?: string;
  recipientId?: string;
  recipientName?: string;
  targetGroupId?: string;
  message: string;
  timestamp: number;
  isMention?: boolean;
}

export interface SendChatRequest {
  channel: ChatChannel;
  message: string;
  recipientId?: string;
  recipientNumericId?: number;
  recipientName?: string;
  squadId?: string;
  clanId?: string;
  roomId?: string;
}

export interface ChatHistoryResponse {
  success: boolean;
  channel: ChatChannel;
  messages: ChatMessage[];
  error?: string;
}

// ==========================================
// HIGH-QUALITY REAL-TIME VOICE CHAT SYSTEM
// ==========================================
export type VoiceChannel = 'squad' | 'match' | 'off';
export type VoiceSignalType = 'offer' | 'answer' | 'candidate' | 'join' | 'leave' | 'state';

export interface VoiceUserState {
  playerId: string;
  username: string;
  numericId: number;
  channel: VoiceChannel;
  isSpeaking: boolean;
  isMuted: boolean;
  isDeafened: boolean;
  volumeLevel: number; // 0.0 to 1.0 (RMS audio meter)
  squadId?: string;
  roomId?: string;
}

export interface VoiceSignalPayload {
  type: VoiceSignalType;
  senderId: string;
  senderName?: string;
  senderNumericId?: number;
  targetPeerId?: string;
  channel: VoiceChannel;
  squadId?: string;
  roomId?: string;
  sdp?: string;
  candidate?: any;
  state?: Partial<VoiceUserState>;
  timestamp: number;
}

export interface VoiceAudioPacket {
  senderId: string;
  senderName: string;
  channel: VoiceChannel;
  squadId?: string;
  roomId?: string;
  sequence: number;
  timestamp: number;
  audioBase64: string;
  position?: Vector3Data; // 3D spatial position for proximity rolloff
}

// ==========================================
// NOTIFICATION & LOBBY QUALITY SYSTEM
// ==========================================
export type NotificationType = 'friend_request' | 'squad_invite' | 'clan_invite' | 'whisper' | 'system';
export type PresenceStatus = 'online' | 'away' | 'in_match' | 'dnd' | 'invisible' | 'offline';
export type LobbyReaction = 'fire' | 'serpent' | 'crown' | 'gg' | 'heart' | 'lightning' | 'rip' | 'shield';

export interface AppNotification {
  id: number;
  playerId: string;
  type: NotificationType;
  title: string;
  message: string;
  senderId?: string;
  senderName?: string;
  senderNumericId?: number;
  dataJson?: string;
  isRead: boolean;
  createdAt: number;
}

export interface LobbyReactionPayload {
  senderId: string;
  senderName: string;
  senderNumericId: number;
  reaction: LobbyReaction;
  squadId?: string;
  timestamp: number;
}



