import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Inline, Pill, StripMark } from '../ui';
import { cn } from '../lib/cn';
import { offerItem, type OfferShown, type PluginGroups, type PluginShown, type PluginState, pluginState, stripOrder } from './catalog';

/**
 * A state's word and its pill's tone (D119 §2). **Running is the quiet neutral** (PLUG10 P9): a hook process is up
 * between calls as a chat is between turns, so it borrows no outcome's hue. **Refused waits on the person**, and wears
 * the waiting hue. Simply on says no word until PLUGUI1f's health gives it one.
 */
export const STATE_WORD: Record<PluginState, { key: string; tone: 'neutral' | 'open' } | null> = {
  running: { key: 'plugin.running', tone: 'neutral' },
  on: null,
  refused: { key: 'plugin.health.refused', tone: 'open' },
  off: { key: 'plugin.off', tone: 'neutral' },
};

/** What a plugin or an offer adds, as fragments: *2 points · 1 agent · 1 server*. */
function useFragments() {
  const { t } = useTranslation();
  return (adds: { points: string[]; harnesses: string[]; servers?: string[] }) => [
    adds.points.length > 0 ? t('plugin.row.points', { count: adds.points.length }) : null,
    adds.harnesses.length > 0 ? t('plugin.row.agents', { count: adds.harnesses.length }) : null,
    adds.servers?.length ? t('plugin.row.servers', { count: adds.servers.length }) : null,
  ].filter(Boolean).join(' · ');
}

/**
 * The Plugins view's list (D119 §3.1): the installed plugins grouped by what they need from the person — *Waiting on
 * you*, *On*, *Off* — then *Daoris's own plugins* not installed, each group with its count and absent with none.
 *
 * @remarks
 * **A molecule**: the catalogue arrives grouped (`pluginGroups`), and every press goes out. A plugin's name, version
 * and description are content, shown as they are. Each row is a row of its list (`data-list-row`), so ↑, ↓, Home and
 * End move along it. An offer's *Install* sits beside its row's door, never inside it.
 */
export function PluginList({ groups, chosen, installing = false, unanswered, onChoose, onInstall }: {
  groups: PluginGroups;
  /** The list's chosen item: a plugin's id, or `offer:<id>`. */
  chosen: string | null;
  /** An offer's install is on its way. */
  installing?: boolean;
  /** The sentence for a list that has never had an answer, said in place rather than left blank (D118 §3h). */
  unanswered?: string | null;
  onChoose: (item: string) => void;
  onInstall: (id: string) => void;
}) {
  const { t } = useTranslation();
  const fragments = useFragments();

  if (unanswered) {
    return <p className="m-0 px-3 py-3 text-small text-ink-soft"><Inline text={unanswered} /></p>;
  }

  const installed = (key: string, rows: PluginShown[]) => rows.length > 0 && (
    <Group key={key} title={t(key, { count: rows.length })}>
      {rows.map((plugin) => {
        const word = STATE_WORD[pluginState(plugin)];
        const adds = plugin.problem ? '' : fragments(plugin) || t('plugin.quiet');
        return (
          <li key={plugin.id} data-list-row="">
            <RowDoor chosen={chosen === plugin.id} onPress={() => onChoose(plugin.id)}>
              <span className="flex min-w-0 items-baseline gap-2">
                <span className={cn('min-w-0 truncate text-body', plugin.enabled ? 'text-ink' : 'text-ink-soft')}>{plugin.name}</span>
                {plugin.version && <span className="shrink-0 font-mono text-meta text-ink-faint">{plugin.version}</span>}
                {word && <span className="ml-auto shrink-0"><Pill tone={word.tone}>{t(word.key)}</Pill></span>}
              </span>
              {adds && <span className="block truncate text-meta text-ink-faint">{adds}</span>}
            </RowDoor>
          </li>
        );
      })}
    </Group>
  );

  return (
    <div>
      {installed('plugin.group.waiting', groups.waiting)}
      {installed('plugin.group.on', groups.on)}
      {installed('plugin.group.off', groups.off)}
      {groups.offers.length > 0 && (
        <Group title={t('plugin.group.offers', { count: groups.offers.length })}>
          {groups.offers.map((offer) => <OfferRow key={offer.id} offer={offer} chosen={chosen} installing={installing} adds={fragments(offer)} onChoose={onChoose} onInstall={onInstall} />)}
        </Group>
      )}
    </div>
  );
}

/** A group's header and its rows, as the session rail draws a repository's (`RepositoryGroup`). */
function Group({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section className="border-t border-line first:border-t-0">
      <h3 className="m-0 truncate px-2.5 pb-1 pt-2.5 text-small font-semibold text-ink">{title}</h3>
      <ul className="m-0 list-none p-0">{children}</ul>
    </section>
  );
}

/** A row's door: the whole row chooses it, wearing the list's selection as the session rail's rows do. */
function RowDoor({ chosen, onPress, children }: { chosen: boolean; onPress: () => void; children: ReactNode }) {
  return (
    <button
      type="button"
      // `aria-current`, as the rail's: the row is a button, not a listbox option.
      aria-current={chosen || undefined}
      onClick={onPress}
      className={cn(
        'block w-full min-w-0 border-l-[3px] px-2.5 py-1.5 text-left transition-colors duration-(--speed)',
        'hover:bg-accent-soft/50',
        chosen ? 'border-l-accent bg-accent-soft' : 'border-l-transparent',
      )}
    >
      {children}
    </button>
  );
}

function OfferRow({ offer, chosen, installing, adds, onChoose, onInstall }: {
  offer: OfferShown; chosen: string | null; installing: boolean; adds: string;
  onChoose: (item: string) => void; onInstall: (id: string) => void;
}) {
  const { t } = useTranslation();
  const item = offerItem(offer.id);
  return (
    <li data-list-row="" className="flex items-center">
      <div className="min-w-0 flex-1">
        <RowDoor chosen={chosen === item} onPress={() => onChoose(item)}>
          <span className="flex min-w-0 items-baseline gap-2">
            <span className="min-w-0 truncate text-body text-ink-soft">{offer.name}</span>
            {offer.version && <span className="shrink-0 font-mono text-meta text-ink-faint">{offer.version}</span>}
          </span>
          {adds && <span className="block truncate text-meta text-ink-faint">{adds}</span>}
        </RowDoor>
      </div>
      {/* Only where it can be installed as it stands; its page gives the driver's sentence otherwise. */}
      {!offer.problem && (
        <Button
          variant="ghost"
          disabled={installing}
          aria-label={t('plugin.offers.installNamed', { id: offer.id })}
          onClick={() => onInstall(offer.id)}
          className="mr-1.5 shrink-0 px-2 py-0.5 text-small"
        >
          {t('plugin.offers.install')}
        </Button>
      )}
    </li>
  );
}

/**
 * The list closed to its strip (D119 §3.1): each installed plugin's initial and its mark, in the list's order. Waiting
 * on you wears the waiting mark, off a faint initial, on no mark. Offers are not on the strip, which holds what this
 * machine has; a strip longer than the window scrolls, since the list pane's strip does.
 */
export function PluginStrip({ groups, chosen, onChoose }: {
  groups: PluginGroups;
  chosen: string | null;
  onChoose: (id: string) => void;
}) {
  const { t } = useTranslation();
  return (
    <ul className="m-0 grid list-none justify-items-center gap-1 px-0 py-1.5">
      {stripOrder(groups).map((plugin) => {
        const state = pluginState(plugin);
        const word = STATE_WORD[state];
        return (
          <StripMark
            key={plugin.id}
            label={word ? `${plugin.name} · ${t(word.key)}` : plugin.name}
            initialOf={plugin.name || plugin.id}
            tone={state === 'refused' ? 'parked' : undefined}
            dimmed={state === 'off'}
            current={chosen === plugin.id}
            onPress={() => onChoose(plugin.id)}
          />
        );
      })}
    </ul>
  );
}
