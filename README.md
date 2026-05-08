# BitacoraTech Automation Platform

SaaS platform for SEO automation, AI article generation, human review, and WordPress publishing.

## Stack

- ASP.NET Core 9
- Angular 20
- SQL Server
- Docker
- WordPress REST API
- Gemini/OpenAI/Claude/OpenRouter through provider abstractions

## Build

```powershell
dotnet restore BitacoraTech.sln
dotnet build BitacoraTech.sln --no-restore
```

The Angular project is scaffolded for Angular 20, but this machine currently has Node 12/npm permission issues. Use Node 22+ before running `npm install`.

