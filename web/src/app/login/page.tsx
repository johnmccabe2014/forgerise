import Link from "next/link";
import { AuthForm } from "@/components/AuthForm";

export const metadata = { title: "Sign in — ForgeRise" };

export default function LoginPage() {
  return (
    <main className="min-h-screen flex items-center justify-center bg-mist-grey px-6 py-16">
      <div className="w-full max-w-sm">
        <p className="text-sm uppercase tracking-widest text-rise-copper font-heading text-center">
          ForgeRise
        </p>
        <h1 className="mt-2 mb-8 text-center font-heading text-3xl text-forge-navy">
          Sign in
        </h1>
        <AuthForm mode="login" />
        <p className="mt-6 text-center text-sm text-slate">
          New here?{" "}
          <Link href="/register" className="text-forge-navy underline">
            Create an account
          </Link>
        </p>
      </div>
    </main>
  );
}
