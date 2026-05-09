"use client";

import { useState, type FormEvent, type ChangeEvent } from "react";
import { useRouter } from "next/navigation";
import { slugify } from "@/lib/slugify";

interface CreatedTeam {
  id: string;
  name: string;
  code: string;
}

export function TeamCreateForm() {
  const router = useRouter();
  const [name, setName] = useState("");
  const [code, setCode] = useState("");
  const [codeTouched, setCodeTouched] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  function onNameChange(e: ChangeEvent<HTMLInputElement>) {
    const v = e.target.value;
    setName(v);
    if (!codeTouched) setCode(slugify(v));
  }

  function onCodeChange(e: ChangeEvent<HTMLInputElement>) {
    setCode(e.target.value);
    setCodeTouched(true);
  }

  async function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setError(null);
    if (!name.trim() || !code.trim()) {
      setError("Name and code are both required.");
      return;
    }
    setBusy(true);
    try {
      const res = await fetch("/api/proxy/teams", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ name: name.trim(), code: code.trim() }),
      });
      if (!res.ok) {
        setError(await humaniseProblem(res));
        return;
      }
      const created = (await res.json()) as CreatedTeam;
      router.replace(`/teams/${created.id}`);
      router.refresh();
    } catch {
      setError("Network error. Please try again.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <form
      onSubmit={onSubmit}
      aria-label="Create team"
      className="w-full max-w-md space-y-4"
    >
      <div>
        <label htmlFor="team-name" className="block text-sm font-medium text-slate">
          Team name
        </label>
        <input
          id="team-name"
          name="name"
          required
          value={name}
          onChange={onNameChange}
          maxLength={120}
          className="mt-1 w-full rounded-xl border border-slate/30 bg-white px-3 py-2 focus:outline-none focus:ring-2 focus:ring-rise-copper"
        />
      </div>
      <div>
        <label htmlFor="team-code" className="block text-sm font-medium text-slate">
          Short code
        </label>
        <input
          id="team-code"
          name="code"
          required
          value={code}
          onChange={onCodeChange}
          maxLength={40}
          pattern="[A-Za-z0-9_-]+"
          className="mt-1 w-full rounded-xl border border-slate/30 bg-white px-3 py-2 focus:outline-none focus:ring-2 focus:ring-rise-copper"
        />
        <p className="mt-1 text-xs text-slate">
          Letters, numbers, hyphens, underscores. Used in URLs.
        </p>
      </div>

      {error && (
        <p role="alert" className="text-sm text-readiness-recovery">
          {error}
        </p>
      )}

      <button
        type="submit"
        disabled={busy}
        className="w-full rounded-xl bg-forge-navy py-3 text-white font-heading disabled:opacity-60"
      >
        {busy ? "Creating…" : "Create team"}
      </button>
    </form>
  );
}

async function humaniseProblem(res: Response): Promise<string> {
  if (res.status === 409) return "A team with that code already exists.";
  if (res.status === 401) return "Please sign in again.";
  try {
    const body = (await res.json()) as { title?: unknown; errors?: unknown };
    if (typeof body.title === "string" && body.title.length < 200) return body.title;
  } catch {
    /* ignore */
  }
  return "Could not create the team. Please try again.";
}
