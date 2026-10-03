import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Inline, type MenuAct, Pill, StripMark } from '../ui';
import { cn } from '../lib/cn';
import { type ContextOffer, contextOffer } from '../menus/press';
import {
  offerItem, type OfferShown, type PluginGroups, type PluginState, pluginState, sourceWord, stripOrder, updateWaits,
} from './catalog';
import { PluginIcon } from './PluginIcon';

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
 * The Plugins view's list as a **catalogue** (D140 §2, amending D119 §3.1): *Installed*, waiting on you first, then on,
 * then off; then *Daoris's own plugins* not installed; each section with its count and absent with none.
 *
 * @remarks
 * **A molecule**: the catalogue arrives grouped (`pluginGroups`), and every press goes out. A plugin's name, version
 * and description are content, shown as they are. Each row is a row of its list (`data-list-row`), so ↑, ↓, Home and
 * End move along it. An offer's *Install* sits beside its row's door, never inside it.
 *
 * **A row** is its icon, its name and version with its state's word at the right; then what it gives, its author's
 * description, cut to the row (what it adds as fragments where it has none); then a meta line: where it came from, what
 * it adds, and *update available* where its source declares something different. Never a path: the page says where.
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

  return (
    <div>
      {groups.installed.length > 0 && (
        <Group title={t('plugin.group.installed', { count: groups.installed.length })}>
          {groups.installed.map((plugin) => {
            const word = STATE_WORD[pluginState(plugin)];
            // What it adds is what a refused plugin is not taken for, so it says none (D64 §5).
            const adds = plugin.problem ? '' : fragments(plugin) || t('plugin.quiet');
            const source = sourceWord(plugin.source);
            return (
              <li key={plugin.id} data-list-row="" {...contextOffer(rowMenu(t, plugin.name || plugin.id, plugin.id, () => onChoose(plugin.id)))}>
                <RowDoor chosen={chosen === plugin.id} onPress={() => onChoose(plugin.id)}>
                  <CatalogueRow
                    icon={<PluginIcon id={plugin.id} name={plugin.name} icon={plugin.icon} dimmed={!plugin.enabled} />}
                    name={plugin.name}
                    version={plugin.version}
                    quiet={!plugin.enabled}
                    word={word && <Pill tone={word.tone}>{t(word.key)}</Pill>}
                    gives={plugin.description || adds}
                    meta={[source && t(source), plugin.description ? adds : ''].filter(Boolean).join(' · ')}
                    update={updateWaits(plugin) && <Pill tone="neutral">{t('plugin.update.waits')}</Pill>}
                  />
                </RowDoor>
              </li>
            );
          })}
        </Group>
      )}
      {groups.offers.length > 0 && (
        <Group title={t('plugin.group.offers', { count: groups.offers.length })}>
          {groups.offers.map((offer) => <OfferRow key={offer.id} offer={offer} chosen={chosen} installing={installing} adds={fragments(offer)} onChoose={onChoose} onInstall={onInstall} />)}
        </Group>
      )}
    </div>
  );
}

/**
 * A catalogue row's face (D140 §2): the icon at its left; its name, version and state's word; what it gives, cut to the
 * row; and its meta line. Content (the name, the version, what it gives) is shown as declared.
 */
function CatalogueRow({ icon, name, version, quiet = false, word, gives, meta, update }: {
  icon: ReactNode;
  name: string;
  version: string;
  /** Its name drawn soft: a plugin that is off, or an offer not installed. */
  quiet?: boolean;
  word?: ReactNode;
  /** What it gives: its description, or what it adds where it has none. */
  gives: string;
  /** Where it came from and what it adds, joined; empty where it has nothing to say. */
  meta: string;
  update?: ReactNode;
}) {
  return (
    <span className="flex min-w-0 items-start gap-2.5">
      <span className="mt-0.5 shrink-0">{icon}</span>
      <span className="min-w-0 flex-1">
        <span className="flex min-w-0 items-baseline gap-2">
          <span className={cn('min-w-0 truncate text-body font-medium', quiet ? 'text-ink-soft' : 'text-ink')}>{name}</span>
          {version && <span className="shrink-0 font-mono text-meta text-ink-faint">{version}</span>}
          {word && <span className="ml-auto shrink-0">{word}</span>}
        </span>
        {gives && <span className="block truncate text-small text-ink-soft">{gives}</span>}
        {(meta || update) && (
          <span className="flex min-w-0 items-center gap-1.5 text-meta text-ink-faint">
            {meta && <span className="min-w-0 truncate">{meta}</span>}
            {update && <span className="shrink-0">{update}</span>}
          </span>
        )}
      </span>
    </span>
  );
}

/**
 * What a plugin's row offers a right-click (CTX1, D138 §4): opening it, its row's own act (an offer's *Install*), and its
 * id. What is done to a plugin is its page's.
 */
function rowMenu(t: (key: string) => string, name: string, id: string, open: () => void, own?: MenuAct): ContextOffer {
  return {
    label: name,
    acts: [
      { id: 'open', label: t('contextMenu.act.open'), onSelect: open },
      ...(own ? [own] : []),
      { id: 'copy', label: t('contextMenu.act.copyPlugin'), icon: 'copy', copy: id },
    ],
  };
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
  // Its Install, where its row offers one, is its right-click's too (CTX1).
  const install = offer.problem
    ? undefined
    : { id: 'install', label: t('plugin.offers.install'), icon: 'plus' as const, disabled: installing, onSelect: () => onInstall(offer.id) };
  return (
    <li data-list-row="" className="flex items-center" {...contextOffer(rowMenu(t, offer.name || offer.id, offer.id, () => onChoose(item), install))}>
      <div className="min-w-0 flex-1">
        <RowDoor chosen={chosen === item} onPress={() => onChoose(item)}>
          {/* No state and no source: its section says both (D140 §2). */}
          <CatalogueRow
            icon={<PluginIcon id={offer.id} name={offer.name} icon={offer.icon} />}
            name={offer.name}
            version={offer.version}
            quiet
            gives={offer.description || adds}
            meta={offer.description ? adds : ''}
          />
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
 * The list closed to its strip (D119 §3.1): each installed plugin's icon and its mark, in the list's order (D140 §2: its
 * icon in its initial's place). Waiting on you wears the waiting mark, off a faint icon, on no mark. Offers are not on
 * the strip, which holds what this machine has; a strip longer than the window scrolls, since the list pane's strip does.
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
            face={<PluginIcon id={plugin.id} name={plugin.name} icon={plugin.icon} size="strip" dimmed={state === 'off'} />}
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
