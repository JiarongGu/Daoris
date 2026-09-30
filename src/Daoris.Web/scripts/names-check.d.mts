// The types of `names-check.mjs`, for the tests that import its helpers (NAME1a). The script is plain
// JavaScript so the build runs it on any Node the package supports; this declares what it exports, and the
// tests that call every export keep the two in step.

export type Language = 'en' | 'zh';
export type Catalogue = Record<string, string>;
export type KindName =
  'nav' | 'title' | 'tab' | 'section' | 'field' | 'choice' | 'button' | 'status' | 'menu' | 'command'
  | 'headline' | 'placeholder' | 'toast' | 'sentence';

export type Glossary = {
  measure: { latinUnit: number; numericPlaceholders: string[]; numericLength: number; otherLength: number };
  properNouns: string[];
  kinds: Record<string, {
    what: string;
    room: string;
    budget: { en: number; zh: number } | null;
    case: 'sentence' | 'lower' | 'none';
    keys: string[];
  }>;
  doors: { door: string; opens: string }[];
  terms: {
    term: string;
    en?: string;
    zh?: string;
    zhForms?: string[];
    means: string;
    why?: string;
    match?: string;
    pair?: boolean;
    use?: string;
    avoid: { en: string[]; zh: string[] };
  }[];
};

export type Finding = {
  key: string;
  kind: string;
  rule: 'glossary' | 'budget' | 'form' | 'door';
  language: Language;
  message: string;
  value: string;
};

export function load(root?: string): { glossary: Glossary; en: Catalogue; zh: Catalogue };
export function kindOf(glossary: Glossary): (key: string) => string;
export function measure(text: string, language: Language, rules: Glossary['measure']): number;
export function validate(glossary: Glossary, en: Catalogue, zh: Catalogue): string[];
export function check(glossary: Glossary, en: Catalogue, zh: Catalogue, options?: { all?: boolean }): Finding[];
export function report(findings: Finding[], options?: { labels?: number }): string;
