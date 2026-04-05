# Repaso

A lightweight Spanish vocabulary practice app designed for one learner.

> Note: this project is, in the finest possible sense, totally vibe coded.

## What it does
- Upload a Word `.docx` vocabulary list
- Preview imports before saving
- Ask questions in either English or Spanish
- Accept typed answers
- Detect close matches / likely spelling mistakes
- Let the learner say: **I knew it, just spelt it wrong**
- Track exact answers, typo-saved answers, wrong answers, and weak words
- Show missed words more often

## Tech stack
- ASP.NET Core 8 minimal API
- SQLite with EF Core
- Static frontend served by ASP.NET Core
- Docker for packaging
- GitHub Actions for image build/publish

## Local development
You need the .NET 8 SDK installed.

```bash
dotnet restore SpanishPractice.sln
dotnet run --project src/SpanishPractice.Api/SpanishPractice.Api.csproj
```

Then open:
- `http://localhost:5174` in development

## Docker
```bash
docker build -t repaso .
docker run --rm -p 8080:8080 \
  -e ConnectionStrings__DefaultConnection='Data Source=/app/data/repaso.db' \
  -v $(pwd)/data:/app/data \
  -v $(pwd)/uploads:/app/wwwroot/uploads \
  repaso
```

## Environment variables
See `.env.example`.

Important ones:
- `ConnectionStrings__DefaultConnection`
- `App__UploadsPath`
- `App__DeployWebhookSecret`
- `App__StrictAccents`

## Deployment
Recommended public-repo-safe path:
1. Push repo to GitHub.
2. GitHub Actions builds and pushes image to GHCR.
3. dockge2 runs `docker-compose.yml` using the GHCR image.
4. GitHub Actions triggers a secure deploy webhook on dockge2.

See:
- `docs/architecture.md`
- `deploy/dockge2/README.md`

## GitHub secrets to set
- `DEPLOY_WEBHOOK_URL`
- `DEPLOY_WEBHOOK_SECRET`

## Notes
- Repo is safe for public GitHub if you keep secrets out of it.
- SQLite data and uploaded files should live in mounted volumes.
- The built-in `/api/deploy/webhook` endpoint only validates the secret and acknowledges the request. In production, you will usually point GitHub at a dockge2-side webhook listener that runs the redeploy script.
