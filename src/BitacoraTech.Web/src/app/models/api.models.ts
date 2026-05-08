export interface JobResponse {
  jobId: string;
  queue: string;
  status: string;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest {
  tenantName: string;
  email: string;
  password: string;
}

export interface RefreshTokenRequest {
  refreshToken: string;
}

export interface LogoutRequest {
  refreshToken: string;
}

export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  accessTokenExpiresAt: string;
  refreshTokenExpiresAt: string;
  userId: string;
  tenantId: string;
  email: string;
  roles: string[];
}

export interface KeywordResponse {
  id: string;
  siteId: string;
  keyword: string;
  searchVolume: number;
  difficulty: number;
  trendScore: number;
  opportunityScore: number;
  mentions: number;
  source: string;
  cluster: string;
  intent: string;
  lastSeenAt: string;
}

export interface ResearchKeywordRequest {
  siteId: string;
  seedKeyword: string;
  country?: string;
  language?: string;
  subreddits?: string[];
  rssFeeds?: string[];
}

export interface KeywordClusterResponse {
  name: string;
  keywordCount: number;
  averageOpportunityScore: number;
  keywords: KeywordResponse[];
}
