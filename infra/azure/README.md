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
| Container apps | `web` and `api` (public), and optionally `his-simulator` and `messaging-simulator` |
| Container registry | Holds the images. No admin user; pushes and pulls use Entra ID |
| PostgreSQL flexible server | The `careloop` database (see "Database choice") |
| Key Vault | The database password, the connection string and the inbound-message secret |
| Microsoft Foundry account, project, model deployment and guardrail | Optional; created only with `CREATE_FOUNDRY=true`. Without it the API can use a model deployment in an existing Foundry project (see "AI and Foundry") |
| Managed identity | `apps` pulls images and reads Key Vault. The API also calls the Foundry model deployment as this identity, which needs the Foundry User role on the Foundry account |
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
| AI | Off (`NoOp`) unless `API_AI_PROVIDER=Foundry` is set for the deployment; then the API calls the model deployment `AZURE_AI_MODEL_DEPLOYMENT_NAME` in the Foundry project `AZURE_AI_PROJECT_ENDPOINT` names | Off: `apiAiProvider = 'NoOp'` in the parameter file |
| Key Vault purge protection | Off | On |

The switches are parameters of `main.bicep`: `privateNetworking`, `deploySimulators`, `deployMonitoring`, plus `exposeApi`, `exposeSimulators` and `allowedIpRanges` for what is reachable from the internet.

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

`allowedIpRanges` is empty by default. Given a list, only those address ranges can reach the public apps. It has not been tried: a list on an app may also block calls from the other apps, so test it before relying on it.

Settings the script reads from the shell:

| Variable | Default | |
| --- | --- | --- |
| `AZURE_LOCATION` | `uaenorth` | Region |
| `API_AI_PROVIDER` | `NoOp` | `NoOp` keeps AI off; `Foundry` has the API call a model deployment in the Foundry project (`dev`). See "AI and Foundry" |
| `AZURE_AI_PROJECT_ENDPOINT` | unset | The Foundry project whose model the API calls, `https://<account>.services.ai.azure.com/api/projects/<project>`. Needed for `API_AI_PROVIDER=Foundry` |
| `AZURE_AI_MODEL_DEPLOYMENT_NAME` | unset | The model deployment in that project that writes the texts, for example `gpt-6.1-sol`. Needed for `API_AI_PROVIDER=Foundry`. The API is given the endpoint and the model only when both are set |
| `AZURE_AI_FOUNDRY_ACCOUNT_ID` | unset | Resource ID of that project's Foundry account. When set, and `ASSIGN_ROLES` is `true`, the API's identity is given Foundry User on it |
| `CREATE_FOUNDRY`, `AZURE_AI_LOCATION` | `false`, the region | Create a Foundry account, project and model deployment here instead of using an existing one; the API is pointed at them |
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
- **Foundry.** The Foundry User role is not assigned. Before AI is switched on, the Foundry account's owner must grant it to the `apps` identity by hand; see "AI and Foundry".

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

## AI and Foundry

The API calls a model deployment in a Microsoft Foundry project itself, for three texts shown to staff: the role briefing, the impact summary and the patient message draft. There is no separate AI service. AI is off by default: `apiAiProvider` is `NoOp`, the API's AI endpoints answer 409 and no model call is made. What the model is asked is in the API's plain-text prompt files (`src/api/CareLoop.Api/Prompts/`); the project's hosted agents are not used. The contract is [`docs/ai-design/integration.md`](../../docs/ai-design/integration.md).

### Switching AI on in an environment

Three things are needed:

1. **A model deployment exists.** The Foundry project must hold a model deployment for the API to call. Its name is passed as `AZURE_AI_MODEL_DEPLOYMENT_NAME`. No agents are needed.
2. **The API is pointed at the project and the model.** For `dev`, set these before running the script. They are read from the environment, never written to a parameter file:

   ```bash
   API_AI_PROVIDER=Foundry AZURE_AI_PROJECT_ENDPOINT=https://<account>.services.ai.azure.com/api/projects/<project> AZURE_AI_MODEL_DEPLOYMENT_NAME=<model deployment> AZURE_AI_FOUNDRY_ACCOUNT_ID=<resource ID of the Foundry account> infra/azure/scripts/deploy.sh dev
   ```

   `API_AI_PROVIDER` becomes the API setting `Ai__Provider`. `AZURE_AI_PROJECT_ENDPOINT` becomes `Ai__Foundry__ProjectEndpoint` and `AZURE_AI_MODEL_DEPLOYMENT_NAME` becomes `Ai__Foundry__Model`; the API is given both only when both are set. With `Foundry` and either one missing the API refuses to start. Set the same values on every later run, or the next deployment switches AI off again. `prod` sets `apiAiProvider = 'NoOp'` in its parameter file and reads none of these variables.
3. **The API's identity may call the project.** There is no key. The API signs in as the `apps` managed identity, which needs the **Foundry User** role (`53ca6127-db72-4b80-b1b0-d745d6d5456d`) on the Foundry account. Without it every call is refused with 403 and the dashboard shows "AI could not write this".

Who grants Foundry User:

| Case | Who grants it |
| --- | --- |
| `ASSIGN_ROLES=true` (the default) and `AZURE_AI_FOUNDRY_ACCOUNT_ID` set | The deployment, on that existing account (`modules/foundry-access.bicep`). The person deploying needs the right to assign roles on that account, which may be in another resource group or subscription |
| `ASSIGN_ROLES=true` and `CREATE_FOUNDRY=true` | The deployment, on the Foundry it creates |
| `ASSIGN_ROLES=false`, or an existing project with no `AZURE_AI_FOUNDRY_ACCOUNT_ID` | Nobody. The Foundry account's owner grants it by hand to the `apps` identity (`id-<prefix>-<environment>-apps`) |

### Creating a Foundry

`CREATE_FOUNDRY=true` creates a Foundry account, a project named `careloop`, one model deployment and a Prompt Shields guardrail, and points the API at that project and that model deployment (`AZURE_AI_PROJECT_ENDPOINT`, `AZURE_AI_MODEL_DEPLOYMENT_NAME` and `AZURE_AI_FOUNDRY_ACCOUNT_ID` are then ignored):

```bash
CREATE_FOUNDRY=true API_AI_PROVIDER=Foundry AZURE_AI_LOCATION=<region that offers the model> infra/azure/scripts/deploy.sh dev
```

A Foundry created this way works with `API_AI_PROVIDER=Foundry`: the API calls the model deployment it creates, and no agents are needed. Without `API_AI_PROVIDER=Foundry` the Foundry is created and AI stays off.

The model defaults to `gpt-4.1-mini` on the `GlobalStandard` deployment type, which may process prompts in any region. Change `foundryModelName`, `foundryModelVersion` and `foundryModelSku` in the parameter file to suit; a model that is not offered in the region, or a subscription with no quota for it, fails the deployment. Deleting the environment soft-deletes the account; purge it (`az cognitiveservices account purge`) before creating it again under the same name.

With private networking the Foundry account is reached over its public endpoint; a private endpoint for it is not set up here.

### Leftovers from the retired AI workflow service

An environment first deployed while the Python AI workflow service existed may still hold its container app, `ai-workflow`, and its managed identity, `id-<prefix>-<environment>-ai`. Deployments are incremental: a new deployment does not remove resources that have left the template, so both stay, with any roles the identity was given, until someone removes them. Nothing in the current deployment uses them.

Removing them is a manual, deliberate clean-up by the environment's owner. The deployment never does it. Check each name first, and make sure nothing else uses the identity:

```bash
# 1. The container app.
az containerapp delete --name ai-workflow --resource-group <resource group>

# 2. The identity's roles. List them first, then delete each one. Foundry User on the Foundry account is the one that matters.
AI_PRINCIPAL_ID=$(az identity show --name id-<prefix>-<environment>-ai --resource-group <resource group> --query principalId -o tsv)
az role assignment list --assignee "$AI_PRINCIPAL_ID" --all -o table
az role assignment delete --assignee "$AI_PRINCIPAL_ID" --role 53ca6127-db72-4b80-b1b0-d745d6d5456d --scope <resource ID of the Foundry account>

# 3. The identity itself, last.
az identity delete --name id-<prefix>-<environment>-ai --resource-group <resource group>
```

Deleting a role assignment needs Owner or Role Based Access Control Administrator on its scope. An environment deployed with `ASSIGN_ROLES=false` was given no role assignments, so step 2 lists nothing unless the account's owner granted one by hand.

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
