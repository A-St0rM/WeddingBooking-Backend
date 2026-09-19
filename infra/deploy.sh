#!/usr/bin/env bash
# Deploys the API and the frontend to Azure. Run it again to redeploy; it is
# idempotent. This is the whole deployment procedure — there is no sequence of
# portal clicks to remember.
#
#   ./infra/deploy.sh
#
# Expects:
#   WEDDINGBOOKING_POSTGRES   Npgsql connection string for Supabase
#   WEDDINGBOOKING_SUPABASE   Supabase project URL
# and the Azure CLI, signed in (az login).

set -euo pipefail

RESOURCE_GROUP="${RESOURCE_GROUP:-rg-weddingbooking}"
LOCATION="${LOCATION:-northeurope}"
BACKEND_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
FRONTEND_DIR="${FRONTEND_DIR:-$(cd "$BACKEND_DIR/../WeddingBooking" && pwd)}"

: "${WEDDINGBOOKING_POSTGRES:?Set WEDDINGBOOKING_POSTGRES to the Npgsql connection string}"
: "${WEDDINGBOOKING_SUPABASE:?Set WEDDINGBOOKING_SUPABASE to the Supabase project URL}"

command -v az >/dev/null || { echo "Azure CLI not installed: brew install azure-cli"; exit 1; }
[ -d "$FRONTEND_DIR" ] || { echo "Frontend repo not found at $FRONTEND_DIR"; exit 1; }

echo "==> Resource group"
az group create --name "$RESOURCE_GROUP" --location "$LOCATION" --output none

echo "==> Infrastructure"
outputs=$(az deployment group create \
  --resource-group "$RESOURCE_GROUP" \
  --template-file "$BACKEND_DIR/infra/main.bicep" \
  --parameters \
      postgresConnectionString="$WEDDINGBOOKING_POSTGRES" \
      supabaseUrl="$WEDDINGBOOKING_SUPABASE" \
  --query properties.outputs --output json)

api_name=$(echo "$outputs" | python3 -c 'import json,sys; print(json.load(sys.stdin)["apiName"]["value"])')
api_url=$(echo "$outputs" | python3 -c 'import json,sys; print(json.load(sys.stdin)["apiUrl"]["value"])')
frontend_name=$(echo "$outputs" | python3 -c 'import json,sys; print(json.load(sys.stdin)["frontendName"]["value"])')
frontend_url=$(echo "$outputs" | python3 -c 'import json,sys; print(json.load(sys.stdin)["frontendUrl"]["value"])')

echo "    API:      $api_url"
echo "    Frontend: $frontend_url"

echo "==> Publishing the API"
publish_dir=$(mktemp -d)
zip_path=$(mktemp -t weddingbooking-api).zip
dotnet publish "$BACKEND_DIR/src/WeddingBooking.Api" -c Release -o "$publish_dir" --nologo -v q
# The archive is written outside the directory being archived, or it ends up inside itself.
(cd "$publish_dir" && zip -qr "$zip_path" .)
az webapp deploy \
  --resource-group "$RESOURCE_GROUP" \
  --name "$api_name" \
  --src-path "$zip_path" \
  --type zip --output none
rm -rf "$publish_dir" "$zip_path"

echo "==> Building the frontend against $api_url"
(cd "$FRONTEND_DIR" && VITE_API_BASE_URL="$api_url" npm run build)

echo "==> Publishing the frontend"
token=$(az staticwebapp secrets list \
  --name "$frontend_name" --resource-group "$RESOURCE_GROUP" \
  --query properties.apiKey --output tsv)
(cd "$FRONTEND_DIR" && swa deploy dist --deployment-token "$token" --env production)

echo
echo "==> Checking the API is actually up"
for _ in $(seq 1 12); do
  if curl -fsS --max-time 10 "$api_url/health" >/dev/null 2>&1; then
    echo "    $api_url/health OK"
    break
  fi
  echo "    waiting for the API to start..."
  sleep 10
done

echo
echo "Done."
echo "  Frontend: $frontend_url"
echo "  API:      $api_url"
