# API Surface

Base path: `/api/v1`

- `POST /auth/register`
- `POST /auth/login`
- `POST /auth/refresh`
- `POST /auth/logout`
- `GET /articles`
- `GET /articles/{id}`
- `POST /articles/generate`
- `POST /articles/{id}/seo/analyze`
- `POST /articles/{id}/approve`
- `POST /articles/{id}/publish`
- `GET /keywords`
- `GET /keywords/clusters`
- `POST /keywords/research`
- `POST /seo/analyze`
- `GET /sites`
- `POST /sites`
- `PUT /sites/{id}/wordpress-settings`

## Keyword Research

`POST /keywords/research` runs a complete trend pass for a site. It accepts:

```json
{
  "siteId": "00000000-0000-0000-0000-000000000000",
  "seedKeyword": "ai automation",
  "country": "US",
  "language": "en-US",
  "subreddits": [ "technology", "SEO" ],
  "rssFeeds": [ "https://example.com/feed.xml" ]
}
```

The research engine combines Google Trends RSS, Reddit posts, configured RSS feeds, scoring, intent inference, and clustering. `GET /keywords/clusters` returns grouped opportunities ordered by average opportunity score.
