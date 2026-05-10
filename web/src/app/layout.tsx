import type { Metadata } from "next";
import Splash from "@/components/Splash";
import "./globals.css";

export const metadata: Metadata = {
  title: "ForgeRise — Ops Intelligence for Coaches",
  description:
    "ForgeRise helps grassroots and pathway coaches save time, support player welfare, and make better training decisions from minimal manual input.",
};

export default function RootLayout({
  children,
}: Readonly<{ children: React.ReactNode }>) {
  return (
    <html lang="en">
      <body className="font-body antialiased">
        <Splash />
        {children}
      </body>
    </html>
  );
}
