import { type ClipboardEvent, type DragEvent, useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { admit, MAX_FILE_BYTES, MAX_FILES } from '../attachments';
import { size } from '../format';
import { cn } from '../lib/cn';
import { Button, Icon } from '../ui';

/**
 * What a composer carries beside its words (D65 §2): the links as typed — read into addresses when
 * it is sent — and the browser's own file handles, read whole only then.
 */
export type Carry = { links: string; files: File[] };
export const NO_CARRY: Carry = { links: '', files: [] };

/** Why a drop left something off: past the count, or past the total. */
export type LeftOff = 'tooMany' | 'tooLarge' | null;

/**
 * The browser's file dialog, hidden, and the way to open it — each composer's own button opens it.
 * Cleared after every choice, so choosing a file again after removing it is still a change. Both
 * composers wrote this input out (REV3 CLEAN1).
 */
export function useFileChooser(onChoose: (files: File[]) => void, label: string) {
  const ref = useRef<HTMLInputElement>(null);
  const input = (
    <input
      ref={ref} type="file" multiple className="sr-only" tabIndex={-1} aria-label={label}
      onChange={(event) => {
        onChoose(Array.from(event.target.files ?? []));
        event.target.value = '';
      }}
    />
  );
  return { open: () => ref.current?.click(), input };
}

/**
 * What a record carries, counted rather than listed (D65 §2): a card is a summary, and its record
 * holds the links and the files themselves. Nothing when it carries neither. The quest and ask
 * cards each wrote this (REV3 CLEAN1).
 */
export function CarriedCount({ links, files }: { links: number; files: number }) {
  const { t } = useTranslation();
  if (links === 0 && files === 0) return null;
  const label = [
    links > 0 && t('quests.card.links', { count: links }),
    files > 0 && t('quests.card.files', { count: files }),
  ].filter(Boolean).join(' · ');
  return (
    <span aria-label={label} title={label} className="flex shrink-0 items-center gap-1 font-mono text-meta text-ink-faint">
      {links > 0 && <><Icon name="link" size={12} />{links}</>}
      {files > 0 && <><Icon name="attach" size={12} />{files}</>}
    </span>
  );
}

/** Whether a drag carries files — a dragged selection of text is the words' business, not ours. */
export const carriesFiles = (event: { dataTransfer: DataTransfer | null }) =>
  Array.from(event.dataTransfer?.types ?? []).includes('Files');

/**
 * The drop, paste and chooser half of a composer — shared by the quest composer and the ask composer
 * (INT4c), so the two cannot drift on how a file arrives.
 *
 * @remarks
 * `handlers` go on the FORM: the whole composer takes a drop and a paste, because aiming at a box
 * inside a drawer is a chore, and the box lights up to say where the file went.
 *
 * 🔴 **While `active`, a stray drop is absorbed.** A browser — and the desktop's webview — answers an
 * unhandled file drop by NAVIGATING to the file, which in the desktop replaces the whole application
 * with a picture. The form's own drop handler is what attaches.
 *
 * A state hook, not a data hook: it reaches neither the service nor the shell (components §2).
 */
export function useCarry(carry: Carry, onChange: (next: Carry) => void, active: boolean) {
  const [leftOff, setLeftOff] = useState<LeftOff>(null);
  const [dragging, setDragging] = useState(false);

  useEffect(() => {
    if (!active) return undefined;
    const absorb = (event: globalThis.DragEvent) => { if (carriesFiles(event)) event.preventDefault(); };
    window.addEventListener('dragover', absorb);
    window.addEventListener('drop', absorb);
    return () => {
      window.removeEventListener('dragover', absorb);
      window.removeEventListener('drop', absorb);
    };
  }, [active]);

  const attach = (incoming: File[]) => {
    if (incoming.length === 0) return;
    const { files, refused } = admit(carry.files, incoming);
    onChange({ ...carry, files });
    setLeftOff(refused);
  };

  return {
    leftOff,
    dragging,
    attach,
    remove: (index: number) => {
      onChange({ ...carry, files: carry.files.filter((_, at) => at !== index) });
      setLeftOff(null);
    },
    /** Forget what the last drop left off — a composer starting afresh says nothing about the last one. */
    forget: () => setLeftOff(null),
    handlers: {
      onDragOver: (event: DragEvent) => {
        if (carriesFiles(event)) { event.preventDefault(); setDragging(true); }
      },
      onDragLeave: (event: DragEvent) => { if (event.currentTarget === event.target) setDragging(false); },
      onDrop: (event: DragEvent) => {
        if (!carriesFiles(event)) return;
        event.preventDefault();
        setDragging(false);
        attach(Array.from(event.dataTransfer.files));
      },
      // A screenshot on the clipboard is a file, not text: attached, and kept out of the field it was
      // pasted into. A paste of text goes where it was pasted, as ever.
      onPaste: (event: ClipboardEvent) => {
        const pasted = Array.from(event.clipboardData?.files ?? []);
        if (pasted.length === 0) return;
        event.preventDefault();
        attach(pasted);
      },
    },
  };
}

/**
 * The links field and the files box — what a composer carries, as fields. Props only: the carry is
 * the composer's, and so is what the drop left off (components §2).
 *
 * The limits here are the service's (`QuestExchange`), mirrored in `attachments.ts` only so the form
 * does not offer what the door would refuse; the door still judges, in its own sentence.
 */
export function CarryFields({ carry, filesLabel, leftOff, dragging, busy = false, onLinks, onAttach, onRemove }: {
  carry: Carry;
  /** What happens to the files, which differs by what carries them — said by the composer. */
  filesLabel: string;
  leftOff: LeftOff;
  dragging: boolean;
  busy?: boolean;
  onLinks: (links: string) => void;
  onAttach: (files: File[]) => void;
  onRemove: (index: number) => void;
}) {
  const { t } = useTranslation();
  const chooser = useFileChooser(onAttach, t('carry.choose'));

  return (
    <>
      <label className="grid gap-1 text-small text-ink-soft">
        {t('carry.linksLabel')}
        <textarea
          rows={2} value={carry.links} spellCheck={false}
          placeholder="https://…"
          onChange={(e) => onLinks(e.target.value)}
          className="resize-y rounded-control border border-line-strong bg-raised px-2.5 py-1.5 font-mono text-small text-ink"
        />
      </label>
      <div className="grid gap-1.5 text-small text-ink-soft">
        {filesLabel}
        <div
          className={cn(
            'flex flex-wrap items-center gap-2.5 rounded-control border border-dashed px-3 py-2.5 transition-colors duration-(--speed)',
            dragging ? 'border-accent bg-accent-soft' : 'border-line-strong',
          )}
        >
          <Icon name="attach" size={14} className="text-ink-faint" />
          <span className="flex-1 text-body text-ink-soft">{t('carry.drop')}</span>
          <Button type="button" disabled={busy} onClick={chooser.open}>
            {t('carry.choose')}
          </Button>
          {chooser.input}
        </div>
        {leftOff && (
          <p role="status" className="m-0 text-body text-ink-danger">
            {t(`carry.${leftOff}`, { max: MAX_FILES, size: size(MAX_FILE_BYTES) })}
          </p>
        )}
        {carry.files.length > 0 && (
          <ul className="m-0 grid list-none gap-1 p-0">
            {carry.files.map((file, index) => (
              <li key={`${index}:${file.name}`} className="flex min-w-0 items-center gap-2 text-body text-ink">
                <Icon name="attach" size={12} className="text-ink-faint" />
                <span className="min-w-0 flex-1 truncate">{file.name}</span>
                <span className="shrink-0 font-mono text-meta text-ink-faint">{size(file.size)}</span>
                <Button
                  type="button" variant="ghost" disabled={busy}
                  aria-label={t('carry.remove', { name: file.name })}
                  onClick={() => onRemove(index)}
                >
                  <Icon name="x" size={12} />
                </Button>
              </li>
            ))}
          </ul>
        )}
      </div>
    </>
  );
}
