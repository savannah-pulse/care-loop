# Deploying CareLoop to Azure from your own machine

A step-by-step guide for deploying the submission environment to your Azure subscription by running the deploy script locally (not through GitHub Actions). Allow about half an hour, most of it waiting. Background and every option are in [README.md](README.md); this file is only the steps.

This has been done once end to end, on 18 November 2026: into an existing resource group, on a Visual Studio subscription, with Contributor rights only. The things that went wrong on the way are in "If something goes wrong".

## Before you start

You need:

- **An Azure subscription** where you are Owner, or Contributor. Which one decides a setting in step 2.
- **Azure CLI 2.60 or later.** Check with `az version`. Install: <https://learn.microsoft.com/cli/azure/install-azure-cli>.
- **Git and a clone of the repository**, on the commit you want to submit.
- **A Mac or Linux shell** (on Windows use WSL or Azure Cloud Shell). You do not need Docker; the images are built in Azure.

## 1. Sign in and pick the subscription

```bash
az login
```

```bash
az account list --query "[].{name:name, id:id, isDefault:isDefault}" -o table
```

```bash
az account set --subscription "<subscription id>"
```

Now check what you are allowed to do. This lists your own roles on the subscription:

```bash
az role assignment list --assignee "$(az ad signed-in-user show --query id -o tsv)" --include-groups --include-inherited --scope "/subscriptions/$(az account show --query id -o tsv)" --query "[].roleDefinitionName" -o tsv
```

- `Owner`, or `Contributor` together with `Role Based Access Control Administrator`: you can use the default settings.
- `Contributor` only: you must set `ASSIGN_ROLES=false` in step 2.
- Nothing, or a role granted in the last few minutes: wait. A new role takes up to ten minutes to take effect, and until it does some commands are refused with `AuthorizationFailed`.

## 2. Choose your settings

Settings are environment variables you set in the shell before running the script. Set only the ones you need; the first deployment fixes them for the life of the environment.

| Set this | When |
| --- | --- |
| `export AZURE_LOCATION=<region>` | Always, unless `uaenorth` is what you want. |
| `export ASSIGN_ROLES=false` | You are Contributor only. |
| `export RESOURCE_GROUP=<name>` | You want everything inside a resource group that already exists. Otherwise `rg-careloop-dev` is created. |
| `export API_AI_PROVIDER=Foundry AZURE_AI_PROJECT_ENDPOINT=<project endpoint>` | You want AI on, and a Foundry project already holds the three agents the API asks (`role-briefing`, `impact-summary`, `patient-notification`). Add `AZURE_AI_FOUNDRY_ACCOUNT_ID=<account resource ID>` to have the deployment give the API's identity the Foundry User role; with `ASSIGN_ROLES=false` the account's owner grants it by hand. Leave all of these unset to keep AI off. See "AI and Foundry" in the [README](README.md). |
| `export POSTGRES_LOCATION=northeurope` | Your subscription is a Visual Studio (or similar credit-based) subscription. These are refused the database in many regions; North Europe accepted it. Also use this when the script tells you PostgreSQL is not allowed in your region (it checks first). Only the database goes there. |

The combination that worked on 18 November 2026:

```bash
export AZURE_LOCATION=eastus2 POSTGRES_LOCATION=northeurope ASSIGN_ROLES=false RESOURCE_GROUP=<existing group>
```

## 3. Preview (changes nothing)

From the repository root:

```bash
infra/azure/scripts/deploy.sh dev --what-if
```

It should list about 20 resources to create, including four container apps, and end without an error.

## 4. Deploy

```bash
infra/azure/scripts/deploy.sh dev
```

What you will see, in order:

1. `Registering Microsoft.…` lines the first time a subscription is used. A few minutes.
2. `Checking the deployment ...`
3. `Deploying the platform ...` About ten minutes. The Container Apps environment and PostgreSQL are the slow parts.
4. `Building images ...` Four images, one to two minutes each. The build output is long; that is normal.
5. `Deploying ...` Key Vault and the container apps. A few minutes.
6. `Done.` followed by the addresses of the dashboard, the API and both simulators.

If the script stops with an error, fix the cause and run the same command again with the same settings. It is safe to rerun: it keeps what was created and carries on.

## 5. Check that it works

Open the **Dashboard** address the script printed. Then check each service answers (replace the addresses with the ones printed):

```bash
curl -s https://<api address>/health
```

```bash
curl -s https://<his simulator address>/health
```

```bash
curl -s https://<messaging simulator address>/health
```

```bash
curl -s https://<dashboard address>/api/health
```

Each returns `{"status":"ok",...}`. The first call to the API can take twenty seconds. The last one goes through the dashboard's own proxy to the API, which is the path the dashboard uses.

Then run a scenario from the HIS simulator's screen and watch the case appear on the dashboard and the messages appear in the messaging simulator.

To see the addresses again later:

```bash
az deployment sub show --name careloop-dev --query "properties.outputs.{dashboard:webUrl.value, api:apiUrl.value, his:hisSimulatorUrl.value, messaging:messagingSimulatorUrl.value}" -o table
```

## 6. Deploying a newer build

Pull the commit you want and run the same command with the same settings:

```bash
infra/azure/scripts/deploy.sh dev
```

Later runs skip the platform step. The API is stopped for a minute or two while the new version starts.

## What you are deploying

- Everything is **public** and nothing is restricted by address: the dashboard, the API and both simulators. This is deliberate for the prototype. Synthetic data only.
- **AI:** off unless you set `API_AI_PROVIDER=Foundry` in step 2. By default the API uses `Ai:Provider = NoOp`, its AI endpoints answer 409 ("AI is switched off"), and no model calls are made. Switched on, the API asks the agents in your Foundry project directly as its `apps` managed identity; there is no separate AI service. The three agents (`role-briefing`, `impact-summary`, `patient-notification`) must already exist in that project; the deployment never creates them, and a Foundry created with `CREATE_FOUNDRY=true` has none. The identity needs the Foundry User role on the Foundry account.
- **Timing is the demo timing**: pathway deadlines last seconds, to match the simulators.

## If something goes wrong

| Symptom | What to do |
| --- | --- |
| `AuthorizationFailed ... deployments/validate/action`, sometimes only on some attempts | Your role was granted minutes ago and is still taking effect. Wait ten minutes and run again. If it persists, you have no rights on the subscription. |
| `does not have permission to perform action 'Microsoft.Authorization/roleAssignments/write'` | You are Contributor, not Owner. Set `ASSIGN_ROLES=false` and run again. |
| The script stops at once with "This subscription cannot create PostgreSQL in …" | That region is closed to your subscription for PostgreSQL. Set `POSTGRES_LOCATION` to another region and run again; everything else stays where it is. |
| PostgreSQL fails with `SkuNotAvailable: Specified SKU is not supported in this region` | Your subscription is refused the small database size in that region, even though Azure lists it. Set `POSTGRES_LOCATION=northeurope` (or another region; the README lists what was found) and run again. Everything else stays where it is. |
| PostgreSQL fails with "Specified server name is already used by another server" straight after a failure | The name is held for a few minutes after a refused attempt. Wait five minutes and run again. |
| The image build fails saying ACR Tasks are not permitted | Some free and credit-based subscriptions block building in Azure. Install Docker, start it, and add `BUILD_MODE=docker` to your settings. |
| The `web` image build fails with thousands of lint warnings in `node_modules` | You are on a commit from before the fix to `src/web/.oxlintrc.json` (`ignorePatterns` must include `node_modules` and `dist`). Update to a commit that has it. |
| "Environment dev already exists in …" | You changed `AZURE_LOCATION` or `RESOURCE_GROUP` after the first run. Go back to the original values, or remove the environment first. (`POSTGRES_LOCATION` may be changed: it creates a new, empty database in the new region, and you then delete the old server yourself.) |
| A name is already in use by a soft-deleted Key Vault | You deleted this environment recently. Purge it (see "Removing it") or deploy under another prefix: add `NAME_PREFIX=<up to 8 letters>` to your settings. |
| The script says a secret could not be read from Key Vault | Someone else deployed this environment. Ask them for the values, or set `POSTGRES_ADMIN_PASSWORD` and `SIMULATOR_INBOUND_SECRET` to new values to rotate them. |
| An AI endpoint answers 503 "AI could not write this" | The API could not get an answer from the Foundry agent. Check that the `apps` identity has the Foundry User role on the Foundry account, that `AZURE_AI_PROJECT_ENDPOINT` names the project, and that the agents `role-briefing`, `impact-summary` and `patient-notification` exist there. The API log gives the reason. |
| The dashboard loads but shows no data | Check the API: `az containerapp logs show -n api -g <resource group> --tail 100` (the CLI offers to install the `containerapp` extension the first time). |

To see why a deployment failed, list the failed steps (use your resource group's name):

```bash
az deployment group list -g <resource group> --query "[?properties.provisioningState=='Failed'].name" -o tsv
```

```bash
az deployment operation group list -g <resource group> -n <step name> --query "[?properties.provisioningState=='Failed'].properties.statusMessage.error.message" -o tsv
```

## Removing it

After the submission has been judged. The database is the part that costs money while it exists.

If the deployment created `rg-careloop-dev`:

```bash
az group delete --name rg-careloop-dev --yes
```

If you deployed into an existing resource group, do **not** delete the group. Delete only this environment's resources; run this until it lists nothing, because some deletions are refused until the things that depend on them are gone:

```bash
az resource list --resource-group <group> --query "[?tags.application=='careloop' && tags.environment=='dev'].id" -o tsv | xargs -n 1 az resource delete --ids
```

Key Vault is kept in a deleted state for a while, which blocks deploying the same environment again. To clear it:

```bash
az keyvault list-deleted --query "[].name" -o tsv
```

```bash
az keyvault purge --name <name>
```
