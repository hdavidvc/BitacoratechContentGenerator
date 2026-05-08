# Database Model

The V1 schema is tenant-ready and site-scoped where content belongs to a WordPress property.

## Core Relationships

- Tenant 1-N Users
- Tenant 1-N Sites
- Site 1-1 WordPressConnection
- Site 1-N Articles
- Site 1-N Keywords
- KeywordResearchRun 1-N Keywords
- Keyword 1-N Articles
- Article 1-N ArticleSeoAnalyses
- Article 1-N ArticleImages
- Article 0-1 ArticleApproval
- Article 0-N PublishingJobs
- Article N-N Categories
- Article N-N Tags
- AiProvider 1-N AiUsageRecords
- User 1-N ArticleApprovals

## Tenant Rules

All tenant-owned tables must include `TenantId`. Site-owned content must include `SiteId`.

## Keyword Research Fields

Keywords store automated research metadata: `TrendScore`, `OpportunityScore`, `Mentions`, `Source`, `Cluster`, `Intent`, and `LastSeenAt`. Research runs track `Status` and `CompletedAt` so scheduled worker executions can be audited per tenant and site.
