export interface JobResponse {
  jobId: string;
  queue: string;
  status: string;
}

export interface ArticleSummaryResponse {
  id: string;
  siteId: string;
  title: string;
  status: string;
  seoScore: number | null;
}

export interface ArticleDetailResponse {
  id: string;
  siteId: string;
  primaryKeywordId: string | null;
  title: string;
  htmlContent: string;
  status: string;
  seoScore: number | null;
  wordPressPostId: string | null;
}

export const enum WordPressPublicationMode {
  Draft = 0,
  Publish = 1,
  Schedule = 2
}

export interface PublishArticleRequest {
  siteId: string;
  mode: WordPressPublicationMode;
  scheduledFor?: string | null;
  categories?: string[] | null;
  tags?: string[] | null;
  featuredImageUrl?: string | null;
  featuredImageAltText?: string | null;
  slug?: string | null;
  excerpt?: string | null;
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

export interface ArticlePreviewModel {
  id: string;
  siteId: string;
  title: string;
  excerpt: string;
  htmlContent: string;
  status: string;
  seoScore: number;
  primaryKeyword: string;
  updatedAt: string;
  wordPressPostId?: string | null;
}

export interface SeoAnalyzeRequest {
  articleId?: string;
  title: string;
  htmlContent: string;
  primaryKeyword: string;
}

export interface SeoInternalLinkSuggestion {
  anchorText: string;
  slug: string;
  reason: string;
}

export interface SeoFaqItem {
  question: string;
  answer: string;
}

export interface SeoReadabilityMetrics {
  wordCount: number;
  sentenceCount: number;
  paragraphCount: number;
  readingEase: number;
  gradeLevel: string;
}

export interface SeoAnalyzeResponse {
  score: number;
  keywordDensity: number;
  readability: string;
  recommendations: string[];
  semanticKeywords: string[];
  internalLinks: SeoInternalLinkSuggestion[];
  faqs: SeoFaqItem[];
  faqSchema: string;
  jsonLd: string;
  readabilityMetrics: SeoReadabilityMetrics;
}
