#!/usr/bin/env bash
# One-time setup, per Azure subscription and GitHub environment, that lets the Deploy workflow sign
# in to Azure without a stored secret (OpenID Connect).
#
#   infra/azure/scripts/setup-github-oidc.sh <owner/repo> <environment>
#
# Run it signed in (az login) to the target subscription as someone who may create app
# registrations and assign roles (Owner, or Contributor plus User Access Administrator).
#
# It creates an Entra ID app registration that trusts GitHub runs of <owner/repo> in the GitHub
# environment <environment>, and gives it two roles on the subscription:
#   Contributor                              create the resources
#   Role Based Access Control Administrator  give the managed identities their roles, limited by a
#                                            condition to the four roles the deployment assigns
# It then prints the values to store in the GitHub environment.
set -euo pipefail

REPO="${1:-}"
ENVIRONMENT="${2:-}"
if [ -z "$REPO" ] || [ -z "$ENVIRONMENT" ]; then
  sed -n '2,15p' "$0" | sed 's/^# \{0,1\}//'
  exit 1
fi

SUBSCRIPTION_ID="$(az account show --query id -o tsv)"
TENANT_ID="$(az account show --query tenantId -o tsv)"
APP_NAME="careloop-deploy-$ENVIRONMENT"

echo "Subscription: $(az account show --query name -o tsv) ($SUBSCRIPTION_ID)"

CLIENT_ID="$(az ad app list --display-name "$APP_NAME" --query '[0].appId' -o tsv)"
if [ -z "$CLIENT_ID" ]; then
  CLIENT_ID="$(az ad app create --display-name "$APP_NAME" --query appId -o tsv)"
  echo "Created app registration $APP_NAME"
fi
az ad sp show --id "$CLIENT_ID" >/dev/null 2>&1 || az ad sp create --id "$CLIENT_ID" --output none
PRINCIPAL_ID="$(az ad sp show --id "$CLIENT_ID" --query id -o tsv)"

if ! az ad app federated-credential list --id "$CLIENT_ID" --query '[].name' -o tsv | grep -qx "github-$ENVIRONMENT"; then
  az ad app federated-credential create --id "$CLIENT_ID" --output none --parameters "{
    \"name\": \"github-$ENVIRONMENT\",
    \"issuer\": \"https://token.actions.githubusercontent.com\",
    \"subject\": \"repo:$REPO:environment:$ENVIRONMENT\",
    \"audiences\": [\"api://AzureADTokenExchange\"]
  }"
fi

SCOPE="/subscriptions/$SUBSCRIPTION_ID"

az role assignment create --assignee-object-id "$PRINCIPAL_ID" --assignee-principal-type ServicePrincipal \
  --role "Contributor" --scope "$SCOPE" --output none

# The deployment gives the managed identities four roles and nothing else, so the deploy identity
# may assign or remove only those. Without this condition it could make itself Owner.
#   AcrPull, Key Vault Secrets User, Key Vault Secrets Officer, Foundry User
ASSIGNABLE="7f951dda-4ed3-4680-a7ca-43fe172d538d, 4633458b-17de-408a-b874-0445c86b69e6, b86a8fe4-44ce-4948-aee5-eccb2c155cd7, 53ca6127-db72-4b80-b1b0-d745d6d5456d"
CONDITION="((!(ActionMatches{'Microsoft.Authorization/roleAssignments/write'})) OR (@Request[Microsoft.Authorization/roleAssignments:RoleDefinitionId] ForAnyOfAnyValues:GuidEquals {$ASSIGNABLE})) AND ((!(ActionMatches{'Microsoft.Authorization/roleAssignments/delete'})) OR (@Resource[Microsoft.Authorization/roleAssignments:RoleDefinitionId] ForAnyOfAnyValues:GuidEquals {$ASSIGNABLE}))"

# Replace rather than add, so an earlier assignment without the condition does not survive.
az role assignment delete --assignee "$PRINCIPAL_ID" --role "Role Based Access Control Administrator" \
  --scope "$SCOPE" --output none 2>/dev/null || true
az role assignment create --assignee-object-id "$PRINCIPAL_ID" --assignee-principal-type ServicePrincipal \
  --role "Role Based Access Control Administrator" --scope "$SCOPE" \
  --condition "$CONDITION" --condition-version "2.0" --output none

cat <<EOF

Done. In GitHub, create the environment "$ENVIRONMENT" for $REPO and store:

  Variables
    AZURE_CLIENT_ID        $CLIENT_ID
    AZURE_TENANT_ID        $TENANT_ID
    AZURE_SUBSCRIPTION_ID  $SUBSCRIPTION_ID
    AZURE_LOCATION         (optional, default uaenorth)

  Secrets
    POSTGRES_ADMIN_PASSWORD   letters and digits only, 16 or more, with upper case, lower case and a digit
    SIMULATOR_INBOUND_SECRET  16 or more characters

With the GitHub CLI:

  gh variable set AZURE_CLIENT_ID --env $ENVIRONMENT --repo $REPO --body $CLIENT_ID
  gh variable set AZURE_TENANT_ID --env $ENVIRONMENT --repo $REPO --body $TENANT_ID
  gh variable set AZURE_SUBSCRIPTION_ID --env $ENVIRONMENT --repo $REPO --body $SUBSCRIPTION_ID
  gh secret set POSTGRES_ADMIN_PASSWORD --env $ENVIRONMENT --repo $REPO
  gh secret set SIMULATOR_INBOUND_SECRET --env $ENVIRONMENT --repo $REPO
EOF
