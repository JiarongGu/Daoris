import { useTranslation } from 'react-i18next';
import { Button, Inline, PathText } from '../ui';

/** Where an installed plugin came from (PLUG9 c, D102): a folder, the install's offer, no record, or one that does not read. */
export type PluginSourceShown = {
  kind: 'folder' | 'offer' | 'none' | 'unread';
  folder?: string | null;
  offer?: string | null;
  problem?: string | null;
};

/** One thing an update changes, each side as the manifests write it; empty is none. */
export type PluginChangeShown = { what: 'version' | 'command' | 'points' | 'harnesses' | 'servers'; was: string; now: string };

/** What `PLUGIN_UPDATE` answers: without `apply`, what an update would change or why it cannot; with it, what it did. */
export type PluginUpdatePlanShown = {
  id: string;
  applied: boolean;
  refusal?: string | null;
  source?: string | null;
  from?: string | null;
  changes: PluginChangeShown[];
};

/** Whether a plugin's source is one an update can read. */
export const updatable = (source?: PluginSourceShown | null): boolean => source?.kind === 'folder' || source?.kind === 'offer';

/**
 * Where an installed plugin came from, under its row (PLUG9 c): the folder it was added from, the install's
 * offer, or no record — said, never guessed — or the driver's sentence for a record that does not read.
 */
export function PluginSourceLine({ source }: { source?: PluginSourceShown | null }) {
  const { t } = useTranslation();
  if (!source) return null;

  switch (source.kind) {
    case 'folder':
      return (
        <span>{t('plugin.source.folder')} <PathText path={source.folder ?? ''} className="text-meta" /></span>
      );
    case 'offer':
      return <span>{t('plugin.source.offer')}</span>;
    case 'unread':
      return <span><Inline text={t('plugin.source.unread', { problem: source.problem ?? '' })} /></span>;
    default:
      return <span><Inline text={t('plugin.source.none')} /></span>;
  }
}

/**
 * What an update would change, under the plugin's row, before the press (PLUG9 c): each change as the two
 * manifests write it, or that nothing it declares changes, and **Update now** — or the driver's refusal,
 * verbatim, and nothing to press but Not now.
 */
export function PluginUpdatePlan({ plan, busy = false, onApply, onCancel }: {
  plan: PluginUpdatePlanShown;
  busy?: boolean;
  onApply: (id: string) => void;
  onCancel: () => void;
}) {
  const { t } = useTranslation();
  const side = (value: string) => (value ? <code className="font-mono text-meta">{value}</code> : <span>{t('plugin.update.none')}</span>);

  return (
    <section aria-label={t('plugin.update.title')} className="rounded-control border border-line bg-page/60 px-3.5 py-2.5">
      {plan.refusal ? (
        <p className="m-0 max-w-prose border-l-[3px] border-warn pl-3 text-body text-ink-soft"><Inline text={plan.refusal} /></p>
      ) : plan.changes.length === 0 ? (
        <p className="m-0 text-small text-ink-soft">{t('plugin.update.same')}</p>
      ) : (
        <ul className="m-0 flex list-none flex-col gap-1 p-0">
          {plan.changes.map((change) => (
            <li key={change.what} className="grid grid-cols-[6.5rem_minmax(0,1fr)] items-baseline gap-2 text-small">
              <span className="text-ink-faint">{t(`plugin.update.what.${change.what}`, { defaultValue: change.what })}</span>
              <span className="min-w-0 break-words text-ink-soft">{side(change.was)} → {side(change.now)}</span>
            </li>
          ))}
        </ul>
      )}
      {!plan.refusal && <p className="m-0 mt-2 text-meta text-ink-faint">{t('plugin.update.note')}</p>}
      <div className="mt-2 flex flex-wrap gap-2">
        {!plan.refusal && (
          <Button disabled={busy} aria-busy={busy || undefined} onClick={() => onApply(plan.id)}>{t('plugin.update.apply')}</Button>
        )}
        <Button variant="ghost" disabled={busy} onClick={onCancel}>{t('plugin.update.cancel')}</Button>
      </div>
    </section>
  );
}
