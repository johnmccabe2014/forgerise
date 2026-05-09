# Welfare data handling

ForgeRise treats player welfare data as the most sensitive class of data in the
system. This note documents how raw welfare inputs flow through the API, how
they are kept out of coach-facing surfaces, how every raw read is audited, and
how raw fields are retired on a schedule.

## Scope

Two record types are covered:

| Entity            | Raw fields                                                              | Coach-safe projection             |
|-------------------|-------------------------------------------------------------------------|-----------------------------------|
| `WellnessCheckIn` | `sleepHours`, `sorenessScore`, `moodScore`, `stressScore`, `fatigueScore`, `injuryNotes` | `Category` (Ready / Monitor / ModifyLoad / RecoveryFocus) |
| `IncidentReport`  | `Notes`                                                                 | `Severity`, `Summary` (≤ 280 chars), `OccurredAt`         |

`RawWelfareFields.Names` (`api/ForgeRise.Api/Welfare/RawWelfareFields.cs`) is the
single source of truth for which property names are considered raw.

## Data minimisation

* The check-in `Category` is computed at write time by `ReadinessCategorizer`
  and stored on the row. It is the only piece of welfare data that should ever
  be displayed to a coach. Even after `RawPurgedAt` clears the raw fields, the
  `Category` snapshot survives so historical readiness rollups remain truthful.
* Coach-safe endpoints — `GET /teams/{id}/readiness`,
  `GET /teams/{id}/incidents`, the check-in/incident summary endpoints — never
  serialise a raw field. Tests assert that the response body does not contain
  the raw substring after a write.
* Incident `Summary` is intended to be a one-line, coach-safe headline (e.g.
  "Knock to shin during contact drill"). Any clinical detail belongs in
  `Notes`.

## Defence in depth: structured logging

`WelfareDestructuringPolicy` is registered with Serilog at startup. Whenever a
log call destructures an object (`{@Foo}`) and that object exposes a property
whose name matches `RawWelfareFields.Names`, the value is replaced with
`[REDACTED]` before the event is rendered. This protects against accidental
leakage if a developer logs a request DTO or an entity directly. See
`api/ForgeRise.Api.Tests/Welfare/WelfareLoggingTests.cs`.

Hand-written welfare log lines (`welfare.checkin.recorded`,
`welfare.incident.recorded`, `*.raw_read`, `*.raw_purged`, `*.deleted`) only
include identifiers, the actor's user id, and — for check-ins — the resulting
`Category`. They never include scores, sleep hours, or notes.

## Audit trail for raw access

The only way to retrieve raw welfare data is via a dedicated `/raw` endpoint
(`GET /teams/{teamId}/players/{playerId}/checkins/{id}/raw` and the equivalent
incident path). Each call writes a `WelfareAuditLog` row with the actor's user
id, the player id, the subject id, and one of:

* `ReadRawCheckIn` / `ReadRawIncident`
* `PurgeRawCheckIn` / `PurgeRawIncident`
* `DeleteCheckIn` / `DeleteIncident`

The audit log is exposed at `GET /teams/{teamId}/welfare-audit` to the team
owner only. It is append-only; there is no edit or delete endpoint.

## Authorisation

Every welfare endpoint requires authentication and is gated by
`TeamScope.RequireOwnedTeam` / `RequireOwnedPlayer`, which return 403 if the
caller is not the team owner. CSRF protection from Phase 2 applies to every
unsafe verb. There is no "league" or "admin" scope yet that can bypass team
ownership.

## Retention and right to be forgotten

* `POST /teams/{teamId}/players/{playerId}/checkins/{id}/purge-raw` (and the
  incident equivalent) clears every raw field on the row, sets `RawPurgedAt`,
  and writes a `PurgeRaw*` audit entry. The `Category` snapshot remains so
  team-level readiness history stays consistent.
* `DELETE` on a check-in or incident sets `DeletedAt` (soft delete, filtered
  out by the EF Core query filter) and writes a `Delete*` audit entry.
* A scheduled hard-delete job is **not** in scope for this iteration. Operators
  should treat `RawPurgedAt` as the contractual erasure boundary; the row
  itself is retained for audit continuity.

## Not a medical device

ForgeRise is a coaching tool. `ReadinessCategorizer` is a transparent heuristic
over self-reported scores; it does not diagnose injury or illness. UI text
must reflect this — categories are decision aids, not clinical assessments.
The threat model and product copy should say so explicitly.


## Session planning

The AI session-plan generator (`ISessionPlanGenerator`) reads only safe inputs:

- the team id
- a coach-supplied focus override (optional)
- the focus + free-form review notes of the most recent reviewed session (coach-authored, not welfare data)
- a readiness snapshot built from the latest non-deleted check-in per player, reduced to `(playerId, SafeCategory)` — never raw fields, never dates of birth, never injury notes.

The iter1 default implementation is `HeuristicSessionPlanGenerator` — a deterministic, in-process function. The interface is shaped so a future `OpenAiSessionPlanGenerator` (or Azure OpenAI equivalent) can be registered in DI without touching controllers, and must be supplied with the same context shape: any future provider therefore inherits the welfare redaction guarantee for free.

Generated plans are stored verbatim. `PlanJson` and `ReadinessSnapshotJson` are owner-scoped and contain only ids the coach already manages plus the safe `SafeCategory` enum.
