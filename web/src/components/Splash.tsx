"use client";

import Image from "next/image";
import { useEffect, useState } from "react";

const HOLD_MS = 1200;
const FADE_MS = 400;
const STORAGE_KEY = "forgerise.splashShown";

type Phase = "hidden" | "visible" | "fading";

export default function Splash() {
  // Always start hidden so SSR markup matches the first client render and
  // we never get a stuck server-rendered overlay if the client effect bails.
  const [phase, setPhase] = useState<Phase>("hidden");

  useEffect(() => {
    let alreadyShown = false;
    try {
      alreadyShown = window.sessionStorage.getItem(STORAGE_KEY) === "1";
      if (!alreadyShown) {
        window.sessionStorage.setItem(STORAGE_KEY, "1");
      }
    } catch {
      // sessionStorage may be unavailable; treat as first visit.
    }
    if (alreadyShown) return;

    const showTimer = window.setTimeout(() => setPhase("visible"), 0);
    const fadeTimer = window.setTimeout(() => setPhase("fading"), HOLD_MS);
    const goneTimer = window.setTimeout(() => setPhase("hidden"), HOLD_MS + FADE_MS);
    return () => {
      window.clearTimeout(showTimer);
      window.clearTimeout(fadeTimer);
      window.clearTimeout(goneTimer);
    };
  }, []);

  if (phase === "hidden") return null;

  return (
    <div
      data-testid="splash"
      aria-hidden="true"
      onClick={() => setPhase("hidden")}
      style={{
        position: "fixed",
        inset: 0,
        zIndex: 9999,
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        backgroundColor: "#1F2933",
        opacity: phase === "fading" ? 0 : 1,
        transition: `opacity ${FADE_MS}ms ease-out`,
        pointerEvents: phase === "fading" ? "none" : "auto",
      }}
    >
      <div className="flex flex-col items-center gap-4">
        <Image
          src="/brand/crest-icon.png"
          alt="ForgeRise"
          width={120}
          height={120}
          priority
          className="h-28 w-28 object-contain"
        />
        <span className="font-heading text-2xl tracking-wide text-white">ForgeRise</span>
      </div>
    </div>
  );
}
