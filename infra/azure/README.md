# Azure deployment

Bicep that creates everything CareLoop needs in an Azure subscription, and a script that builds the images and deploys them. It is the Azure counterpart of the root `docker-compose.yml`.

```bash
az login
infra/azure/scripts/deploy.sh dev
```

That is the whole deployment into an empty subscription: about fifteen minutes the first time, and it prints the dashboard address at the end. Run it again to deploy a new build.

First deployed end to end on 18 November 2026, into an existing resource group on a Visual Studio subscription with Contributor rights only (`ASSIGN_ROLES=false`), with the apps in `eastus2` and the database in `northeurope`. The default mode with role assignments, private networking, the GitHub workflow and `CREATE_FOUNDRY` have passed Azure's preflight validation but have not been deployed.

## What it creates

One resource group, `rg-careloop-<environment>`, holding:

| Resource | Purpose |
| --- | --- |
| Container Apps environment | Runs the containers. Apps reach each other over HTTPS inside it |
| Container apps | `web` and `api` (public), and optionally `ai-workflow`, `his-simulator`, `messaging-simulator` |
| Container registry | Holds the images. No admin user; pushes and pulls use Entra ID |
| PostgreSQL flexible server | The `careloop` database (see "Database choice") |
| Key Vault | The database password, the connection string and the inbound-message secret |
| Microsoft Foundry account, project, model deployment and guardrail | Only with `CREATE_FOUNDRY=true`; otherwise an existing Foundry is used |
| Two managed identities | `apps` pulls images and reads Key Vault. `ai` pulls images and signs in to the existing Foundry project, and nothing else |
| Log Analytics and Application Insights | Container logs (optional) |
| Virtual network, private endpoints, private DNS zones | Only with private networking |

## Layout

```text
main.bicep                  Subscription-scope entry point: every parameter is described here
modules/                    One file per resource area; apps.bicep wires the services together
environments/*.bicepparam   One file per environment: dev (demo) and prod (production shape)
scripts/deploy.sh           Checks, images, deployment
scripts/setup-github-oidc.sh  One-time setup for the Deploy workflow
```

## Requirements

- Azure CLI 2.60 or later. Bicep comes with it.
- **Owner** on the subscription, or **Contributor** plus **Role Based Access Control Administrator**, because the deployment gives the managed identities their roles. With **Contributor** alone, use `ASSIGN_ROLES=false`; see "Deploying without the right to assign roles".
- No local Docker: images are built in Azure (`az acr build`). Set `BUILD_MODE=docker` to build locally instead, which is also the way round a subscription type that does not allow ACR Tasks.

## Environments

An environment is a parameter file. `deploy.sh <name>` reads `environments/<name>.bicepparam`; copy one to add an environment, and change `environmentName` to match.

| | `dev` | `prod` |
| --- | --- | --- |
| Networking | Public endpoints | Virtual network and private endpoints |
| Simulators | Deployed, public | Not deployed |
| Timing | Demo pathway (deadlines in seconds) | Real pathway |
| Database | Burstable B1ms, 7-day backups | General Purpose D2ds v5, 35-day geo-redundant backups |
| AI | Service deployed, pointed at an existing Foundry project (or one created with `CREATE_FOUNDRY=true`) | Not deployed |
| Key Vault purge protection | Off | On |

The switches are parameters of `main.bicep`: `privateNetworking`, `deploySimulators`, `deployAi`, `deployMonitoring`, plus `exposeApi`, `exposeSimulators` and `allowedIpRanges` for what is reachable from the internet.

## Addresses

Each app's address is `https://<app>.<environment domain>`, for example `https://web.<environment domain>`. The
environment domain is given to the Container Apps environment when it is created, so:

- **Every redeployment keeps the same addresses.** A new build, a changed setting or a new revision does not touch them.
- **Deleting the environment loses them.** Removing the Container Apps environment (or the whole resource group) and
  deploying again gives a new, different domain, and every address changes. To keep the addresses, never delete
  `cae-<prefix>-<environment>`; redeploy over it.
- **Addresses that survive a rebuild need a custom domain**, a name you own with DNS records pointing at the web app.
  That is not set up here.

`deploy.sh` prints the addresses at the end of every run, and they can be read at any time:

```bash
az deployment sub show --name careloop-dev --query "properties.outputs.{dashboard:webUrl.value, api:apiUrl.value, his:hisSimulatorUrl.value, messaging:messagingSimulatorUrl.value}" -o table
```

## What is reachable from the internet

This is a prototype, so by default everything with a screen or an endpoint is public and nothing is restricted by address:

| App | Default | To hide it |
| --- | --- | --- |
| `web` (dashboard) | Public | Always public |
| `api` | Public, so a hospital system can post to `/fhir` | `exposeApi = false`: reachable only from the other apps, and from outside through the web app on `/api/` |
| `his-simulator`, `messaging-simulator` | Public, so their screens can be opened | `exposeSimulators = false` |
| `ai-workflow` | Never public | |

`allowedIpRanges` is empty by default. Given a list, only those address ranges can reach the public apps. It has not been tried: a list on an app may also block calls from the other apps, so test it before relying on it.

Settings the script reads from the shell:

| Variable | Default | |
| --- | --- | --- |
| `AZURE_LOCATION` | `uaenorth` | Region |
| `AZURE_AI_PROJECT_ENDPOINT`, `AZURE_AI_MODEL_DEPLOYMENT_NAME`, `AZURE_AI_FOUNDRY_ACCOUNT_ID`, `CARELOOP_FOUNDRY_PROMPT_SHIELDS_ATTESTED` | unset | The existing Foundry project; see "AI" |
| `CREATE_FOUNDRY`, `AZURE_AI_LOCATION` | `false`, the region | Create a Foundry here instead; see "AI" |
| `NAME_PREFIX` | `careloop` | Prefix of every resource name. Change it to run two copies of one environment in a subscription |
| `RESOURCE_GROUP` | unset | Deploy into this existing resource group instead of creating `rg-careloop-<environment>` |
| `ASSIGN_ROLES` | `true` | `false` when you are Contributor only; see "Deploying without the right to assign roles" |
| `POSTGRES_LOCATION` | the region | Put only the database in another region; see "PostgreSQL refusals" |
| `POSTGRES_SKU_NAME`, `POSTGRES_SKU_TIER` | `Standard_B1ms`, `Burstable` | Database size (dev). See "PostgreSQL refusals" |
| `POSTGRES_ADMIN_PASSWORD`, `SIMULATOR_INBOUND_SECRET` | generated | See "Secrets" |
| `IMAGE_TAG` | `<commit>-<timestamp>` | |
| `BUILD_MODE` | `acr` | `docker` builds locally and pushes |
| `API_ROLLOUT` | `recreate` | See "How a deployment runs" |

To see what a run would change without changing anything, container apps included:

```bash
infra/azure/scripts/deploy.sh dev --what-if
```

An environment stays in the region it was created in. The script stops if `AZURE_LOCATION` differs from where the environment already is; use another environment name or `NAME_PREFIX` for a copy elsewhere.

## Deploying into an existing resource group

```bash
RESOURCE_GROUP=<name> infra/azure/scripts/deploy.sh dev
```

Everything is created inside that group, which is otherwise left alone: not created, moved or retagged. The script remembers the group on later runs. The group's own region does not matter; resources go to `AZURE_LOCATION`. Do not remove the environment by deleting the group (see "Removing an environment").

## Deploying without the right to assign roles

With Contributor and nothing more, Azure refuses the role assignments (`roleAssignments/write`). Then:

```bash
ASSIGN_ROLES=false infra/azure/scripts/deploy.sh dev
```

No role assignment is made, and three things work differently:

- **Image pulls.** The registry's admin user is switched on and each app pulls with its password, kept as a secret on the app, instead of with its managed identity.
- **Key Vault.** The vault uses access policies instead of Azure roles. The apps still read their secrets through Key Vault references.
- **Foundry.** Nobody is given the Foundry User role; the Foundry account's owner grants it to the AI service's identity by hand.

This is weaker than the default: anyone who can read an app's configuration can read the registry password. Use it for a prototype, and move to the default once someone with the right to assign roles can deploy. Do not flip `ASSIGN_ROLES` on an environment that already exists without checking the Key Vault afterwards: changing a vault between roles and access policies changes who can read it.

## PostgreSQL refusals

A subscription can be refused PostgreSQL in two ways. They look alike, and Azure's own listing of sizes predicts only the first.

- **The region is closed to your subscription.** The script checks this before doing anything and stops with Azure's reason.
- **The small (Burstable) size is refused in that region.** The deployment fails with `SkuNotAvailable: Specified SKU is not supported in this region`, although the size is listed as available there. Seen on Visual Studio subscriptions.

Either way, keep everything else where it is and put only the database in a region that accepts it:

```bash
POSTGRES_LOCATION=<region> infra/azure/scripts/deploy.sh dev
```

What was found on Visual Studio subscriptions on 18 November 2026; another subscription may differ:

| Region | Result |
| --- | --- |
| `northeurope` | Burstable accepted |
| `centralus`, `westus3`, `uaenorth` | Burstable refused; General Purpose accepted in `centralus` |
| `eastus2`, `eastus`, `westus2` | Closed to the subscription |

The database's name includes its region, so changing `POSTGRES_LOCATION` on an existing environment creates a new, empty server beside the old one and points the apps at it. Data is not copied, and the old server keeps running and costing money until you delete it (`az postgres flexible-server delete`). A database far from the apps adds latency to every query: `eastus2` to `northeurope` is roughly 80 to 100 ms each way.

A General Purpose size is accepted where Burstable is not, at roughly ten times the cost while it runs: `POSTGRES_SKU_NAME=Standard_D2ds_v5 POSTGRES_SKU_TIER=GeneralPurpose`. Do not try to resize a General Purpose server down to Burstable in a region that refuses Burstable: the resize is accepted, then hangs with the database unavailable (over forty minutes when tried, until the server was deleted).

After a refused attempt the server name stays reserved for a few minutes ("server name is already used"); wait and run again.

## How a deployment runs

1. **Check.** Azure validates the whole template before anything is built or changed.
2. **Platform, first run only.** The images need a registry, so the first run deploys `main.bicep` with `imageTag` empty, which creates everything except the container apps.
3. **Images.** One per service, tagged with the commit and a timestamp.
4. **Stop the running API.** Container Apps starts a new revision beside the old one. For the API that would mean two deadline workers and two outboxes for a while, and the old copy running against a database the new one has just migrated. So the script stops the old revision first, and the API is down until the new one is healthy (a minute or two). `API_ROLLOUT=rolling` skips this step and accepts the overlap instead.
5. **Deploy.** One deployment of `main.bicep` with `imageTag` set: the platform and the apps together, so a changed secret and the apps that read it change in the same step.

## Secrets

Two values are secret: the PostgreSQL administrator password and the secret that signs messaging callbacks. They are never written to a file.

- On the first run the script generates both. Bicep stores them in Key Vault and gives the person deploying the right to read them.
- On later runs the script reads them back from Key Vault.
- If it cannot read them back (a service principal, or a Key Vault on a private network), it stops and asks for them. It never generates a new value for an environment that already exists, because that would change the database password under the running API.
- To choose the values, or to rotate them on purpose, set `POSTGRES_ADMIN_PASSWORD` and `SIMULATOR_INBOUND_SECRET` before running. The password must be letters and digits only (it goes into a connection string) with upper case, lower case and a digit.

The container apps hold no secret values. Each reads its secrets through a Key Vault reference using the `apps` managed identity.

## Deploying from GitHub Actions

The **Deploy to Azure** workflow (`.github/workflows/deploy.yml`) runs the same script. It is started by hand, takes an environment name, and signs in with OpenID Connect, so no Azure credential is stored in GitHub.

Once per subscription and environment:

```bash
az login
infra/azure/scripts/setup-github-oidc.sh savannah-pulse/care-loop dev
```

This creates an app registration trusted only for runs of that repository in the GitHub environment `dev`, gives it Contributor and Role Based Access Control Administrator on the subscription, and prints the three variables and two secrets to store on the GitHub environment. A different Azure account is a different GitHub environment with its own values.

The second role carries a condition: the identity may assign and remove only the four roles the deployment gives the managed identities (AcrPull, Key Vault Secrets User, Key Vault Secrets Officer, Foundry User). It cannot make itself or anyone else an Owner.

## The web app's proxy

The dashboard calls the API on its own origin under `/api/`, and nginx in the web image forwards those calls to `http://api:8080`. That address does not exist in Container Apps: another app is reached over HTTPS on its own host name, and the environment routes by `Host` header.

There is still one nginx configuration, `src/web/nginx.conf`. `apps.bicep` reads it, rewrites the two proxy lines for Container Apps and mounts the result over the image's copy. If those two lines change shape in `src/web/nginx.conf`, the deployment stops with a message naming the two variables in `apps.bicep` to update, rather than shipping a proxy that points nowhere.

## Database choice

`AGENTS.md` lists the database as an open question: PostgreSQL is assumed and Azure SQL is the alternative. This project deploys PostgreSQL because that is what the API and Docker Compose run on today. It is not a decision. If the team chooses Azure SQL, the change is contained: replace `modules/postgres.bicep`, the connection string built in `main.bicep`, and the `postgres*` parameters.

## AI

`deployAi` deploys the `ai-workflow` app, internal only, with its own managed identity, and points it at a Foundry project. There are two ways to give it one.

**Where things stand in the code.** On `main` the AI workflow service is a scaffold: it serves `/health` and `/workflow/stages`, reads none of the settings below and makes no call to Foundry. The API's `AiWorkflow` and `FoundryDirect` providers are not implemented either. So today these settings connect nothing; they are in place for when WS9 lands.

### Use an existing Foundry (the usual case)

1. Set these before running `deploy.sh` (the first two are the names the service and Docker Compose use). They are live addresses, so they are read from the environment and never written in a parameter file.

   ```bash
   export AZURE_AI_PROJECT_ENDPOINT="https://<account>.services.ai.azure.com/api/projects/<project>"
   export AZURE_AI_MODEL_DEPLOYMENT_NAME="<deployment name>"
   export AZURE_AI_FOUNDRY_ACCOUNT_ID="$(az cognitiveservices account show --name <account> --resource-group <its resource group> --query id -o tsv)"
   ```

2. The service signs in with its own managed identity, which needs the **Foundry User** role on the Foundry account. With `AZURE_AI_FOUNDRY_ACCOUNT_ID` set, the deployment gives it that role (you need the right to assign roles on the account). Without it, someone who owns the account grants the role by hand after the first deployment, to the identity in the `aiIdentityPrincipalId` output.

   The service also refuses to call Foundry until an operator attests that Prompt Shields block both user and document (indirect) attacks on that model deployment. Once that is confirmed in the Foundry guardrail playground:

   ```bash
   export CARELOOP_FOUNDRY_PROMPT_SHIELDS_ATTESTED=true
   ```

3. Set `apiAiProvider = 'AiWorkflow'` once WS9 has finished that provider. Until then the API stays on `NoOp`.

### Create a Foundry here

For a subscription with no Foundry to point at:

```bash
CREATE_FOUNDRY=true AZURE_AI_LOCATION=<region that offers the model> infra/azure/scripts/deploy.sh dev
```

This creates a Foundry account with keys disabled, a project, one model deployment and a guardrail with Prompt Shields blocking user and document attacks, gives the service's identity the Foundry User role, and points the service at it. The existing-Foundry variables are then ignored.

The model defaults to `gpt-4.1-mini` on the `GlobalStandard` deployment type, which may process prompts in any region. Change `foundryModelName`, `foundryModelVersion` and `foundryModelSku` in the parameter file to suit; a model that is not offered in the region, or a subscription with no quota for it, fails the deployment. Deleting the environment soft-deletes the account; purge it (`az cognitiveservices account purge`) before creating it again under the same name.

Either way, with private networking the Foundry account is reached over its public endpoint; a private endpoint for it is not set up here.

## Before real patient data

The infrastructure is one of several prerequisites listed in `AGENTS.md`. On the infrastructure side:

- Use `privateNetworking = true`. In public mode PostgreSQL accepts connections from any Azure service (the firewall's "allow Azure services" rule), protected by its password and TLS only.
- The API signs in to PostgreSQL as the server administrator with a password. A least-privilege database role, or Entra ID sign-in, needs a change in the API first.
- The API has demo-level authentication, and by default the dashboard, the API and the simulators are open to everyone. Hide the API (`exposeApi = false`) or restrict addresses (`allowedIpRanges`) until it has real authentication.
- The API applies migrations at startup (`migrateOnStartup`) and runs as exactly one replica, because the deadline worker, the outbox and the notification send log live in its process. Each deployment therefore stops the API for a minute or two.
- Application Insights receives nothing from the services until they add telemetry; container logs go to Log Analytics.

## Removing an environment

When the deployment created its own resource group:

```bash
az group delete --name rg-careloop-dev
```

When it went into an existing group (`RESOURCE_GROUP`), delete only what carries this environment's tags, and leave the group. Dependencies mean some deletions are refused the first time, so run it until it lists nothing:

```bash
az resource list --resource-group <group> --query "[?tags.application=='careloop' && tags.environment=='dev'].id" -o tsv | xargs -n 1 az resource delete --ids
```

Key Vault is soft-deleted, which blocks a redeployment under the same name. Purge it (`az keyvault purge`) or wait out the retention period. A Key Vault with purge protection cannot be purged early. A Foundry account created with `CREATE_FOUNDRY` is soft-deleted the same way (`az cognitiveservices account purge`).

## Checking a change

```bash
az bicep lint --file infra/azure/main.bicep
```

`bicepconfig.json` makes unused parameters, unused variables and secrets in outputs errors.
