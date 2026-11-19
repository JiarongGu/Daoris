/**
 * `tokens.css` read as numbers (D330): its four colour blocks as maps, and WCAG 2's luminance and contrast, so the tests that
 * hold the role set compute on the values the window draws. Only tests import it.
 */

export type Theme = 'system-light' | 'system-dark' | 'chosen-light' | 'chosen-dark';

/**
 * Each block's `#rrggbb` declarations: the first `:root` (light), the one inside the dark media query, and the two a chosen
 * theme sets (`:root[data-theme]`). A declaration whose value is no hex colour (a variable, a shadow) is not a colour and is
 * not read.
 */
export function palettes(css: string): Record<Theme, Record<string, string>> {
  const tokens = (body: string) =>
    Object.fromEntries([...body.matchAll(/(--[a-z-]+)\s*:\s*(#[0-9a-fA-F]{3,8})\s*;/g)].map((m) => [m[1]!, m[2]!.toLowerCase()]));
  const block = (pattern: RegExp) => tokens(pattern.exec(css)?.[1] ?? '');
  return {
    'system-light': block(/^:root\s*\{([^}]*)\}/m),
    'system-dark': block(/@media \(prefers-color-scheme: dark\)\s*\{\s*:root\s*\{([^}]*)\}/),
    'chosen-light': block(/:root\[data-theme="light"\]\s*\{([^}]*)\}/),
    'chosen-dark': block(/:root\[data-theme="dark"\]\s*\{([^}]*)\}/),
  };
}

/** Relative luminance of a `#rrggbb` colour (WCAG 2). */
export function luminance(hex: string): number {
  const [r, g, b] = [1, 3, 5].map((at) => {
    const c = parseInt(hex.slice(at, at + 2), 16) / 255;
    return c <= 0.04045 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4;
  }) as [number, number, number];
  return 0.2126 * r + 0.7152 * g + 0.0722 * b;
}

/** The contrast ratio of two `#rrggbb` colours (WCAG 2), whichever is lighter. */
export function contrast(a: string, b: string): number {
  const [light, dark] = [luminance(a), luminance(b)].sort((x, y) => y - x) as [number, number];
  return (light + 0.05) / (dark + 0.05);
}

/** A `#rrggbb` colour's OKLCH hue angle in degrees. */
export function hueAngle(hex: string): number {
  const [r, g, b] = [1, 3, 5].map((at) => {
    const c = parseInt(hex.slice(at, at + 2), 16) / 255;
    return c <= 0.04045 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4;
  }) as [number, number, number];
  const l = Math.cbrt(0.4122214708 * r + 0.5363325363 * g + 0.0514459929 * b);
  const m = Math.cbrt(0.2119034982 * r + 0.6806995451 * g + 0.1073969566 * b);
  const s = Math.cbrt(0.0883024619 * r + 0.2817188376 * g + 0.6299787005 * b);
  const a = 1.9779984951 * l - 2.428592205 * m + 0.4505937099 * s;
  const bAxis = 0.0259040371 * l + 0.7827717662 * m - 0.808675766 * s;
  return ((Math.atan2(bAxis, a) * 180) / Math.PI + 360) % 360;
}

/** The angle between two hues, the short way round. */
export const hueApart = (a: number, b: number): number => Math.abs((((a - b) % 360) + 540) % 360 - 180);
