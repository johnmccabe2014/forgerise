# Skill: observability

If you cannot see it in production, it does not work in production.

## Signals
- **Logs**: structured (JSON), levelled, with correlation ID, request ID, user/tenant ID (hashed if sensitive). No PII, no secrets, no full payloads of sensitive ops.
- **Metrics**: RED for services (Rate, Errors, Duration); USE for resources (Utilisation, Saturation, Errors). Business KPIs where relevant.
- **Traces**: spans across service boundaries; propagate context.

## Required for every change
- New endpoint / job → at least one log line at start + end, one metric (count + duration), one error path log.
- New error class → mapped to a known severity + alert policy (or explicitly "info only").
- New dependency call → timed, with timeout, and with a circuit-breaker or retry policy documented.

## Alerting
Alert on symptoms (latency, error rate, saturation), not causes. Every alert links to a runbook entry; if no runbook, no alert.
