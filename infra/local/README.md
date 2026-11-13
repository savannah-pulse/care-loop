# Local environment

The root `docker-compose.yml` runs PostgreSQL, the API, the web starter, the Python AI workflow service and both simulators locally. Copy `.env.example` to `.env` to change host ports. PostgreSQL is the assumed coordination store (not yet confirmed; see `AGENTS.md`); the API applies its EF Core migrations at startup in this stack only.
