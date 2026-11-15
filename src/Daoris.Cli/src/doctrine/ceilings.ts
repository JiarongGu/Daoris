// The declared documents' ceilings, measured: what `check` reports, and fails on where a declaration says so.

import type { DeclaredDocument, DriftReport } from './records.ts';
import { wordCount } from '../layout/documents.ts';

type Over = DriftReport['documentsOver'][number];

/** One text a role's ceiling measures, with the label `check` prints for it. */
export interface Measured {
  label: string;
  text: string;
}

/**
 * Every declared document over a ceiling it declares, in words or in bytes, each marked failing where its declaration
 * says `fails`.
 *
 * @param texts The texts a role's ceiling measures: the root instruction file for the brief, each room's instructions,
 * a file, or each document of a folder.
 */
export function overCeilings(
  documents: readonly DeclaredDocument[],
  texts: (document: DeclaredDocument) => readonly Measured[],
): Over[] {
  const over: Over[] = [];
  for (const document of documents) {
    if (document.words === null && document.bytes === null) continue;
    for (const { label, text } of texts(document)) {
      const words = wordCount(text);
      if (document.words !== null && words > document.words) {
        over.push({ label, measured: words, ceiling: document.words, unit: 'words', fails: document.fails });
      }
      const bytes = Buffer.byteLength(text, 'utf8');
      if (document.bytes !== null && bytes > document.bytes) {
        over.push({ label, measured: bytes, ceiling: document.bytes, unit: 'bytes', fails: document.fails });
      }
    }
  }
  return over;
}
