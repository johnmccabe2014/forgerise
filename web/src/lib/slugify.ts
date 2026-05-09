/**
 * URL-safe slug for team codes.
 * - Lowercases.
 * - Strips diacritics via NFKD.
 * - Replaces non-alphanumeric runs with a single dash.
 * - Trims leading/trailing dashes.
 * - Caps to 40 chars (matches API `[StringLength(40)]`).
 */
export function slugify(input: string): string {
  if (!input) return "";
  const normalised = input.normalize("NFKD").replace(/[\u0300-\u036f]/g, "");
  const slug = normalised
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "");
  return slug.slice(0, 40);
}
