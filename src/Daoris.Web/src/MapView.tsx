import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { CodeMapCanvas, CodeMapDetail } from './map/CodeMap';
import { MapCanvas, type MapSelection } from './map/MapCanvas';
import { MapDetail } from './map/MapDetail';
import { buildTopology } from './map/topology';
import { useCodeMap, useConvergence, useQuests, useRegistry, useSessions } from './queries';
import {
  Button, Card, EmptyState, Icon, type Notify, PageHeader, SkeletonRows, useErrorNotify,
} from './ui';

/** The similarity the Convergence view opens at — the map's dotted lines are the same findings. */
const SHARED = 0.75;

/**
 * The workspace map (MAP2, D67 §3; `docs/2026-09-23-map-design.md` §1): the circle's repositories and
 * what actually moved between them. A view of its own, the owner's choice.
 *
 * @remarks
 * Read-only, and read from what every other view already reads — the registry, the quests, the
 * convergence findings, the live sessions — so it adds no request shape the service does not serve,
 * and nothing machine-local: the same map in a browser and on the desktop (D47 §4).
 */
export function MapView({ notify, onOpenConvergence }: {
  notify: Notify;
  onOpenConvergence?: () => void;
}) {
  const { t } = useTranslation();
  const registry = useRegistry();
  const quests = useQuests(null, true);
  const shared = useConvergence(SHARED);
  const sessions = useSessions(null, false);
  const [selected, setSelected] = useState<MapSelection | null>(null);
  // One level in (MAP3a): the repository whose own code map is open, and the module chosen on it.
  const [code, setCode] = useState<string | null>(null);
  const [module, setModule] = useState<string | null>(null);
  const codeMap = useCodeMap(code);
  useErrorNotify(registry.error ?? quests.error ?? sessions.error ?? codeMap.error, notify);

  if (code !== null) {
    const back = () => { setCode(null); setModule(null); };
    const answer = codeMap.data;
    return (
      <section>
        <PageHeader
          title={t('code.title', { repository: code })}
          description={t('code.description')}
          action={<Button variant="ghost" onClick={back}><Icon name="map" size={14} />{t('code.back')}</Button>}
        />
        {codeMap.isPending && <SkeletonRows rows={4} />}
        {answer?.problem && (
          /* The service's own sentence, whole: it names the first rule the file broke (§3). */
          <Card>
            <p className="m-0 mb-2 text-body font-semibold text-ink">{t('code.refused')}</p>
            <p className="m-0 max-w-prose whitespace-pre-wrap border-l-[3px] border-warn pl-2.5 text-body text-ink-soft">
              {answer.problem}
            </p>
          </Card>
        )}
        {/* 🔴 ABSENT, not null: the host leaves a null field out of its answer, and `=== null` drew
            nothing at all for a repository with no map (found by the browser gate). */}
        {answer && !answer.problem && !answer.file && (
          <EmptyState
            icon="map"
            headline={t('code.none.headline', { repository: code })}
            body={t('code.none.body')}
          />
        )}
        {answer?.file && !answer.problem && (
          <div className="grid items-start gap-4 lg:grid-cols-[minmax(0,1fr)_20rem]">
            <Card>
              <CodeMapCanvas
                repository={code}
                modules={answer.modules}
                dependencies={answer.dependencies}
                selected={module}
                onSelect={setModule}
              />
              <p className="m-0 mt-2 text-meta text-ink-faint">{t('code.legend')}</p>
            </Card>
            <Card>
              <CodeMapDetail
                modules={answer.modules}
                dependencies={answer.dependencies}
                selected={module}
                file={answer.file}
                onSelect={setModule}
              />
            </Card>
          </div>
        )}
      </section>
    );
  }

  const header = <PageHeader title={t('map.title')} description={t('map.description')} />;

  if (registry.isPending || quests.isPending) {
    return <section>{header}<SkeletonRows rows={4} /></section>;
  }

  const topology = buildTopology(
    registry.data ?? [], quests.data ?? [],
    // Convergence may fail on its own — a lexical-only machine still has repositories and quests.
    shared.data ?? [], sessions.data ?? []);

  if (topology.nodes.length === 0) {
    return (
      <section>
        {header}
        <EmptyState icon="map" headline={t('map.empty.headline')} body={t('map.empty.body')} />
      </section>
    );
  }

  return (
    <section>
      {header}
      <div className="grid items-start gap-4 lg:grid-cols-[minmax(0,1fr)_20rem]">
        <Card>
          <MapCanvas
            topology={topology}
            selected={selected}
            onSelect={(next) => setSelected(next)}
          />
          {/* The key, in words: each line's meaning is its shape as well as its hue (D41). */}
          <ul className="m-0 mt-2 flex list-none flex-wrap gap-x-4 gap-y-1 p-0 text-meta text-ink-faint">
            <li>{t('map.legend.quests')}</li>
            <li>{t('map.legend.closed')}</li>
            <li>{t('map.legend.knowledge')}</li>
            <li>{t('map.legend.working')}</li>
          </ul>
          {topology.outside > 0 && (
            <p className="m-0 mt-2 text-small text-ink-soft">{t('map.outside', { count: topology.outside })}</p>
          )}
        </Card>
        <Card>
          <MapDetail
            topology={topology}
            selected={selected}
            onOpenConvergence={onOpenConvergence}
            onOpenCode={setCode}
          />
        </Card>
      </div>
    </section>
  );
}
