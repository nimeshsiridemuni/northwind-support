# Northwind Support Desk

A support ticket management system: raise tickets, search and filter them, assign them to agents,
and move them through their lifecycle.

**.NET 10 Web API** + **React 19 / TypeScript** + **SQL Server**, in one monorepo.

---

## The assignment

You are joining a team that already has this app working. Complete the tasks **in order**.
Full requirements are in **[`ASSIGNMENT_CANDIDATE.md`](ASSIGNMENT_CANDIDATE.md)**.

| #   | Task                                        | Done looks like                                                                                         | Effort |
| --- | ------------------------------------------- | ------------------------------------------------------------------------------------------------------- | ------ |
| 1   | **Fix a defect, add server-side filtering** | Root cause fixed with a regression test; every list filter applied in the database with a correct count | ~2.5 h |
| 2   | **Ticket escalation & assignment**          | Automatic triage on create, escalation with history, SLA status, UI for all of it                       | ~7 h   |
| 3   | **Login and authentication**                | Agents sign in; API rejects unauthenticated calls; login UI                                             | ~4 h   |
| 4   | **Dockerise the API** _(optional)_          | `docker compose up` gives a migrated, seeded API with no baked-in secret                                | ~1.5 h |

### Guidance

- **Time: 2 days.** Honestly marking a task "not finished" beats rushing all of them.
- **Fork** this repo to your own GitHub account and work there. **Do not push to the original.**
- **AI tools are allowed.** You must understand and be able to defend every line you submit.
- **Commit as you go** and keep the real history. Commit the Task 1 defect fix and the filtering
  separately from each other and from the feature work. A single squashed commit is not accepted.
- **Where the spec is ambiguous**, make a decision, write it down in the README, and move on.
- **Out of scope:** registration, password reset, roles, OAuth/SSO, refresh tokens, MFA,
  notifications, CI, caching, visual redesign. Mention them under _Known limitations_ instead.

### Submission

1. Update this **README** with what you built, your design decisions (chosen, rejected and why),
   the defect's root cause, dev login credentials, how to run and test it, AI usage, and known
   limitations. A reviewer must be able to run it without asking you anything.
2. Add an **APPROACH.md** explaining your approach and thought process for each task.
3. Commit both, then send us the **link to your fork**, the **dev login credentials**, and a
   **rough note of time spent** on what. (If the fork is private, add the reviewers as collaborators.)

---

## Quick start

**Prerequisites:** .NET SDK 10 (`global.json`), Node.js 22 (`.nvmrc`; 20.19+ works), Docker.

```bash
cp .env.example .env          # optional, every value has a working default
npm install                   # installs the web app
npm run db:up                 # SQL Server 2022 in Docker on localhost:1433
npm run api                   # terminal 1: migrates, seeds, then serves the API
npm run web                   # terminal 2: React dev server
```

| What    | Where                                               |
| ------- | --------------------------------------------------- |
| Web app | http://localhost:5173                               |
| API     | https://localhost:7043 (also http://localhost:5043) |
| Swagger | https://localhost:7043/swagger                      |

In Development the API applies migrations and seeds demo data on startup. **Never commit real
credentials.**

**Visual Studio 2026:** run `npm run db:up`, open `SupportDesk.sln`, set
**SupportDesk.Presentation** as the startup project, and press F5 on the **https** profile.

## Tests

```bash
npm test                      # dotnet test, then the web tests (Vitest)
```

Both pass on a clean checkout and neither needs a database.

## Commands

| Command                                  | What it does                         |
| ---------------------------------------- | ------------------------------------ |
| `npm run db:up` / `db:down` / `db:reset` | Start / stop / wipe SQL Server       |
| `npm run api` / `npm run web`            | Run the API / the React app          |
| `npm run build`                          | Build both                           |
| `npm run lint` / `typecheck` / `format`  | Web lint and types / `dotnet format` |
| `npm run ef -- migrations add <Name>`    | Add an EF Core migration             |

## API at a glance

| Endpoint                                                 | Description                                                                                                                                                                                        |
| -------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `GET /api/tickets`                                       | Paged list with `search`, `status`, `priority`, `categoryId`, `customerId`, `assignedAgentId`, `unassignedOnly`, `sortBy`, `sortDirection`. **Filters are accepted but not applied yet** (Task 1). |
| `GET /api/tickets/{id}`                                  | One ticket                                                                                                                                                                                         |
| `POST /api/tickets`                                      | Create a ticket                                                                                                                                                                                    |
| `PATCH /api/tickets/{id}/status` · `/assignment`         | Change status / assignee                                                                                                                                                                           |
| `GET /api/customers` · `/api/agents` · `/api/categories` | Reference data                                                                                                                                                                                     |

Errors are `ProblemDetails`: `400` validation, `404` not found, `409` conflict or business rule.

**Seed data:** 5 categories, 6 customers (3 Premium), 6 agents (one inactive, one at their limit)
and 40 tickets across every status, priority and SLA state.

## Troubleshooting

| Symptom                                | Fix                                                                         |
| -------------------------------------- | --------------------------------------------------------------------------- |
| API cannot reach the database          | `npm run db:up`, wait ~20 s, check `docker compose ps`                      |
| Port 1433 already in use               | Change the port in `docker-compose.yml` and `.env`                          |
| Browser warns about the certificate    | `dotnet dev-certs https --trust`, or use the web app, which proxies         |
| Web app shows "Unable to load tickets" | The API is not running, or `VITE_API_PROXY_TARGET` points at the wrong port |
| SQL Server slow on Apple Silicon       | Enable Rosetta in Docker Desktop → Settings → General                       |

# Candidate Submission Notes - Nimesh Siridemuni

# Northwind Support Desk

A support desk built with .NET 10, React, TypeScript, and SQL Server.
The submission includes ticket filtering, automatic triage and assignment,
SLA tracking, escalation history, agent login, and a Dockerised API.

All four assessment tasks are implemented. The work was committed as it
progressed. Design notes are in [APPROACH.md](APPROACH.md).

## Run with Docker

Prerequisites: Docker Desktop with Linux containers, Node.js, and npm.
The .NET 10 SDK is also required to run backend tests locally.

Run from the repository root in PowerShell:

```powershell
Copy-Item .env.example .env

$dockerJwtKey = [Guid]::NewGuid().ToString("N") + [Guid]::NewGuid().ToString("N")
$envText = (Get-Content .env -Raw) -replace '(?m)^JWT_SIGNING_KEY=.*$', "JWT_SIGNING_KEY=$dockerJwtKey"
Set-Content .env $envText

docker compose up -d --build --wait
npm install
```

In a separate terminal:

```powershell
$env:VITE_API_PROXY_TARGET = "http://localhost:5080"
npm run web
```

Open http://localhost:5173.

Development login:

- Email: `alex.turner@northwind-support.example`
- Password: `Local_Agent_Password1`

| Service             | Address                       |
| ------------------- | ----------------------------- |
| Frontend            | http://localhost:5173         |
| Container API       | http://localhost:5080/api     |
| Database health     | http://localhost:5080/health  |
| Development Swagger | http://localhost:5080/swagger |

Compose reads `.env`. The JWT key is required; the example leaves it blank
so each developer generates their own. The database and agent passwords
in `.env.example` are local development fixtures. Do not use them for
production or commit `.env`.

Development startup applies migrations, seeds 40 demo tickets on an empty
database, and adds password hashes to demo agents without existing hashes.
Existing hashes are preserved. Changing the seed password does not reset
existing credentials.

`docker compose down` stops and removes the containers while keeping the
database volume. Adding `-v` deletes the database volume and its data.

## Run the API directly

Use the .NET 10 SDK. Set the API's local secrets:

```powershell
$jwtSigningKey = [Guid]::NewGuid().ToString("N") + [Guid]::NewGuid().ToString("N")

dotnet user-secrets set "Jwt:SigningKey" "$jwtSigningKey" --project apps/api/src/SupportDesk.Presentation

dotnet user-secrets set "DevelopmentAuth:Password" "Local_Agent_Password1" --project apps/api/src/SupportDesk.Presentation

npm run db:up
npm run api
```

The root `.env` still needs the Docker signing key because Compose validates
its configuration when starting SQL Server.

The direct API listens on http://localhost:5043 and https://localhost:7043.
To use it from the frontend:

```powershell
$env:VITE_API_PROXY_TARGET = "http://localhost:5043"
npm run web
```

Root `.env` values are not automatically loaded by `dotnet run`; use user
secrets or process environment variables for direct API configuration.

## Task 1 — Defect and filtering

DEFECT-117 came from the ticket-list effect watching search and pagination
but missing the other filters. Changing Status updated the controls without
making a request. Typing in Search then sent the previously selected status.

The hook now separates the search input from the applied query. Search waits
for a 300 ms pause in typing; other filters apply immediately. The request
effect watches the complete query state. The status-only regression test
failed before the fix and passed afterward.

Server-side filters apply before counting, sorting, and pagination.
Status, priority, category, customer, assigned agent, and unassigned-only
filters use exact matches and combine with AND. Search trims whitespace
and checks title, reference, and customer name. Filtering is grouped in
`ApplyFilters`, with a separate helper for derived SLA filtering.

## Task 2 — Triage, SLA, and escalation

New tickets use the requested priority, defaulting to Medium. A category's
Critical flag overrides that selection.

SLA windows come from the `Sla` configuration section. Defaults are Critical
4 hours, High 8, Medium 24, and Low 72. Premium customers receive half the
window, with a minimum of one hour. Create and escalate share the same policy.

Assignment considers active agents below their open-ticket limit. Categories
requiring a specialist also require a matching specialization. The lowest
open-ticket count wins, with agent ID breaking ties. If nobody qualifies,
the ticket stays unassigned. Resolved and Closed tickets do not count as open.

Escalation raises priority one level and starts a new SLA window from the
escalation time. An eligible owner is retained; otherwise, another eligible
agent is selected. Critical, Resolved, and Closed tickets are rejected
without saving changes. Ticket changes and the history entry are saved
through the same unit of work.

SLA state is derived from timestamps. Unresolved tickets become At Risk
when 25% or less of the current window remains. The exact deadline is
At Risk; a later time is Breached. Resolution on or before the deadline
is Met. Tickets without a deadline have No SLA.

The frontend shows triage results, uses one SLA indicator, supports SLA
filtering, and displays escalation history newest first. Escalation validates
the reason, disables submission while pending, and preserves the displayed
ticket when the API rejects the request.

Database changes add the SLA start timestamp, password hash, and escalation
history. Older tickets use creation time when no SLA start is stored.
History preserves previous and new priority, owner, and deadline, plus
reason, actor, and time. Restricted foreign keys preserve its references.
The `(TicketId, EscalatedAtUtc, Id)` index supports per-ticket history lookup
and stable ordering.

## Task 3 — Authentication

Agents sign in with email and password. Microsoft's PasswordHasher stores
salted hashes and supports hash upgrades after successful verification.
Wrong credentials and inactive accounts return the same 401 response.

Login returns a JWT and agent details. Tokens expire after 30 minutes by
default. The API checks signature, issuer, audience, expiry, and whether
the agent is still active. A central authorization policy protects endpoints.
Login and database health are public; Swagger is available in Development.

The frontend validates login, shows errors, and disables the form while
pending. Protected routes preserve the requested destination. The API client
attaches the token and clears the session after a protected request returns 401. Expiry and logout also clear the session.

I chose sessionStorage to retain login across refreshes in the same tab.
It is accessible to JavaScript, so preventing XSS remains important.
Logout clears the browser session but does not revoke an issued token.

Escalation accepts only the reason from the browser. The server records
the authenticated agent's name and ID, such as `Alex Turner (#1)`.

## Task 4 — Docker

`apps/api/Dockerfile` builds from the repository root. It restores project
files and shared build configuration before copying source, then publishes
with the .NET 10 SDK. The final image contains the ASP.NET runtime and
published application, runs as `app`, and listens on HTTP port 8080.

`.dockerignore` excludes build outputs, dependencies, Git data, local
environment files, certificates, and the web app. Published appsettings files
are removed from the runtime image. Connection string, signing key, and
environment name are supplied at runtime.

Compose exposes the API on localhost:5080, connects to SQL Server by service
name, and waits for database health before starting the API. `/health` checks
database connectivity without requiring a token.

Verification completed:

- Both containers became healthy; `/health` returned `Healthy`.
- A separate Compose project with an empty volume seeded 40 tickets and
  allowed Alex Turner to log in. It used API port 5081 and was removed afterward.
- Changing a C# file rebuilt the application while `RUN dotnet restore`
  remained `CACHED`.
- `docker compose exec api whoami` returned `app`.
- Docker reported the API image as 464 MB and the SDK image as 1.3 GB.
- The frontend worked with `VITE_API_PROXY_TARGET=http://localhost:5080`.

Automatic migrations and demo seeding are Development conveniences.
For production, I would apply a reviewed migration script or migration
bundle as a separate deployment step using a migration account. This avoids
multiple API instances competing to change the schema and lets the running
application use narrower database permissions. Production startup does not
seed development credentials.

## Testing

Stop the directly running API before building on Windows to avoid executable
file locks.

```powershell
npm run test
npm run build
npm run typecheck
npm run lint
```

The latest focused runs passed 101 backend unit tests and 18 frontend tests.
`npm run test` also runs the architecture tests. Automated tests do not require
the Docker SQL Server.

Tests cover filter counts and pagination, combined filters, triage rules,
assignment capacity and specialization, SLA boundaries, escalation rules and
history, password verification, login, redirects, logout, and session expiry.

Manual checks covered SQL Server SLA filtering across all five states,
login and `/api/auth/me`, rejected anonymous requests, inactive agents,
authenticated escalation history, Docker startup, and frontend connectivity.
The SQL Server-specific SLA filter was checked against the running database;
SQLite tests cover the other query filters.

## AI usage

I used ChatGPT/Codex to help inspect the defect, draft code and tests, explain
errors, and prepare setup instructions. I applied the changes locally and
checked them through tests, builds, lint, typechecking, and API/browser checks.

I retained the shared triage-policy approach because create and escalate
need the same rules. I changed the temporary actor input to use the authenticated
identity once login was implemented.

AI-generated code also needed corrections: `Date.now()` in React render failed
the purity lint rule, so expiry handling moved into the session store. A test
mock returned `Task` instead of `Task<int>` and was corrected. These failures
reinforced the need to verify generated code.

## Known limitations

- Assignment eligibility is checked before saving; concurrent requests could
  exceed an agent's capacity. No concurrency stress test was performed.
- Session storage is exposed to JavaScript. There are no refresh tokens or
  server-side logout revocation.
- Automated tests do not replace a full browser end-to-end suite or load tests.
- Registration, password reset, roles, SSO, MFA, notifications, background jobs,
  CI/CD, and deployment beyond the Docker setup are outside this assessment.
