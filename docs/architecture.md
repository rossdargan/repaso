# Architecture

## Stack
- Frontend: static HTML/CSS/JS served by ASP.NET Core
- Backend: ASP.NET Core 8 minimal API
- Persistence: SQLite via EF Core
- Import: `.docx` parsing with OpenXML
- Delivery: Docker container built in GitHub Actions and pushed to GHCR

## Core flows
1. Upload `.docx` file.
2. Parse table rows or `english - spanish` lines.
3. Preview imported pairs.
4. Commit words into SQLite.
5. Serve weighted quiz questions.
6. Record exact / typo-saved / wrong attempts.
7. Increase priority for frequently missed words.

## Weighting
Words are selected using a simple weighted priority:
- wrong answers add strong weight
- typo-saved answers add moderate weight
- unseen/recently missed words get a bonus
- long correct streaks reduce frequency

This is deliberately simple enough to maintain in C# without extra infrastructure.
