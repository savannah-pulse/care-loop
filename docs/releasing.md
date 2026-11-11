# Build and release

## Continuous integration

The `CI` workflow runs on pull requests and pushes to `main`. It restores, builds, and tests the .NET solution, then installs the locked web dependencies, runs Oxlint, type-checks, and builds the React app.

Run the same checks locally:

```sh
dotnet restore CareLoop.sln
dotnet build CareLoop.sln --configuration Release --no-restore
dotnet test CareLoop.sln --configuration Release --no-build
cd src/web
npm ci
npm run lint
npm run build
```

## Continuous delivery

Pushing a `v*` tag runs the `Release` workflow. It publishes the API and builds the static web bundle into a versioned zip, then attaches that artifact to a GitHub Release. This is a build-and-package delivery step; it does not deploy to a hosted runtime.

No hosting platform, environment, approval policy, or release owner has been selected. Configure and review those with the team before adding automatic deployment or production credentials. Releases from this scaffold are not for handling real health data.
