import type { Config } from "tailwindcss";

// Brand tokens — master prompt §6
const config: Config = {
  darkMode: "class",
  content: [
    "./src/pages/**/*.{js,ts,jsx,tsx,mdx}",
    "./src/components/**/*.{js,ts,jsx,tsx,mdx}",
    "./src/app/**/*.{js,ts,jsx,tsx,mdx}",
  ],
  theme: {
    extend: {
      colors: {
        // Primary
        "forge-navy": "#102A43",
        // Brand orange — refreshed to match the forge-spark mark (hotter,
        // more saturated than the original copper). Old token name kept so
        // existing class usage continues to work.
        "rise-copper": "#F26A1F",
        // Supporting
        "soft-ember": "#F4A364",
        // New: cool sky-blue accent pulled from the brand sheet's
        // stadium-light highlights. Use sparingly for links, focus rings
        // and secondary callouts so it never competes with the orange.
        "forge-spark": "#4DA8DA",
        "mist-grey": "#F4F7FA",
        slate: "#486581",
        "deep-charcoal": "#1F2933",
        // Readiness (coach-safe categories — master prompt §9)
        readiness: {
          ready: "#2F855A",
          monitor: "#D69E2E",
          modify: "#DD6B20",
          recovery: "#C53030",
        },
        background: "var(--background)",
        foreground: "var(--foreground)",
      },
      fontFamily: {
        heading: ["'Inter Tight'", "Sora", "system-ui", "sans-serif"],
        body: ["Inter", "system-ui", "sans-serif"],
      },
      borderRadius: {
        card: "1rem",
      },
      boxShadow: {
        soft: "0 4px 20px -8px rgba(16, 42, 67, 0.15)",
      },
    },
  },
  plugins: [],
};
export default config;
