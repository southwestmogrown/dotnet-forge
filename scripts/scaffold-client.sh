#!/usr/bin/env bash
set -euo pipefail

# Update this URL to point at your own fork / organisation repo
TEMPLATE_REPO="https://github.com/yourname/dotnet-forge"

echo "╔══════════════════════════════════╗"
echo "║   dotnet-forge client scaffolder ║"
echo "╚══════════════════════════════════╝"

read -rp "Client name (slug, no spaces): " CLIENT_NAME
read -rp "Postgres password: " -s DB_PASS; echo
read -rp "JWT secret (min 32 chars): " -s JWT_SECRET; echo
read -rp "API port [5000]: " API_PORT
API_PORT="${API_PORT:-5000}"

TARGET_DIR="${CLIENT_NAME}-api"
git clone "$TEMPLATE_REPO" "$TARGET_DIR"
cd "$TARGET_DIR"

# Rename solution files (portable across GNU and BSD sed)
if sed --version >/dev/null 2>&1; then
  # GNU sed
  find . -name "*.sln" -exec sed -i "s/dotnet-forge/${CLIENT_NAME}/g" {} \;
  find . -name "*.csproj" -exec sed -i "s/dotnet-forge/${CLIENT_NAME}/g" {} \;
else
  # BSD sed (macOS)
  find . -name "*.sln" -exec sed -i '' "s/dotnet-forge/${CLIENT_NAME}/g" {} \;
  find . -name "*.csproj" -exec sed -i '' "s/dotnet-forge/${CLIENT_NAME}/g" {} \;
fi

# Write .env
cat > .env <<EOF
ASPNETCORE_ENVIRONMENT=Production
API_PORT=${API_PORT}
POSTGRES_DB=${CLIENT_NAME}
POSTGRES_USER=${CLIENT_NAME}_user
POSTGRES_PASSWORD=${DB_PASS}
DB_PORT=5432
JWT_SECRET=${JWT_SECRET}
JWT_ISSUER=${CLIENT_NAME}-api
JWT_AUDIENCE=${CLIENT_NAME}-clients
EOF

echo ""
echo "✓ Scaffolded → ./${TARGET_DIR}"
echo "  Next: cd ${TARGET_DIR} && docker compose up --build"
