/**
 * Lightweight structured JSON logger with correlation-ID + welfare redaction.
 * Master prompt §11: structured JSON, correlation IDs, no sensitive welfare in logs.
 *
 * In Phase 5 this will hand over to OpenTelemetry logs via the collector
 * configured in OTEL_EXPORTER_OTLP_ENDPOINT. Until then, stdout JSON is enough.
 */
import { redactWelfare } from "@/lib/welfare";

export type LogLevel = "debug" | "info" | "warn" | "error";

const LEVELS: Record<LogLevel, number> = {
  debug: 10,
  info: 20,
  warn: 30,
  error: 40,
};

const minLevel: number =
  LEVELS[(process.env.LOG_LEVEL as LogLevel) ?? "info"] ?? LEVELS.info;

export interface LogContext {
  correlationId?: string;
  [key: string]: unknown;
}

export function log(level: LogLevel, message: string, ctx: LogContext = {}) {
  if (LEVELS[level] < minLevel) return;
  const entry = {
    ts: new Date().toISOString(),
    level,
    service: process.env.OTEL_SERVICE_NAME ?? "forgerise-web",
    msg: message,
    ...redactWelfare(ctx),
  };
  console.log(JSON.stringify(entry));
}

export const logger = {
  debug: (m: string, c?: LogContext) => log("debug", m, c),
  info: (m: string, c?: LogContext) => log("info", m, c),
  warn: (m: string, c?: LogContext) => log("warn", m, c),
  error: (m: string, c?: LogContext) => log("error", m, c),
};
