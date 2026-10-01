import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { cn } from '../lib/cn';
import { store, stored } from '../lib/stored';
import { Menu, Tip } from '../ui';
import { ASKS_DASH, CHAIN_DASH, DEPENDS_DASH } from './MapCanvas';
import { LINE_KINDS, type LineKind, type Topology, type When, WHENS } from './topology';

/**
 * Where a viewer's choice of lines is kept: a convenience of theirs, so the browser's own storage,
 * through the page's one guard (`lib/stored.ts`; FRAME1c, audit A10), where it kept a guard of its own.
 */
const KEY = 'daoris.mapLines';
const WHEN_KEY = 'daoris.mapWhen';

/** The kinds a viewer hid, as kept; anything unreadable is nothing hidden. */
function keptHidden(): LineKind[] {
  try {
    const kept = JSON.parse(stored(KEY) ?? '[]') as unknown;
    return Array.isArray(kept) ? kept.filter((kind): kind is LineKind => (LINE_KINDS as readonly string[]).includes(kind)) : [];
  } catch {
    return [];
  }
}

/**
 * Which kinds of line the map draws, as this viewer last chose (MAP4b). Every kind until they hide
 * one. Storage can be absent or refuse (a private window), and then the choice lasts the visit.
 */
export function useShownLines(): [ReadonlySet<LineKind>, (kind: LineKind) => void] {
  const [hidden, setHidden] = useState<LineKind[]>(keptHidden);
  const toggle = (kind: LineKind) => setHidden((was) => {
    const next = was.includes(kind) ? was.filter((other) => other !== kind) : [...was, kind];
    store(KEY, JSON.stringify(next));
    return next;
  });
  return [new Set(LINE_KINDS.filter((kind) => !hidden.includes(kind))), toggle];
}

/** Which quests the map draws, as this viewer last chose (MAP4c): all of them until they choose. */
export function useMapWhen(): [When, (when: When) => void] {
  const [when, setWhen] = useState<When>(() => {
    const kept = stored(WHEN_KEY);
    return (WHENS as readonly string[]).includes(kept ?? '') ? kept as When : 'all';
  });
  const choose = (next: When) => {
    setWhen(next);
    store(WHEN_KEY, next);
  };
  return [when, choose];
}

/** How much each kind of line carries on this map: quests for the three kinds that move work, pairs for findings. */
export function lineCounts(topology: Topology): Record<LineKind, number> {
  return {
    quests: topology.quests.reduce((sum, edge) => sum + edge.quests.length, 0),
    asks: topology.asks.reduce((sum, edge) => sum + edge.quests.length, 0),
    chains: topology.chains.reduce((sum, edge) => sum + edge.steps.length + edge.waiting.length, 0),
    depends: topology.depends.length,
    knowledge: topology.knowledge.length,
  };
}

/** A kind's line as the map draws it, small, so the menu names each kind by its look as well as its word. */
function Sample({ kind }: { kind: LineKind }) {
  const dash = { quests: undefined, asks: ASKS_DASH, chains: CHAIN_DASH, depends: DEPENDS_DASH, knowledge: '5 5' }[kind];
  return (
    <svg width="28" height="8" aria-hidden className="shrink-0">
      <line
        x1="1" y1="4" x2="27" y2="4" strokeDasharray={dash} strokeLinecap={kind === 'chains' ? 'round' : undefined}
        className={cn(
          kind === 'knowledge' ? 'stroke-ink-faint' : kind === 'depends' ? 'stroke-ink-soft' : 'stroke-accent',
          kind === 'chains' ? 'stroke-2' : 'stroke-[1.5]',
        )}
      />
    </svg>
  );
}

/**
 * The switches for the map's lines (MAP4b) and which quests they draw (MAP4c): an options menu, as the
 * owner asked of every control that is not a direction. Each kind is ticked while it is drawn,
 * pictured by its line, and counted; below, one choice of which quests, all of them until chosen.
 */
export function LinesMenu({ shown, counts, onToggle, when, onWhen }: {
  shown: ReadonlySet<LineKind>;
  counts: Record<LineKind, number>;
  onToggle: (kind: LineKind) => void;
  when: When;
  onWhen: (when: When) => void;
}) {
  const { t } = useTranslation();
  const some = shown.size < LINE_KINDS.length;
  return (
    <Menu.Root>
      <Tip content={t('map.linesTip')}>
        <Menu.Trigger asChild>
          <button
            type="button"
            aria-label={t('map.linesLabel', { shown: shown.size, all: LINE_KINDS.length })}
            className={cn(
              'flex h-7 shrink-0 items-center gap-1.5 rounded-control px-2 text-small text-ink-soft',
              'transition-colors duration-(--speed) hover:bg-raised hover:text-ink data-[state=open]:bg-raised data-[state=open]:text-ink',
              'focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-accent',
            )}
          >
            {t('map.lines')}
            {some && <span className="font-mono text-meta tabular-nums text-ink-faint">{shown.size}/{LINE_KINDS.length}</span>}
            {when !== 'all' && <span className="text-meta text-ink-faint">· {t(`map.when.short.${when}`)}</span>}
          </button>
        </Menu.Trigger>
      </Tip>
      <Menu.Content align="end" className="min-w-64">
        {LINE_KINDS.map((kind) => (
          <Menu.CheckboxItem
            key={kind}
            checked={shown.has(kind)}
            // Stay open: a person switching lines usually switches more than one.
            onSelect={(event) => event.preventDefault()}
            onCheckedChange={() => onToggle(kind)}
          >
            <Sample kind={kind} />
            <span className="flex-1 truncate">{t(`map.kind.${kind}`)}</span>
            <span className="font-mono text-meta tabular-nums text-ink-faint">{counts[kind]}</span>
          </Menu.CheckboxItem>
        ))}
        <Menu.Separator />
        <Menu.Label className="pt-0.5">{t('map.when.title')}</Menu.Label>
        <Menu.RadioGroup value={when} onValueChange={(value) => onWhen(value as When)}>
          {WHENS.map((choice) => (
            <Menu.RadioItem key={choice} value={choice} onSelect={(event) => event.preventDefault()}>
              <span className="flex-1 truncate">{t(`map.when.${choice}`)}</span>
            </Menu.RadioItem>
          ))}
        </Menu.RadioGroup>
      </Menu.Content>
    </Menu.Root>
  );
}
