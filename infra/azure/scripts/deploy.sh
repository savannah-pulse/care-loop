#!/usr/bin/env bash
# Deploys CareLoop to the Azure subscription the Azure CLI is signed in to.
#
#   infra/azure/scripts/deploy.sh <environment> [--what-if]
#
# <environment> names a parameter file: infra/azure/environments/<environment>.bicepparam.
#
# Three steps: deploy the platform, build and push the images, deploy the container apps.
# Safe to run again; each run builds a new image tag and rolls the apps on to it.
#
# Environment variables (all optional):
#   AZURE_LOCATION            region, default uaenorth
#   AZURE_AI_LOCATION         region of the Foundry account, where the parameter file reads it
#   NAME_PREFIX               resource name prefix, default careloop
#   POSTGRES_ADMIN_PASSWORD   letters and digits only; reused from Key Vault or generated when unset
#   SIMULATOR_INBOUND_SECRET  reused from Key Vault or generated when unset
#   IMAGE_TAG                 default <commit>-<timestamp>
#   BUILD_MODE                acr (default: build in Azure, no local Docker) or docker (build here and push)
set -euo pipefail

usage() {
  sed -n '2,19p' "$0" | sed 's/^# \{0,1\}//'
  exit 1
}

ENVIRONMENT="${1:-}"
[ -n "$ENVIRONMENT" ] || usage
WHAT_IF=false
[ "${2:-}" = "--what-if" ] && WHAT_IF=true

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
INFRA_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
REPO_ROOT="$(cd "$INFRA_DIR/../.." && pwd)"
PARAM_FILE="$INFRA_DIR/environments/$ENVIRONMENT.bicepparam"
[ -f "$PARAM_FILE" ] || { echo "No parameter file: $PARAM_FILE" >&2; exit 1; }

export AZURE_LOCATION="${AZURE_LOCATION:-uaenorth}"
export NAME_PREFIX="${NAME_PREFIX:-careloop}"
BUILD_MODE="${BUILD_MODE:-acr}"
DEPLOYMENT_NAME="$NAME_PREFIX-$ENVIRONMENT"

command -v az >/dev/null || { echo "The Azure CLI (az) is required." >&2; exit 1; }
az account show >/dev/null 2>&1 || { echo "Sign in first: az login" >&2; exit 1; }
echo "Subscription: $(az account show --query name -o tsv)"
echo "Environment:  $ENVIRONMENT   Region: $AZURE_LOCATION"

# --- Resource providers: a new subscription has none of these registered ---------------------------
for provider in Microsoft.App Microsoft.ContainerRegistry Microsoft.DBforPostgreSQL Microsoft.KeyVault \
  Microsoft.ManagedIdentity Microsoft.Network Microsoft.OperationalInsights Microsoft.Insights \
  Microsoft.CognitiveServices; do
  state="$(az provider show --namespace "$provider" --query registrationState -o tsv 2>/dev/null || true)"
  if [ "$state" != "Registered" ]; then
    echo "Registering $provider ..."
    az provider register --namespace "$provider" --wait
  fi
done

# --- Secrets: given, else reused from this environment's Key Vault, else generated -----------------
VAULT="$(az keyvault list \
  --query "[?tags.application=='$NAME_PREFIX' && tags.environment=='$ENVIRONMENT'].name | [0]" -o tsv 2>/dev/null || true)"

from_vault() {
  [ -n "$VAULT" ] || return 0
  az keyvault secret show --vault-name "$VAULT" --name "$1" --query value -o tsv 2>/dev/null || true
}

# Upper case, lower case and digits (PostgreSQL asks for three character classes), and nothing
# that needs escaping in a connection string.
generate() { echo "Cl$(openssl rand -hex 20)Z9"; }

if [ -z "${POSTGRES_ADMIN_PASSWORD:-}" ]; then
  POSTGRES_ADMIN_PASSWORD="$(from_vault postgres-admin-password)"
  if [ -z "$POSTGRES_ADMIN_PASSWORD" ]; then
    [ -z "$VAULT" ] || echo "Could not read the database password from $VAULT; generating a new one (this rotates it)."
    POSTGRES_ADMIN_PASSWORD="$(generate)"
  fi
fi
if [ -z "${SIMULATOR_INBOUND_SECRET:-}" ]; then
  SIMULATOR_INBOUND_SECRET="$(from_vault simulator-inbound-secret)"
  [ -n "$SIMULATOR_INBOUND_SECRET" ] || SIMULATOR_INBOUND_SECRET="$(generate)"
fi
export POSTGRES_ADMIN_PASSWORD SIMULATOR_INBOUND_SECRET

# Lets a person read the secrets back on the next run. Empty for a service principal, which is
# expected to be given the secrets (the Deploy workflow reads them from GitHub).
if [ -z "${DEPLOYER_PRINCIPAL_ID:-}" ]; then
  DEPLOYER_PRINCIPAL_ID="$(az ad signed-in-user show --query id -o tsv 2>/dev/null || true)"
fi
export DEPLOYER_PRINCIPAL_ID

deploy() {
  az deployment sub create \
    --name "$DEPLOYMENT_NAME" \
    --location "$AZURE_LOCATION" \
    --parameters "$PARAM_FILE" \
    --output none
}

output() {
  az deployment sub show --name "$DEPLOYMENT_NAME" --query "properties.outputs.$1.value" -o tsv
}

if $WHAT_IF; then
  export IMAGE_TAG="${IMAGE_TAG:-}"
  az deployment sub what-if --name "$DEPLOYMENT_NAME" --location "$AZURE_LOCATION" --parameters "$PARAM_FILE"
  exit 0
fi

# --- 1. Platform -----------------------------------------------------------------------------------
TAG="${IMAGE_TAG:-$(git -C "$REPO_ROOT" rev-parse --short=12 HEAD)-$(date -u +%Y%m%d%H%M%S)}"

echo
echo "1/3 Deploying the platform (registry, database, Key Vault, Container Apps environment) ..."
export IMAGE_TAG=""
deploy

REGISTRY="$(output registryName)"
LOGIN_SERVER="$(output registryLoginServer)"

# --- 2. Images -------------------------------------------------------------------------------------
# name | Dockerfile | build context, as in docker-compose.yml
IMAGES="api|src/api/CareLoop.Api/Dockerfile|.
web|src/web/Dockerfile|src/web"
if [ "$(output deployAi)" = "true" ]; then
  IMAGES="$IMAGES
ai-workflow|src/ai_workflow/Dockerfile|src/ai_workflow"
fi
if [ "$(output deploySimulators)" = "true" ]; then
  IMAGES="$IMAGES
his-simulator|src/simulators/his/Dockerfile|.
messaging-simulator|src/simulators/messaging/Dockerfile|."
fi

echo
echo "2/3 Building images with tag $TAG ($BUILD_MODE) ..."
[ "$BUILD_MODE" = "docker" ] && az acr login --name "$REGISTRY"

cd "$REPO_ROOT"
echo "$IMAGES" | while IFS='|' read -r name dockerfile context; do
  image="careloop/$name:$TAG"
  echo "  $image"
  if [ "$BUILD_MODE" = "docker" ]; then
    docker buildx build --platform linux/amd64 --file "$dockerfile" --tag "$LOGIN_SERVER/$image" --push "$context"
  else
    az acr build --registry "$REGISTRY" --image "$image" --file "$dockerfile" --platform linux/amd64 \
      --only-show-errors --output none "$context" </dev/null
  fi
done

# --- 3. Apps ---------------------------------------------------------------------------------------
echo
echo "3/3 Deploying the container apps ..."
export IMAGE_TAG="$TAG"
deploy

echo
echo "Done. Resource group: $(output resourceGroupName)"
echo "  Dashboard:            $(output webUrl)"
for item in apiUrl:API hisSimulatorUrl:"HIS simulator" messagingSimulatorUrl:"Messaging simulator"; do
  url="$(output "${item%%:*}")"
  [ -z "$url" ] || printf '  %-21s %s\n' "${item#*:}:" "$url"
done
