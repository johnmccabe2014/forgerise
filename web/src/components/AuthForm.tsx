"use client";

import { useState, type FormEvent } from "react";
import { useRouter } from "next/navigation";

export type AuthFormMode = "login" | "register";

export interface AuthFormProps {
  mode: AuthFormMode;
}

export function AuthForm({ mode }: AuthFormProps) {
  const router = useRouter();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [displayName, setDisplayName] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setError(null);

    if (mode === "register" && password.length < 12) {
      setError("Password must be at least 12 characters.");
      return;
    }

    setBusy(true);
    try {
      const body =
        mode === "login"
          ? { email, password }
          : { email, password, displayName };

      const res = await fetch(`/api/proxy/auth/${mode}`, {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify(body),
      });

      if (!res.ok) {
        const text = await res.text();
        setError(humaniseAuthError(res.status, text));
        return;
      }
      router.replace("/dashboard");
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
      className="w-full max-w-sm space-y-4"
      aria-label={mode === "login" ? "Sign in" : "Create account"}
    >
      {mode === "register" && (
        <Field
          label="Your name"
          name="displayName"
          autoComplete="name"
          value={displayName}
          onChange={setDisplayName}
          required
        />
      )}
      <Field
        label="Email"
        name="email"
        type="email"
        autoComplete="email"
        value={email}
        onChange={setEmail}
        required
      />
      <Field
        label="Password"
        name="password"
        type="password"
        autoComplete={mode === "login" ? "current-password" : "new-password"}
        value={password}
        onChange={setPassword}
        required
        hint={
          mode === "register" ? "At least 12 characters." : undefined
        }
      />

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
        {busy
          ? mode === "login"
            ? "Signing in…"
            : "Creating account…"
          : mode === "login"
            ? "Sign in"
            : "Create account"}
      </button>
    </form>
  );
}

interface FieldProps {
  label: string;
  name: string;
  value: string;
  onChange: (v: string) => void;
  type?: string;
  required?: boolean;
  autoComplete?: string;
  hint?: string;
}

function Field({
  label,
  name,
  value,
  onChange,
  type = "text",
  required,
  autoComplete,
  hint,
}: FieldProps) {
  const id = `f-${name}`;
  const hintId = hint ? `${id}-hint` : undefined;
  return (
    <div>
      <label htmlFor={id} className="block text-sm text-slate font-medium">
        {label}
      </label>
      <input
        id={id}
        name={name}
        type={type}
        required={required}
        autoComplete={autoComplete}
        value={value}
        onChange={(e) => onChange(e.target.value)}
        aria-describedby={hintId}
        className="mt-1 w-full rounded-xl border border-slate/30 bg-white px-3 py-2 text-deep-charcoal focus:outline-none focus:ring-2 focus:ring-rise-copper"
      />
      {hint && (
        <p id={hintId} className="mt-1 text-xs text-slate">
          {hint}
        </p>
      )}
    </div>
  );
}

function humaniseAuthError(status: number, body: string): string {
  if (status === 401) return "Email or password is incorrect.";
  if (status === 409) return "An account with that email already exists.";
  if (status === 429) return "Too many attempts. Please wait a moment.";
  // Try to extract ProblemDetails.title without ever rendering raw user input.
  try {
    const parsed = JSON.parse(body) as { title?: unknown };
    if (typeof parsed.title === "string" && parsed.title.length < 200) {
      return parsed.title;
    }
  } catch {
    /* ignore */
  }
  return "Something went wrong. Please try again.";
}
