import Image from "next/image";
import Link from "next/link";

interface BrandMarkProps {
  /** Where the brand mark links to. Defaults to /dashboard. Pass null for non-link contexts. */
  href?: string | null;
  /** Render the wordmark beside the shield. Defaults to true. */
  showWordmark?: boolean;
  /** Pixel size of the shield icon. Defaults to 32 (header use). */
  size?: number;
  className?: string;
}

/**
 * Shield + "ForgeRise" lockup used in page headers. The shield art has a baked-in
 * dark grungy backdrop, so we present it inside a deep-charcoal rounded chip — that
 * way the imagery sits comfortably against the otherwise white/mist-grey nav bar
 * without us having to alpha-mask the source PNG. The wordmark stays as live text
 * (font-heading, forge-navy) so it scales crisply and stays accessible.
 */
export function BrandMark({
  href = "/dashboard",
  showWordmark = true,
  size = 32,
  className = "",
}: BrandMarkProps) {
  const inner = (
    <span
      data-testid="brand-mark"
      className={`inline-flex items-center gap-2 ${className}`}
    >
      <span
        className="inline-flex items-center justify-center rounded-md bg-deep-charcoal shadow-soft overflow-hidden"
        style={{ width: size, height: size }}
        aria-hidden="true"
      >
        <Image
          src="/brand/crest-icon.png"
          alt=""
          width={size}
          height={size}
          priority
          className="h-full w-full object-cover"
        />
      </span>
      {showWordmark && (
        <span className="font-heading text-forge-navy tracking-wide">
          ForgeRise
        </span>
      )}
    </span>
  );

  if (href === null) return inner;
  return (
    <Link href={href} className="inline-flex items-center" aria-label="ForgeRise home">
      {inner}
    </Link>
  );
}
