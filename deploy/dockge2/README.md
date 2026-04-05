# Dockge2 deployment

## Recommended flow
1. Dockge2 hosts this `docker-compose.yml` stack.
2. GitHub Actions builds and pushes `ghcr.io/rossdargan/repaso:latest`.
3. A secure webhook on dockge2 triggers:
   - `docker compose pull`
   - `docker compose up -d`

## Notes
- Keep `.env` on dockge2, not in GitHub.
- Mount persistent volumes for `/app/data` and `/app/wwwroot/uploads`.
- If you already have a deploy-listener on dockge2, point GitHub at that listener.
- If not, use any tiny authenticated webhook runner there (for example webhook + shell script, or a small Caddy/Nginx protected endpoint that calls a local script).
