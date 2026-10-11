# Approach

## Task 1 — Diagnose before changing the feature

I followed the filter value from the UI into the ticket-list hook.
Status changed in state, but the request effect did not depend on it.
This explained why typing in Search appeared to make the status filter work.

I added a status-only regression test, observed its failure, and separated
debounced search from immediately applied filters. I kept the two Task 1
changes in separate commits.

For backend filtering, I applied conditions to the database query before
counting and pagination. Filtering after pagination would produce incomplete
pages and incorrect totals. SQLite tests checked exact matches, combined
filters, customer-name search, and counts.

## Task 2 — Share the rules and preserve history

I put priority and SLA calculations in one policy used by creation and
escalation. Window lengths, the Premium multiplier, minimum window, and
At Risk threshold come from configuration.

Agent eligibility is queried separately from the ticket rules. Selection
uses open workload and agent ID for predictable ties. No eligible agent
leaves the ticket unassigned.

Escalation validates the ticket before changing it. The history entry captures
the previous values, then the ticket and history are saved together.
I stored the SLA start time because escalation starts a new window; using
creation time would give the wrong remaining-window percentage.

SLA state remains derived rather than stored, since it changes as time passes.
I checked its SQL Server filter against the actual database before relying
on it for counts and pagination.

## Task 3 — Reuse agents as accounts

The existing Agent aggregate already had email and active status, so I added
a nullable password hash instead of creating a separate user model.
Existing agents can receive development hashes without replacing saved ones.

I used Microsoft's password hasher rather than implementing password
cryptography. Token validation checks issuer, audience, signature, and
expiry. A central policy protects API endpoints, and active status is
checked on authenticated requests.

The frontend uses one request client for bearer tokens and 401 handling.
Protected routes preserve the original destination. I chose sessionStorage
for refresh support within a tab and documented its XSS tradeoff.

Once authentication was available, I removed the actor input and made the
server derive escalation identity from the signed token.

## Task 4 — Verify the running image

I separated build and runtime stages and copied project files before source
to preserve restore caching. Runtime configuration comes from environment
variables, and the final image runs as a non-root user.

Compose waits for SQL Server health. The public health endpoint checks
database reachability rather than only confirming that the process is running.

I verified a fresh database using a separate Compose project so the existing
development data remained available. The fresh instance seeded 40 tickets
and accepted the development login. I also checked restore caching after
a source change, image size, the container user, and the frontend proxy.

A test database port conflicted with an existing listener. I removed its
host port mapping because the test API could reach SQL Server through
Docker's internal network.

## Verification and scope

I worked through the tasks in order and committed as the implementation
progressed. Automated checks were combined with manual SQL Server, API,
browser, and Docker checks.

AI helped draft and explain changes, but its output was checked and corrected.
Examples included the React purity error and the incorrect return type in a
test mock. I documented remaining concurrency, token-storage, and testing
limitations instead of expanding into features outside the assessment.
