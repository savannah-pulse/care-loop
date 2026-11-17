#!/usr/bin/env bash
# Deploys CareLoop to the Azure subscription the Azure CLI is signed in to.
#
#   infra/azure/scripts/deploy.sh <environment> [--what-if]
#
# <environment> names a parameter file: infra/azure/environments/<environment>.bicepparam.
#
# First run: deploy the platform, build and push the images, deploy everything with the apps.
# Later runs: build and push the images, then one deployment of everything.
# --what-if changes nothing and shows what a deployment would change, apps included.
#
# Environment variables (all optional):
#   AZURE_LOCATION            region, default uaenorth
#   AZURE_AI_LOCATION         region of the Foundry account, where the parameter file reads it
#   NAME_PREFIX               resource name prefix, default careloop
#   POSTGRES_ADMIN_PASSWORD   letters and digits only; reused from Key Vault, or generated on the first run
#   SIMULATOR_INBOUND_SECRET  reused from Key Vault, or generated on the first run
#   IMAGE_TAG                 default <commit>-<timestamp>
#   BUILD_MODE                acr (default: build in Azure, no local Docker) or docker (build here and push)
#   API_ROLLOUT               recreate (default: stop the running API before starting the new one) or rolling
set -euo pipefail

usage() {
  sed -n '2,21p' "$0" | sed 's/^# \{0,1\}//'
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
API_ROLLOUT="${API_ROLLOUT:-recreate}"
DEPLOYMENT_NAME="$NAME_PREFIX-$ENVIRONMENT"

command -v az >/dev/null || { echo "The Azure CLI (az) is required." >&2; exit 1; }
az account show >/dev/null 2>&1 || { echo "Sign in first: az login" >&2; exit 1; }
SUBSCRIPTION_ID="$(az account show --query id -o tsv)"
echo "Subscription: $(az account show --query name -o tsv)"
echo "Environment:  $ENVIRONMENT   Region: $AZURE_LOCATION"

# --- What already exists: every resource carries the application and environment tags --------------
TAGGED="[?tags.application=='$NAME_PREFIX' && tags.environment=='$ENVIRONMENT']"
RESOURCE_GROUP="$(az group list --query "$TAGGED.name | [0]" -o tsv)"
EXISTING_LOCATION="$(az group list --query "$TAGGED.location | [0]" -o tsv)"
VAULT="$(az keyvault list --query "$TAGGED.name | [0]" -o tsv)"
REGISTRY="$(az acr list --query "$TAGGED.name | [0]" -o tsv)"
LOGIN_SERVER="$(az acr list --query "$TAGGED.loginServer | [0]" -o tsv)"

# An environment cannot move: its resource group and its subscription deployment are tied to a region.
if [ -n "$EXISTING_LOCATION" ] && [ "$EXISTING_LOCATION" != "$AZURE_LOCATION" ]; then
  echo "Environment $ENVIRONMENT already exists in $EXISTING_LOCATION, not $AZURE_LOCATION." >&2
  echo "Set AZURE_LOCATION=$EXISTING_LOCATION, or use another environment name or NAME_PREFIX for a second copy." >&2
  exit 1
fi

# --- Secrets: given, else reused from this environment's Key Vault, else generated on the first run -
from_vault() {
  [ -n "$VAULT" ] || return 0
  az keyvault secret show --vault-name "$VAULT" --name "$1" --query value -o tsv 2>/dev/null || true
}

# Upper case, lower case and digits (PostgreSQL asks for three character classes), and nothing
# that needs escaping in a connection string.
generate() { echo "Cl$(openssl rand -hex 20)Z9"; }

# resolve <variable> <Key Vault secret name>
resolve_secret() {
  local value="${!1:-}"
  [ -n "$value" ] || value="$(from_vault "$2")"
  if [ -z "$value" ]; then
    if $WHAT_IF; then
      echo "Note: $1 is not set and could not be read from Key Vault; the preview uses a placeholder, so it shows that secret as changing." >&2
      value="WhatIfPlaceholder0000"
    elif [ -n "$VAULT" ]; then
      # Never rotate by accident: a new value would change the database password under the running API.
      echo "$1 is not set and could not be read from Key Vault $VAULT (no access, or it is on a private network)." >&2
      echo "Set $1 to the current value, or to a new value to rotate it on purpose." >&2
      exit 1
    else
      value="$(generate)"
    fi
  fi
  export "$1=$value"
}
resolve_secret POSTGRES_ADMIN_PASSWORD postgres-admin-password
resolve_secret SIMULATOR_INBOUND_SECRET simulator-inbound-secret

# Lets a person read the secrets back on the next run. Empty for a service principal, which is
# expected to be given the secrets (the Deploy workflow reads them from GitHub).
if [ -z "${DEPLOYER_PRINCIPAL_ID:-}" ]; then
  DEPLOYER_PRINCIPAL_ID="$(az ad signed-in-user show --query id -o tsv 2>/dev/null || true)"
fi
export DEPLOYER_PRINCIPAL_ID

TAG="${IMAGE_TAG:-$(git -C "$REPO_ROOT" rev-parse --short=12 HEAD)-$(date -u +%Y%m%d%H%M%S)}"

# --- Preview only ----------------------------------------------------------------------------------
if $WHAT_IF; then
  # With an image tag, so the container apps are part of the preview.
  export IMAGE_TAG="$TAG"
  az deployment sub what-if --name "$DEPLOYMENT_NAME" --location "$AZURE_LOCATION" --parameters "$PARAM_FILE"
  exit 0
fi

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

# --- Check the whole template before anything is built or changed ----------------------------------
# The check also returns every parameter with its defaults applied, which says which images are needed.
echo
echo "Checking the deployment ..."
export IMAGE_TAG="$TAG"
SWITCHES="$(az deployment sub validate --name "$DEPLOYMENT_NAME" --location "$AZURE_LOCATION" \
  --parameters "$PARAM_FILE" --only-show-errors -o tsv \
  --query "[properties.parameters.deployAi.value, properties.parameters.deploySimulators.value]" | tr '\n' ' ')"
# shellcheck disable=SC2086
set -- $SWITCHES
DEPLOY_AI="${1:-}"
DEPLOY_SIMULATORS="${2:-}"
[ -n "$DEPLOY_SIMULATORS" ] || { echo "Could not read the deployment's parameters." >&2; exit 1; }

# --- Platform, first run only: the images need a registry before the apps can be deployed ----------
if [ -z "$REGISTRY" ]; then
  echo
  echo "Deploying the platform (registry, database, Key Vault, Container Apps environment) ..."
  export IMAGE_TAG=""
  deploy
  RESOURCE_GROUP="$(output resourceGroupName)"
  REGISTRY="$(output registryName)"
  LOGIN_SERVER="$(output registryLoginServer)"
fi

# --- Images ----------------------------------------------------------------------------------------
# name | Dockerfile | build context, as in docker-compose.yml
IMAGES="api|src/api/CareLoop.Api/Dockerfile|.
web|src/web/Dockerfile|src/web"
if [ "$DEPLOY_AI" = "true" ]; then
  IMAGES="$IMAGES
ai-workflow|src/ai_workflow/Dockerfile|src/ai_workflow"
fi
if [ "$DEPLOY_SIMULATORS" = "true" ]; then
  IMAGES="$IMAGES
his-simulator|src/simulators/his/Dockerfile|.
messaging-simulator|src/simulators/messaging/Dockerfile|."
fi

echo
echo "Building images with tag $TAG ($BUILD_MODE) ..."
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

# --- Stop the running API --------------------------------------------------------------------------
# Container Apps starts a new revision beside the old one and switches over when it is healthy. For
# the API that means two copies of the deadline worker and the outbox for a while, and the old copy
# running against a database the new one has just migrated. So the old revision is stopped first.
# The API is down from here until the new revision is healthy; API_ROLLOUT=rolling skips this.
if [ "$API_ROLLOUT" = "recreate" ]; then
  REVISIONS_URL="https://management.azure.com/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.App/containerApps/api/revisions"
  ACTIVE="$(az rest --method get --url "$REVISIONS_URL?api-version=2024-03-01" \
    --query "value[?properties.active].name" -o tsv 2>/dev/null || true)"
  for revision in $ACTIVE; do
    echo
    echo "Stopping API revision $revision ..."
    az rest --method post --url "$REVISIONS_URL/$revision/deactivate?api-version=2024-03-01" --output none
  done
fi

# --- Everything, with the apps on the new images ---------------------------------------------------
echo
echo "Deploying ..."
export IMAGE_TAG="$TAG"
deploy

echo
echo "Done. Resource group: $(output resourceGroupName)"
echo "  Dashboard:            $(output webUrl)"
for item in apiUrl:API hisSimulatorUrl:"HIS simulator" messagingSimulatorUrl:"Messaging simulator"; do
  url="$(output "${item%%:*}")"
  [ -z "$url" ] || printf '  %-21s %s\n' "${item#*:}:" "$url"
done
