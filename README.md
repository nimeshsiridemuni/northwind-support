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

## Task 1.1 — Filter defect (DEFECT-117)

The problem was in the ticket list hook. Its effect watched search
and pagination changes, but missed status and the other filters.
This meant changing Status did not send a request. When the user
typed in Search, that request also included the status selected earlier.

I fixed this by keeping the search input separate from the filters
used for requests. Search waits until the user stops typing for
300 ms, while other filter changes send a request immediately.
The effect now watches the complete query state, so future filters
can follow the same approach.

I added a regression test for changing Status without typing in
Search. It failed before the fix and passed afterwards. The existing
search test also passed, along with the TypeScript and lint checks.

## Task 1.2 — Ticket filtering

Ticket filters now apply in the database query before counting,
sorting, and pagination. Status, priority, category, customer,
assigned agent, and unassigned-only filters use exact matches.
When several filters are supplied, all must match.

Search checks the ticket title, reference, and customer name.
Leading and trailing spaces are removed, and blank searches
are ignored.

Filtering is grouped in an ApplyFilters helper so additional
filters can be added in one place.

Three tests use an in-memory SQLite database to check exact
priority filtering, combined filters, and customer-name search.
They also check that the total count is calculated before
pagination. The full test suite passed: 33 backend tests and
3 frontend tests.
