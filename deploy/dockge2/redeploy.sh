#!/usr/bin/env bash
set -euo pipefail

STACK_DIR="${STACK_DIR:-/opt/stacks/repaso}"
cd "$STACK_DIR"
docker compose pull
 docker compose up -d
