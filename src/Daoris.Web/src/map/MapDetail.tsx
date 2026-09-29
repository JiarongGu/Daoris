import { useTranslation } from 'react-i18next';
import type { Quest } from '../api';
import { Button, Chip, Pill, QUEST_TONE } from '../ui';
import type { MapSelection } from './MapCanvas';
import type { Topology } from './topology';

/**
 * A list of quests, each with its state in words as well as in hue (D41), and each a door to its
 * drawer, where acting on it lives (design §1). They were text, so a person read a quest here and had
 * to find it again on Quests (UX5 U46, U26's rule). Status leads, as on every quest row (§4).
 */
function QuestList({ quests, onOpenQuest }: { quests: Quest[]; onOpenQuest?: (id: string) => void }) {
  const { t } = useTranslation();
  if (quests.length === 0) return <p className="m-0 text-small text-ink-faint">{t('map.detail.none')}</p>;
  return (
    <ul className="m-0 grid list-none gap-0.5 p-0">
      {quests.map((quest) => {
        const row = (
          <>
            <Pill tone={QUEST_TONE[quest.status]}>{t(`status.${quest.status}`)}</Pill>
            <span className="min-w-0 text-small text-ink">{quest.title}</span>
          </>
        );
        return (
          <li key={quest.id}>
            {onOpenQuest ? (
              <button
                type="button"
                onClick={() => onOpenQuest(quest.id)}
                className="flex w-full cursor-pointer items-baseline gap-2 rounded-control border-0 bg-transparent px-1 py-1 text-left hover:bg-accent-soft"
              >
                {row}
              </button>
            ) : <div className="flex items-baseline gap-2 px-1 py-1">{row}</div>}
          </li>
        );
      })}
    </ul>
  );
}

/**
 * A session there, in the hue the canvas and the rail give the same fact (UX5 U1, U45): waiting on
 * the person is open's, working is taken's. They were accent chips, and the accent is never a status.
 */
function SessionMark({ parked }: { parked: boolean }) {
  const { t } = useTranslation();
  return <Pill tone={parked ? 'open' : 'taken'}>{t(parked ? 'map.parked' : 'map.working')}</Pill>;
}

/**
 * What the chosen part of the map holds (MAP2). It adds no action of its own: acting on a quest or a
 * session stays where it already lives, and this only says what is there.
 */
export function MapDetail({ topology, selected, onOpenConvergence, onOpenCode, onOpenQuest }: {
  topology: Topology;
  selected: MapSelection | null;
  onOpenConvergence?: () => void;
  /** Open a quest named here in its drawer (U46). */
  onOpenQuest?: (id: string) => void;
  /** Open this repository's own code map (MAP3a) — the same map, one level in. */
  onOpenCode?: (repository: string) => void;
}) {
  const { t } = useTranslation();
  // A choice the map no longer holds (the scope moved, or the data did) is no choice: it left an
  // empty card (UX5 U47).
  const hint = <p className="m-0 text-body text-ink-soft">{t('map.detail.hint')}</p>;

  if (!selected) return hint;

  if (selected.kind === 'node') {
    const node = topology.nodes.find((n) => n.id === selected.id);
    if (!node) return hint;
    // What the asks put on it is a quest to it as well (MAP4b), whether or not its line is shown.
    const into = [
      ...topology.quests.filter((e) => e.to === node.id).flatMap((e) => e.quests),
      ...topology.asks.filter((e) => e.to === node.id).flatMap((e) => e.quests),
    ];
    const out = topology.quests.filter((e) => e.from === node.id).flatMap((e) => e.quests);
    const uses = topology.depends.filter((e) => e.from === node.id).map((e) => e.to);
    const usedBy = topology.depends.filter((e) => e.to === node.id).map((e) => e.from);
    return (
      <div className="grid gap-3">
        <header className="flex flex-wrap items-center gap-2">
          <span className="text-body font-semibold text-ink">{node.id}</span>
          {node.parked && <SessionMark parked />}
          {node.working && <SessionMark parked={false} />}
        </header>
        {/* Which circle, only when the map spans several: in one, it is the circle on screen (U49). */}
        {topology.circles > 1 && node.workspace && (
          <p className="m-0 -mt-2 text-meta text-ink-faint">{t('map.detail.circle', { workspace: node.workspace })}</p>
        )}
        {node.summary && <p className="m-0 text-small text-ink-soft">{node.summary}</p>}
        {onOpenCode && (
          <div>
            <Button variant="ghost" onClick={() => onOpenCode(node.id)}>{t('map.detail.openCode')}</Button>
          </div>
        )}
        {node.owns.length > 0 && (
          <section>
            <p className="m-0 mb-1 text-meta text-ink-faint">{t('map.detail.owns')}</p>
            <div className="flex flex-wrap gap-1">{node.owns.map((o) => <Chip key={o}>{o}</Chip>)}</div>
          </section>
        )}
        {node.accepts.length > 0 && (
          <section>
            <p className="m-0 mb-1 text-meta text-ink-faint">{t('map.detail.accepts')}</p>
            <div className="flex flex-wrap gap-1">{node.accepts.map((a) => <Chip key={a}>{a}</Chip>)}</div>
          </section>
        )}
        {/* What it says it uses, and who says they use it (D91): declarations, beside the others. */}
        {uses.length > 0 && (
          <section>
            <p className="m-0 mb-1 text-meta text-ink-faint">{t('map.detail.uses')}</p>
            <div className="flex flex-wrap gap-1">{uses.map((name) => <Chip key={name}>{name}</Chip>)}</div>
          </section>
        )}
        {usedBy.length > 0 && (
          <section>
            <p className="m-0 mb-1 text-meta text-ink-faint">{t('map.detail.usedBy')}</p>
            <div className="flex flex-wrap gap-1">{usedBy.map((name) => <Chip key={name}>{name}</Chip>)}</div>
          </section>
        )}
        <section>
          <p className="m-0 mb-1 text-meta text-ink-faint">{t('map.detail.into')}</p>
          <QuestList quests={into} onOpenQuest={onOpenQuest} />
        </section>
        <section>
          <p className="m-0 mb-1 text-meta text-ink-faint">{t('map.detail.out')}</p>
          <QuestList quests={out} onOpenQuest={onOpenQuest} />
        </section>
      </div>
    );
  }

  if (selected.kind === 'quests') {
    const edge = topology.quests.find((e) => e.from === selected.from && e.to === selected.to);
    if (!edge) return hint;
    return (
      <div className="grid gap-2">
        <p className="m-0 text-body font-semibold text-ink">{t('map.detail.edge', { from: edge.from, to: edge.to })}</p>
        <QuestList quests={edge.quests} onOpenQuest={onOpenQuest} />
      </div>
    );
  }

  if (selected.kind === 'asks') {
    const edge = topology.asks.find((e) => e.to === selected.to);
    if (!edge) return hint;
    return (
      <div className="grid gap-2">
        <p className="m-0 text-body font-semibold text-ink">{t('map.detail.asks', { to: edge.to })}</p>
        <div className="flex flex-wrap gap-1">{edge.asks.map((id) => <Chip key={id}>{t('chain.ask', { id })}</Chip>)}</div>
        <QuestList quests={edge.quests} onOpenQuest={onOpenQuest} />
      </div>
    );
  }

  if (selected.kind === 'chains') {
    const edge = topology.chains.find((e) => e.from === selected.from && e.to === selected.to);
    if (!edge) return hint;
    return (
      <div className="grid gap-2">
        <p className="m-0 text-body font-semibold text-ink">{t('map.detail.chain', { from: edge.from, to: edge.to })}</p>
        <p className="m-0 text-small text-ink-soft">{t('map.detail.chainWhat', { from: edge.from })}</p>
        {edge.steps.length > 0 && <QuestList quests={edge.steps} onOpenQuest={onOpenQuest} />}
        {edge.waiting.length > 0 && (
          <section>
            <p className="m-0 mb-1 text-meta text-ink-faint">{t('chain.pending')}</p>
            <ul className="m-0 grid list-none gap-0.5 p-0">
              {edge.waiting.map((step, index) => (
                <li key={`${step.to}-${index}`} className="px-1 py-1 text-small text-ink-soft">{step.title}</li>
              ))}
            </ul>
          </section>
        )}
      </div>
    );
  }

  if (selected.kind === 'depends') {
    const edge = topology.depends.find((e) => e.from === selected.from && e.to === selected.to);
    if (!edge) return hint;
    return (
      <div className="grid gap-2">
        <p className="m-0 text-body font-semibold text-ink">{t('map.detail.depends', { from: edge.from, to: edge.to })}</p>
        <p className="m-0 text-small text-ink-soft">{t('map.detail.dependsWhere', { from: edge.from })}</p>
      </div>
    );
  }

  const pair = topology.knowledge.find((e) => e.a === selected.a && e.b === selected.b);
  if (!pair) return hint;
  return (
    <div className="grid gap-2">
      <p className="m-0 text-body font-semibold text-ink">{t('map.detail.knowledge', { a: pair.a, b: pair.b })}</p>
      <p className="m-0 text-small text-ink-soft">{t('map.detail.knowledgeCount', { count: pair.groups })}</p>
      {onOpenConvergence && (
        <div>
          <Button variant="ghost" onClick={onOpenConvergence}>{t('map.detail.openConvergence')}</Button>
        </div>
      )}
    </div>
  );
}
