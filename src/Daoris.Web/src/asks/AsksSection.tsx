import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { type Ask, canBeAsked } from '../api';
import { linksOf, toUpload } from '../attachments';
import { NO_CARRY } from '../compose/carry';
import { sentence } from '../format';
import { useAsk, useAsks, useCloseAsk, usePublishAsk, useQuests, useRegistry, useSessions } from '../queries';
import { useScope } from '../scope';
import { type Notify, SectionTitle, useErrorNotify } from '../ui';
import { AskCard } from './AskCard';
import { AskComposer, type AskDraft } from './AskComposer';
import { AskRecord } from './AskRecord';

const EMPTY_DRAFT: AskDraft = { circle: '', sentence: '', to: '', ...NO_CARRY };

/**
 * Asks, at the head of the Quests view (INT4c): the screen twin of `daoris-driver ask` (D50) — the
 * composer that makes one, the list, and each one's record with its two verbs.
 *
 * @remarks
 * **An ask is made in ONE circle**: the one the page is scoped to, or the only one the machine holds.
 * Scoped to none of several, the composer asks which — "every circle" names nobody, and assuming
 * `default` would ask somewhere the person did not choose.
 *
 * Every answer is the service's sentence, verbatim (frontend §4a). A refused publish still leaves the
 * ask kept with its proposal (INT4a), which is why the list is asked again whatever the door said.
 *
 * The organism: it holds the hooks so the three molecules below it hold none (components §2).
 */
export function AsksSection({
  notify, includeClosed, composing, onComposingChange, onOpenQuest, focus, onFocused, onAttend,
}: {
  notify: Notify;
  includeClosed: boolean;
  /** Whether the composer is open — the view's header and the palette open it; this closes it. */
  composing: boolean;
  onComposingChange: (composing: boolean) => void;
  /** A quest the ask became, to open in the view's own drawer. */
  onOpenQuest: (id: string) => void;
  /** An ask a door named (INT4d) — an event, consumed by identity and cleared by its holder. */
  focus?: string | null;
  onFocused?: () => void;
  /** The door into Sessions for an ask's intake session — absent where Sessions is (a browser). */
  onAttend?: (session: string) => void;
}) {
  const { t } = useTranslation();
  const scope = useScope();
  const asks = useAsks(includeClosed);
  // Scoped like every cross-repository read: scoped, its rows are that circle's and the circle is fixed;
  // unscoped, they are every circle's, which is exactly the list the composer asks the person to pick from.
  const family = useRegistry();
  const everything = useQuests(null, true);
  const sessions = useSessions(null, true);
  const ask = useAsk();
  const publish = usePublishAsk();
  const close = useCloseAsk();
  useErrorNotify(asks.error, notify);

  const [draft, setDraft] = useState<AskDraft>(EMPTY_DRAFT);
  const [reading, setReading] = useState(false);
  // The record on screen: the card that opened it, or the door's last answer about it — replaced by the
  // list's copy once that is newer, so a move made elsewhere shows, and a list that has not caught up
  // with the door yet does not undo what the person just did.
  const [held, setHeld] = useState<Ask | null>(null);
  const listed = held ? asks.data?.find((candidate) => candidate.id === held.id) : undefined;
  const shown = listed && held && Date.parse(listed.updated) > Date.parse(held.updated) ? listed : held;
  // Its intake session's record, ended ones included — the record says who answered (INT4d). A
  // session record is the host's HTTP, so a browser on this machine reads it too.
  const intake = shown?.intake
    ? sessions.data?.find((session) => session.id === shown.intake) ?? null
    : null;

  // An ask a door named (INT4d), consumed by identity for the quest composer's reason (frontend §4b):
  // the last one seen is remembered, a new one opens its record once the ask is loaded, and the holder
  // is told from an effect. Forgotten once the holder clears it, so the same ask asked twice opens twice.
  const [focusSeen, setFocusSeen] = useState<string | null>(null);
  const focused = focus ? asks.data?.find((candidate) => candidate.id === focus) : undefined;
  if (focus && focused && focus !== focusSeen) {
    setFocusSeen(focus);
    setHeld(focused);
  }
  if (!focus && focusSeen) setFocusSeen(null);
  useEffect(() => { if (focus && focusSeen === focus) onFocused?.(); }, [focus, focusSeen, onFocused]);

  const circles = [...new Set((family.data ?? []).map((row) => row.workspace ?? 'default'))].sort();
  const fixed = scope.workspace ?? (circles.length === 1 ? circles[0] : null);
  const receiversIn = (circle: string) => (family.data ?? [])
    .filter((row) => canBeAsked(row) && (row.workspace ?? 'default') === circle)
    .map((row) => row.repository)
    .sort();
  const questTitles = Object.fromEntries((everything.data ?? []).map((quest) => [quest.id, quest.title]));
  const busy = ask.isPending || publish.isPending || close.isPending || reading;

  const onAsk = async () => {
    // Read whole only now — a file chosen and then removed was never read at all.
    setReading(true);
    let attachments;
    try {
      attachments = await Promise.all(draft.files.map(toUpload));
    } catch (e) {
      notify(sentence(e), 'error');
      return;
    } finally {
      setReading(false);
    }

    const workspace = fixed ?? draft.circle;
    ask.mutate({
      workspace,
      sentence: draft.sentence.trim(),
      links: linksOf(draft.links),
      attachments,
      // Unnamed, the declarations propose and nothing is published; named, it is published at once.
      ...(draft.to ? { to: draft.to } : {}),
    }, {
      onSuccess: (result) => {
        notify(result.message);
        setDraft(EMPTY_DRAFT);
        onComposingChange(false);
        setHeld(result.ask);
      },
      onError: (e) => notify(sentence(e), 'error'),
    });
  };

  const onPublish = (id: string, to: string) => publish.mutate({ id, to }, {
    onSuccess: (result) => { notify(result.message); setHeld(result.ask); },
    onError: (e) => notify(sentence(e), 'error'),
  });

  const onClose = (id: string, reason: string) => close.mutate({ id, reason }, {
    onSuccess: (result) => { notify(result.message); setHeld(result.ask); },
    onError: (e) => notify(sentence(e), 'error'),
  });

  return (
    <>
      {(asks.data?.length ?? 0) > 0 && (
        <div>
          <SectionTitle>{t('asks.group', { count: asks.data!.length })}</SectionTitle>
          {asks.data!.map((item) => <AskCard key={item.id} ask={item} onOpen={setHeld} />)}
        </div>
      )}

      {composing && (
        <AskComposer
          draft={draft}
          onChange={setDraft}
          fixed={fixed}
          circles={circles}
          receivers={receiversIn(fixed ?? draft.circle)}
          busy={busy}
          onSubmit={() => void onAsk()}
          onCancel={() => onComposingChange(false)}
        />
      )}

      {shown && !composing && (
        <AskRecord
          ask={shown}
          receivers={receiversIn(shown.workspace)}
          questTitles={questTitles}
          intake={intake}
          onAttend={onAttend}
          busy={busy}
          onPublish={(to) => onPublish(shown.id, to)}
          onClose={(reason) => onClose(shown.id, reason)}
          onOpenQuest={(id) => { setHeld(null); onOpenQuest(id); }}
          onDismiss={() => setHeld(null)}
        />
      )}
    </>
  );
}
