import Link from "next/link";
import { AuthForm } from "@/components/AuthForm";

export const metadata = { title: "Create account — ForgeRise" };
export const dynamic = "force-dynamic";

export default function RegisterPage() {
  return (
    <main className="min-h-screen flex items-center justify-center bg-mist-grey px-6 py-16">
      <div className="w-full max-w-sm">
        <p className="text-sm uppercase tracking-widest text-rise-copper font-heading text-center">
          ForgeRise
        </p>
        <h1 className="mt-2 mb-2 text-center font-heading text-3xl text-forge-navy">
          Create your coach account
        </h1>
        <p className="mb-8 text-center text-sm text-slate">
          Get session plans, attendance, and welfare-aware suggestions.
        </p>
        <AuthForm mode="register" />
        <p className="mt-6 text-center text-sm text-slate">
          Already have an account?{" "}
          <Link href="/login" className="text-forge-navy underline">
            Sign in
          </Link>
        </p>
      </div>
    </main>
  );
}
