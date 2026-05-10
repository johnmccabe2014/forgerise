"use client";

import Image from "next/image";
import { useEffect, useState } from "react";

const HOLD_MS = 1200;
const FADE_MS = 400;

export default function Splash() {
  const [phase, setPhase] = useState<"visible" | "fading" | "gone">(() => {
    if (typeof window === "undefined") return "visible";
    try {
      if (window.sessionStorage.getItem("forgerise.splashShown") === "1") {
        return "gone";
      }
      window.sessionStorage.setItem("forgerise.splashShown", "1");
    } catch {
      // sessionStorage may be unavailable; fall through to show splash.
    }
    return "visible";
  });

  useEffect(() => {
    if (phase !== "visible") return;
    const fadeTimer = window.setTimeout(() => setPhase("fading"), HOLD_MS);
    const goneTimer = window.setTimeout(() => setPhase("gone"), HOLD_MS + FADE_MS);
    return () => {
      window.clearTimeout(fadeTimer);
      window.clearTimeout(goneTimer);
    };
  }, [phase]);

  if (phase === "gone") return null;

  return (
    <div
      data-testid="splash"
      aria-hidden="true"
      className={`fixed inset-0 z-[9999] flex items-center justify-center bg-deep-charcoal transition-opacity duration-[400ms] ${
        phase === "fading" ? "opacity-0" : "opacity-100"
      }`}
    >
      <div className="flex flex-col items-center gap-4">
        <div className="h-24 w-24 overflow-hidden rounded-2xl bg-deep-charcoal shadow-soft animate-pulse">
          <Image
            src="/brand/crest-icon.png"
            alt="ForgeRise"
            width={96}
            height={96}
            priority
            className="h-24 w-24 object-cover"
          />
        </div>
        <span className="font-heading text-2xl tracking-wide text-white">ForgeRise</span>
      </div>
    </div>
  );
}
