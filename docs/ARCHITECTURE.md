# BitacoraTech Architecture

BitacoraTech is a tenant-ready modular monolith using Clean Architecture.

## Dependency Flow

`Web -> Api -> Application -> Domain`

`Api -> Infrastructure -> Application -> Domain`

`Worker -> Application + Infrastructure`

## Safety Rule

Publishing is only valid when an article has explicit human approval metadata:

- approved user
- approval timestamp
- target site

The domain enforces this through `Article.CanPublish` and `Article.MarkPublishing`.

## Queues

- keywords
- articles
- seo
- media
- publishing
- maintenance

The initial implementation exposes a Hangfire-compatible scheduler abstraction. The concrete V1 adapter is named `HangfireSqlJobScheduler`; real Hangfire SQL packages can replace the in-memory job id implementation without changing Application or Domain.

