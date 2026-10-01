import { useTranslation } from 'react-i18next';
import type { Entry } from '../api';
import { Inline, Pill, Prose } from '../ui';
import { type MainNotice, PageHead, ViewMain } from '../work/ViewMain';

/**
 * An entry's text, **as it is written** (UX5 U43): the source, monospaced and unrendered. Doctrine is read as it is
 * written, and a Markdown renderer would be a second opinion about what the file says.
 *
 * @remarks
 * It takes the width it is given and no measure of its own: a source is wrapped near a hundred characters, and the
 * 32rem drawer it was read in broke every line once more, a zigzag of long and short lines. A wide drawer gave it room;
 * since FRAME1f it is the main area's, beside the list and Ask Daoris rather than under a scrim (D118 §3d).
 */
export function EntryText({ body }: { body: string }) {
  return <pre className="m-0 whitespace-pre-wrap break-words font-mono text-small leading-[1.65]">{body}</pre>;
}

/** An entry's kind and where it came from, as pills: the words it is filed under, never its own. */
export function EntryPills({ kind, provenance }: { kind: string; provenance?: string }) {
  const { t } = useTranslation();
  return (
    <>
      <Pill>{t(`kind.${kind}`)}</Pill>
      {provenance && <Pill>{t(`provenance.${provenance}`)}</Pill>}
    </>
  );
}

/** Where an entry lives: its repository and its path in it, which is the entry's id said for a person. */
export const entryPlace = (entry: Pick<Entry, 'repository' | 'path'>) => `${entry.repository} · ${entry.path}`;

/**
 * **An entry's page** (FRAME1f, D118 §2): Search's main area, the entry a result names read whole. Its header is its
 * title, its kind and provenance, and where it lives; then its text as it is written. It was the reader, a wide drawer
 * over the side bar and the panel (audit SR4).
 *
 * @remarks
 * **A molecule**: the entry arrives read, and the page holds nothing. It adds no acts: doctrine is changed where its
 * repository keeps it (D31), never from here.
 */
export function EntryPage({ entry }: { entry: Entry }) {
  return (
    <ViewMain header={<PageHead title={entry.title} pills={<EntryPills kind={entry.kind} provenance={entry.provenance} />} id={entryPlace(entry)} />}>
      <EntryText body={entry.body} />
    </ViewMain>
  );
}

/**
 * Search's main area with no entry to read (D118 §3b): **nothing chosen** says how to choose; **gone** says the entry a
 * result named is no longer in the index; **loading** is skeleton rows, never the empty state; and a read that failed
 * says its sentence in place.
 */
export function EntryMainNotice({ state, sentence }: {
  state: 'none' | 'gone' | 'loading' | 'unanswered';
  /** The sentence of a read that failed. */
  sentence?: string;
}) {
  const { t } = useTranslation();
  if (state === 'unanswered') return <ViewMain><Prose><Inline text={sentence ?? ''} /></Prose></ViewMain>;
  const none: MainNotice = { icon: 'search', headline: t('search.none.headline'), body: t('search.none.body') };
  const gone: MainNotice = { icon: 'search', headline: t('search.gone.headline'), body: t('search.gone.body') };
  return <ViewMain state={state} none={none} gone={gone} />;
}
