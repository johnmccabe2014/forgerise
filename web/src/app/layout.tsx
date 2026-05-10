import type { Metadata } from "next";
import { headers } from "next/headers";
import Splash from "@/components/Splash";
import ThemeToggle from "@/components/ThemeToggle";
import "./globals.css";

export const metadata: Metadata = {
  title: "ForgeRise — Ops Intelligence for Coaches",
  description:
    "ForgeRise helps grassroots and pathway coaches save time, support player welfare, and make better training decisions from minimal manual input.",
};

// Inline script that runs before paint to apply the persisted (or system)
// theme. This prevents a light-flash when the user has chosen dark mode.
const themeInitScript = `(() => {
  try {
    const stored = localStorage.getItem('forgerise.theme');
    const prefersDark = window.matchMedia('(prefers-color-scheme: dark)').matches;
    const theme = stored === 'dark' || stored === 'light' ? stored : (prefersDark ? 'dark' : 'light');
    if (theme === 'dark') document.documentElement.classList.add('dark');
  } catch (_) {}
})();`;

export default async function RootLayout({
  children,
}: Readonly<{ children: React.ReactNode }>) {
  // Pull the per-request CSP nonce minted in middleware so our inline
  // theme bootstrap is allowed under script-src 'nonce-...' 'strict-dynamic'.
  const nonce = (await headers()).get("x-nonce") ?? undefined;

  return (
    <html lang="en">
      <head>
        <script nonce={nonce} dangerouslySetInnerHTML={{ __html: themeInitScript }} />
      </head>
      <body className="font-body antialiased">
        <Splash />
        <div className="fixed bottom-4 right-4 z-50">
          <ThemeToggle />
        </div>
        {children}
      </body>
    </html>
  );
}
