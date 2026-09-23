import { useTranslation } from 'react-i18next';
import type { Quest } from '../api';
import { Button, Chip, Pill, QUEST_TONE } from '../ui';
import type { MapSelection } from './MapCanvas';
import type { Topology } from './topology';

/** A list of quests, each with its state in words as well as in hue (D41). */
function QuestList({ quests }: { quests: Quest[] }) {
  const { t } = useTranslation();
  if (quests.length === 0) return <p className="m-0 text-small text-ink-faint">{t('map.detail.none')}</p>;
  return (
    <ul className="m-0 grid list-none gap-1.5 p-0">
      {quests.map((quest) => (
        <li key={quest.id} className="flex items-baseline justify-between gap-2">
          <span className="min-w-0 truncate text-small text-ink">{quest.title}</span>
          <Pill tone={QUEST_TONE[quest.status]}>{t(`status.${quest.status}`)}</Pill>
        </li>
      ))}
    </ul>
  );
}

/**
 * What the chosen part of the map holds (MAP2). It adds no action of its own: acting on a quest or a
 * session stays where it already lives, and this only says what is there.
 */
export function MapDetail({ topology, selected, onOpenConvergence }: {
  topology: Topology;
  selected: MapSelection | null;
  onOpenConvergence?: () => void;
}) {
  const { t } = useTranslation();

  if (!selected) {
    return <p className="m-0 text-body text-ink-soft">{t('map.detail.hint')}</p>;
  }

  if (selected.kind === 'node') {
    const node = topology.nodes.find((n) => n.id === selected.id);
    if (!node) return null;
    const into = topology.quests.filter((e) => e.to === node.id).flatMap((e) => e.quests);
    const out = topology.quests.filter((e) => e.from === node.id).flatMap((e) => e.quests);
    return (
      <div className="grid gap-3">
        <header className="flex flex-wrap items-center gap-2">
          <span className="text-body font-semibold text-ink">{node.id}</span>
          {node.working && <Chip accent>{t('map.working')}</Chip>}
        </header>
        {node.summary && <p className="m-0 text-small text-ink-soft">{node.summary}</p>}
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
        <section>
          <p className="m-0 mb-1 text-meta text-ink-faint">{t('map.detail.into')}</p>
          <QuestList quests={into} />
        </section>
        <section>
          <p className="m-0 mb-1 text-meta text-ink-faint">{t('map.detail.out')}</p>
          <QuestList quests={out} />
        </section>
      </div>
    );
  }

  if (selected.kind === 'quests') {
    const edge = topology.quests.find((e) => e.from === selected.from && e.to === selected.to);
    if (!edge) return null;
    return (
      <div className="grid gap-2">
        <p className="m-0 text-body font-semibold text-ink">{t('map.detail.edge', { from: edge.from, to: edge.to })}</p>
        <QuestList quests={edge.quests} />
      </div>
    );
  }

  const pair = topology.knowledge.find((e) => e.a === selected.a && e.b === selected.b);
  if (!pair) return null;
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
