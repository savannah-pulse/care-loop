# Azure deployment

Bicep that creates everything CareLoop needs in an Azure subscription, and a script that builds the images and deploys them. It is the Azure counterpart of the root `docker-compose.yml`.

```bash
az login
infra/azure/scripts/deploy.sh dev
```

That is the whole deployment into an empty subscription: about fifteen minutes the first time, and it prints the dashboard address at the end. Run it again to deploy a new build.

## What it creates

One resource group, `rg-careloop-<environment>`, holding:

| Resource | Purpose |
| --- | --- |
| Container Apps environment | Runs the containers. Apps reach each other over HTTPS inside it |
| Container apps | `web` (public), `api`, and optionally `ai-workflow`, `his-simulator`, `messaging-simulator` |
| Container registry | Holds the images. No admin user; pushes and pulls use Entra ID |
| PostgreSQL flexible server | The `careloop` database |
| Key Vault | The database password, the connection string and the inbound-message secret |
| Two managed identities | `apps` pulls images and reads Key Vault. `ai` pulls images and calls the model, and nothing else |
| Log Analytics and Application Insights | Container logs (optional) |
| Microsoft Foundry account, project, model deployment and guardrail | For the AI workflow service (optional) |
| Virtual network, private endpoints, private DNS zones | Only with private networking |

## Layout

```text
main.bicep                  Subscription-scope entry point: every parameter is described here
modules/                    One file per resource area; apps.bicep wires the services together
config/web-nginx.conf       nginx configuration mounted into the web app (see "The web app's proxy")
environments/*.bicepparam   One file per environment: dev (demo) and prod (production shape)
scripts/deploy.sh           Platform, images, apps
scripts/setup-github-oidc.sh  One-time setup for the Deploy workflow
```

## Requirements

- Azure CLI 2.60 or later. Bicep comes with it.
- **Owner** on the subscription, or **Contributor** plus **Role Based Access Control Administrator**. Contributor alone fails, because the deployment gives the managed identities their roles.
- No local Docker: images are built in Azure (`az acr build`). Set `BUILD_MODE=docker` to build locally instead, which is also the way round a subscription type that does not allow ACR Tasks.

## Environments

An environment is a parameter file. `deploy.sh <name>` reads `environments/<name>.bicepparam`; copy one to add an environment, and change `environmentName` to match.

| | `dev` | `prod` |
| --- | --- | --- |
| Networking | Public endpoints | Virtual network and private endpoints |
| Simulators | Deployed, public | Not deployed |
| Timing | Demo pathway (deadlines in seconds) | Real pathway |
| Database | Burstable B1ms, 7-day backups | General Purpose D2ds v5, 35-day geo-redundant backups |
| AI | Service and Foundry resources created, inference off | Not deployed |
| Key Vault purge protection | Off | On |

The switches are parameters of `main.bicep`: `privateNetworking`, `deploySimulators`, `deployAi`, `deployMonitoring`, plus `exposeApi`, `exposeSimulators` and `allowedIpRanges` for what is reachable from the internet.

Settings the script reads from the shell:

| Variable | Default | |
| --- | --- | --- |
| `AZURE_LOCATION` | `uaenorth` | Region |
| `AZURE_AI_LOCATION` | `swedencentral` (dev) | Region of the Foundry account; the model must be offered there |
| `NAME_PREFIX` | `careloop` | Prefix of every resource name. Change it to run two copies of one environment in a subscription |
| `POSTGRES_ADMIN_PASSWORD`, `SIMULATOR_INBOUND_SECRET` | generated | See "Secrets" |
| `IMAGE_TAG` | `<commit>-<timestamp>` | |
| `BUILD_MODE` | `acr` | `docker` builds locally and pushes |

To see what a run would change without changing anything:

```bash
infra/azure/scripts/deploy.sh dev --what-if
```

## How a deployment runs

The container apps need images, and the images need a registry, so `deploy.sh` deploys `main.bicep` twice:

1. With `imageTag` empty: the platform only.
2. Build and push one image per service, tagged with the commit and a timestamp.
3. With `imageTag` set: the same platform plus the container apps on that tag.

Deployments are incremental, so step 1 leaves the running apps alone on later runs.

## Secrets

Two values are secret: the PostgreSQL administrator password and the secret that signs messaging callbacks. They are never written to a file.

- On the first run the script generates both. Bicep stores them in Key Vault and gives the person deploying the right to read them.
- On later runs the script reads them back from Key Vault, so the password is not rotated by accident.
- To choose the values, set `POSTGRES_ADMIN_PASSWORD` and `SIMULATOR_INBOUND_SECRET` before running. The password must be letters and digits only (it goes into a connection string) with upper case, lower case and a digit.
- With private networking the Key Vault is not reachable from a laptop, so set both variables yourself; otherwise the script warns and generates a new password, which rotates it.

The container apps hold no secret values. Each reads its secrets through a Key Vault reference using the `apps` managed identity.

## Deploying from GitHub Actions

The **Deploy to Azure** workflow (`.github/workflows/deploy.yml`) runs the same script. It is started by hand, takes an environment name, and signs in with OpenID Connect, so no Azure credential is stored in GitHub.

Once per subscription and environment:

```bash
az login
infra/azure/scripts/setup-github-oidc.sh savannah-pulse/care-loop dev
```

This creates an app registration trusted only for runs of that repository in the GitHub environment `dev`, gives it Contributor and Role Based Access Control Administrator on the subscription, and prints the three variables and two secrets to store on the GitHub environment. A different Azure account is a different GitHub environment with its own values.

## The web app's proxy

The dashboard calls the API on its own origin under `/api/`, and nginx in the web image forwards those calls to `http://api:8080`. That address does not exist in Container Apps: another app is reached over HTTPS on its own host name, and the environment routes by `Host` header. So `apps.bicep` mounts `config/web-nginx.conf` over the image's `default.conf`, with the API's host name filled in. `src/web` is unchanged. If `src/web/nginx.conf` changes, make the same change in `config/web-nginx.conf`.

## AI

`deployAi` creates the `ai-workflow` app (internal only), a Foundry account with keys disabled, a project, one model deployment, and a guardrail with Prompt Shields blocking user and document attacks. The service's identity gets the Foundry User role.

Everything is then in place but off. To turn inference on, in order:

1. Verify the guardrail in the Foundry guardrail playground.
2. Set `aiInferenceEnabled = true` and `aiPromptShieldsAttested = true`.
3. Set `apiAiProvider` to `AiWorkflow` or `FoundryDirect` once WS9 has finished that provider. `FoundryDirect` also gives the API's identity the Foundry User role.

The model is `gpt-4.1-mini` on the `GlobalStandard` deployment type, which may process prompts in any region. Change `aiModelSku`, `aiModelName`, `aiModelVersion` and `aiLocation` to suit the residency rules that apply; a model that is not offered in the chosen region, or a subscription with no quota for it, fails the deployment.

## Before real patient data

The infrastructure is one of several prerequisites listed in `AGENTS.md`. On the infrastructure side:

- Use `privateNetworking = true`. In public mode PostgreSQL accepts connections from any Azure service (the firewall's "allow Azure services" rule), protected by its password and TLS only.
- The API signs in to PostgreSQL as the server administrator with a password. A least-privilege database role, or Entra ID sign-in, needs a change in the API first.
- The API has demo-level authentication. Keep `exposeApi = false`, or restrict `allowedIpRanges`, until it has real authentication. The dashboard itself is public unless `allowedIpRanges` is set.
- The API applies migrations at startup (`migrateOnStartup`) and runs as exactly one replica, because the deadline worker, the outbox and the notification send log live in its process.
- Application Insights receives nothing from the services until they add telemetry; container logs go to Log Analytics.

## Removing an environment

```bash
az group delete --name rg-careloop-dev
```

Key Vault and the Foundry account are soft-deleted, which blocks a redeployment under the same name. Purge them (`az keyvault purge`, `az cognitiveservices account purge`) or wait out the retention period. A Key Vault with purge protection cannot be purged early.

## Checking a change

```bash
az bicep lint --file infra/azure/main.bicep
```

`bicepconfig.json` makes unused parameters, unused variables and secrets in outputs errors.
