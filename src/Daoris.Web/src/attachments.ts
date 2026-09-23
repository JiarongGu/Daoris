// What a quest carries beside its words (D65 §2), as the composer handles it BEFORE the service
// judges it. The service is the authority on every limit here — `QuestExchange` refuses whatever these
// miss, in its own sentence — and these exist only so the form does not offer an ask it would refuse,
// and can say why while the person is still choosing files.

/** How many files a quest carries — `QuestExchange.MaxAttachments`. */
export const MAX_FILES = 10;

/** How many bytes a quest's files come to together — `QuestExchange.MaxAttachmentBytes`. */
export const MAX_FILE_BYTES = 20 * 1024 * 1024;

/** A file on its way to the local host: its name, and its bytes as base64 — a byte array in JSON. */
export type Upload = { name: string; content: string };

/**
 * Add what fits, in the order it came, and say what did not. A file past the count or past the total
 * is left out rather than failing the whole drop — the ones that fit are still what was meant.
 */
export function admit(current: File[], incoming: File[]): { files: File[]; refused: 'tooMany' | 'tooLarge' | null } {
  const files = [...current];
  let total = files.reduce((sum, f) => sum + f.size, 0);
  let refused: 'tooMany' | 'tooLarge' | null = null;
  for (const file of incoming) {
    if (files.length >= MAX_FILES) { refused ??= 'tooMany'; continue; }
    if (total + file.size > MAX_FILE_BYTES) { refused ??= 'tooLarge'; continue; }
    files.push(file);
    total += file.size;
  }
  return { files, refused };
}

/** The addresses in what the person typed: one per line or per space, trimmed, each once. */
export function linksOf(text: string): string[] {
  return [...new Set(text.split(/\s+/).map((part) => part.trim()).filter(Boolean))];
}

/** A file read whole, for the JSON body the local host takes. */
export function toUpload(file: File): Promise<Upload> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => {
      // `data:<type>;base64,<content>` — the content is everything after the first comma.
      const url = String(reader.result);
      resolve({ name: file.name, content: url.slice(url.indexOf(',') + 1) });
    };
    reader.onerror = () => reject(reader.error ?? new Error(`could not read ${file.name}`));
    reader.readAsDataURL(file);
  });
}

/**
 * Whether a drawer may show the file as a picture. The kinds the host serves inline — never SVG,
 * which is a document that can carry script, and is offered as a file like any other.
 */
export function isImage(name: string): boolean {
  return /\.(png|jpe?g|gif|webp)$/i.test(name);
}
