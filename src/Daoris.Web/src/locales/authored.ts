/**
 * Words a person or a plugin writes into configuration (D222): a string as written, or an object of language tags to text with at
 * least one entry. Nothing requires a language; a page shows the text in the viewer's language, else its base language
 * (`zh-TW` → `zh`), else as written (the string, or the first entry). The twin of the driver's `AuthoredText`.
 */
export type Authored = string | Record<string, string>;

const TAG = /^[A-Za-z]{2,8}(-[A-Za-z0-9]{1,8})*$/;

/** The authored text in `value`, or null where it is not one: a blank string, an empty map, a blank text or a key that is no language tag. */
export function readAuthored(value: unknown): Authored | null {
  if (typeof value === 'string') return value.trim() === '' ? null : value;
  if (value === null || typeof value !== 'object' || Array.isArray(value)) return null;
  const entries = Object.entries(value);
  if (entries.length === 0) return null;
  return entries.every(([tag, text]) => TAG.test(tag) && typeof text === 'string' && text.trim() !== '') ? (value as Record<string, string>) : null;
}

/** `value` for the viewer's `language`, else its base language, else as written; empty where there is none. */
export function authoredText(value: Authored | null | undefined, language: string): string {
  if (value === null || value === undefined) return '';
  if (typeof value === 'string') return value;
  const entries = Object.entries(value);
  const find = (tag: string) => entries.find(([each]) => each.toLowerCase() === tag.toLowerCase())?.[1];
  return find(language) ?? find(language.split('-')[0]!) ?? entries[0]?.[1] ?? '';
}

/** Every text written, for a search that reads them all. */
export function authoredTexts(value: Authored | null | undefined): string[] {
  if (value === null || value === undefined) return [];
  return typeof value === 'string' ? [value] : Object.values(value);
}

/** The language entries written, in order; none for a plain string. */
export function authoredEntries(value: Authored | null | undefined): [string, string][] {
  return value !== null && typeof value === 'object' ? Object.entries(value) : [];
}

/**
 * What an author gave, as the shape to write: the one text alone as a plain string, with other languages a map (the one text kept as
 * `en`), or the map alone. Blank texts are left out; null where nothing is left.
 */
export function composeAuthored(text: string, others: readonly (readonly [string, string])[]): Authored | null {
  const kept = others.map(([tag, said]) => [tag.trim(), said.trim()] as const).filter(([tag, said]) => tag !== '' && said !== '');
  const one = text.trim();
  if (kept.length === 0) return one === '' ? null : one;
  const entries = one === '' || kept.some(([tag]) => tag.toLowerCase() === 'en') ? kept : ([['en', one], ...kept] as const);
  return Object.fromEntries(entries);
}
