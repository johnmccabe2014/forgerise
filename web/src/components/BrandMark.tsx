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
 * Shield + "ForgeRise" lockup used in page headers. The crest art is now a
 * transparent PNG so it sits cleanly on either light or dark backgrounds —
 * no chip wrapper needed. The wordmark stays as live text so it scales
 * crisply and adapts to the active theme via text-forge-navy (which the
 * dark-mode override in globals.css flips to a light tint).
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
      <Image
        src="/brand/crest-icon.png"
        alt=""
        width={size}
        height={size}
        priority
        style={{ width: size, height: size }}
        className="object-contain"
      />
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
