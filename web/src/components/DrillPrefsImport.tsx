"use client";

import { useState, useTransition } from "react";
import { useRouter } from "next/navigation";

interface ImportResult {
  applied: number;
  cleared: number;
  skipped: number;
  errors: { line: number; reason: string }[];
}

export interface DrillPrefsImportProps {
  teamId: string;
}

/**
 * Coach-facing CSV bulk import for drill preferences. Pasted CSV is sent to
 * the API which returns a per-row summary; we surface counts and a short
 * list of any errors so the coach can fix the source spreadsheet and retry.
 */
export function DrillPrefsImport({ teamId }: DrillPrefsImportProps) {
  const router = useRouter();
  const [csv, setCsv] = useState("");
  const [pending, startTransition] = useTransition();
  const [busy, setBusy] = useState(false);
  const [result, setResult] = useState<ImportResult | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function submit(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault();
    if (!csv.trim()) return;
    setBusy(true);
    setError(null);
    setResult(null);
    try {
      const res = await fetch(
        `/api/proxy/teams/${teamId}/drill-preferences/import`,
        {
          method: "POST",
          headers: { "content-type": "application/json" },
          body: JSON.stringify({ csv }),
        },
      );
      if (!res.ok) {
        setError("Import failed. Check the CSV and try again.");
        return;
      }
      const body = (await res.json()) as ImportResult;
      setResult(body);
      startTransition(() => router.refresh());
    } catch {
      setError("Network error. Please try again.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <details
      className="rounded-card bg-white p-4 shadow-soft"
      data-testid="drill-prefs-import"
    >
      <summary className="cursor-pointer font-medium text-deep-charcoal">
        Bulk import from CSV
      </summary>
      <form className="mt-3 space-y-3" onSubmit={submit}>
        <p className="text-xs text-slate">
          Two columns: <code>drillId,status</code>. Status is{" "}
          <code>favourite</code>, <code>exclude</code>, or <code>clear</code>.
          A header row is allowed.
        </p>
        <textarea
          data-testid="drill-prefs-import-textarea"
          value={csv}
          onChange={(e) => setCsv(e.target.value)}
          rows={6}
          className="w-full rounded border border-slate/30 bg-mist-grey/40 p-2 font-mono text-xs text-deep-charcoal"
          placeholder={"drillId,status\nmobility-flow,favourite\nconditioned-game,exclude"}
        />
        <div className="flex items-center gap-3">
          <button
            type="submit"
            disabled={busy || pending || !csv.trim()}
            className="rounded-pill bg-rise-copper px-4 py-1.5 text-sm font-medium text-white disabled:opacity-60"
          >
            {busy ? "Importing…" : "Import"}
          </button>
          {error && (
            <span
              role="alert"
              data-testid="drill-prefs-import-error"
              className="text-xs text-red-600"
            >
              {error}
            </span>
          )}
        </div>
        {result && (
          <div
            data-testid="drill-prefs-import-result"
            className="rounded bg-mist-grey/60 p-3 text-xs text-deep-charcoal space-y-1"
          >
            <p>
              Applied {result.applied}, cleared {result.cleared}, skipped{" "}
              {result.skipped}.
            </p>
            {result.errors.length > 0 && (
              <ul className="list-disc pl-4 text-red-700">
                {result.errors.slice(0, 5).map((err) => (
                  <li key={`${err.line}-${err.reason}`}>
                    line {err.line}: {err.reason}
                  </li>
                ))}
                {result.errors.length > 5 && (
                  <li>…and {result.errors.length - 5} more</li>
                )}
              </ul>
            )}
          </div>
        )}
      </form>
    </details>
  );
}
