import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { CodeMapCanvas, CodeMapDetail } from './map/CodeMap';
import { MapCanvas, type MapSelection } from './map/MapCanvas';
import { LayeredMap } from './map/LayeredMap';
import { RING_MAX } from './map/layers';
import { LinesMenu, lineCounts, useMapWhen, useShownLines } from './map/LinesMenu';
import { MapDetail } from './map/MapDetail';
import { buildTopology, keepQuests, type LineKind, showLines } from './map/topology';
import { useCodeMap, useConvergence, useQuests, useRegistry, useSessions } from './queries';
import { useScope } from './scope';
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
export function MapView({ notify, code: opening = null, onOpenConvergence, onOpenQuest }: {
  notify: Notify;
  /**
   * The repository whose code map it opens on, where a door named one: a repository's page (FRAME1e). The Map then
   * holds it as its own, and goes back to the workspace from it as from any code map.
   */
  code?: string | null;
  onOpenConvergence?: () => void;
  /** A quest named in a detail opens on its page, on Quests (UX5 U46; FRAME1d). */
  onOpenQuest?: (id: string) => void;
}) {
  const { t } = useTranslation();
  const registry = useRegistry();
  const { workspace } = useScope();
  const quests = useQuests(null, true);
  const shared = useConvergence(SHARED);
  const sessions = useSessions(null, false);
  const [selected, setSelected] = useState<MapSelection | null>(null);
  const [shownLines, toggleShown] = useShownLines();
  const [when, setWhen] = useMapWhen();
  // One level in (MAP3a): the repository whose own code map is open, and the module chosen on it.
  const [code, setCode] = useState<string | null>(opening);
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
            <p className="m-0 whitespace-pre-wrap border-l-[3px] border-warn pl-2.5 text-body text-ink-soft">
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
            // A teammate's held commit that keeps none (MAP3e) says so by that commit: the advice
            // about committing a file is for a repository whose checkout is here.
            body={answer.fed
              ? t('code.none.fedBody', { commit: answer.fed.shortCommit })
              : t('code.none.body')}
          />
        )}
        {answer?.file && !answer.problem && (
          // Beside the code map by the main area's own width, as the workspace map's detail is (D118 §3b).
          <div className="grid items-start gap-4 @4xl/main:grid-cols-[minmax(0,1fr)_20rem]">
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
                fed={answer.fed}
                onSelect={setModule}
              />
            </Card>
          </div>
        )}
      </section>
    );
  }

  const all = buildTopology(
    registry.data ?? [], quests.data ?? [],
    // Convergence may fail on its own — a lexical-only machine still has repositories and quests.
    shared.data ?? [], sessions.data ?? [],
    // Which quests, as the person chose (MAP4c); a window is measured from this render.
    keepQuests(when, Date.now()));
  // What is drawn is what the person chose to see (MAP4b); the detail still reads everything.
  const topology = showLines(all, shownLines);
  const toggleLine = (kind: LineKind) => {
    // A chosen line of a kind going out of sight is no longer a choice on the map.
    if (selected && selected.kind === kind) setSelected(null);
    toggleShown(kind);
  };
  const lines = (
    <LinesMenu shown={shownLines} counts={lineCounts(all)} onToggle={toggleLine} when={when} onWhen={setWhen} />
  );
  // Scoped to every workspace of several, it says so, as its empty state does (UX5 U49).
  const header = (
    <PageHeader
      title={t('map.title')}
      description={t(topology.circles > 1 ? 'map.descriptionAll' : 'map.description')}
    />
  );

  if (registry.isPending || quests.isPending) {
    return <section>{header}<SkeletonRows rows={4} /></section>;
  }

  if (topology.nodes.length === 0) {
    return (
      <section>
        {header}
        {/* Scoped to every circle there is no "this circle" to speak of, which it did on the window. */}
        <EmptyState
          icon="map"
          headline={t(workspace ? 'map.empty.headline' : 'map.empty.headlineAll')}
          body={t('map.empty.body')}
        />
      </section>
    );
  }

  return (
    <section>
      {header}
      {/* The detail beside the canvas by the main area's own width, never the viewport's (D118 §3b, audit
          MA3), and under it in a narrow one, where the drawing shrinks only as a last resort (U44). It stays
          in the main area: a view's own detail never goes to the side bar (§3c, §4). */}
      <div className="grid items-start gap-4 @4xl/main:grid-cols-[minmax(0,1fr)_20rem]">
        <Card>
          {/* A ring holds a handful; a bigger circle is laid out in layers, which pan and zoom (MAP4). */}
          {topology.nodes.length > RING_MAX
            ? <LayeredMap topology={topology} selected={selected} onSelect={(next) => setSelected(next)} tools={lines} />
            : (
              <>
                <div className="mb-1 flex justify-end">{lines}</div>
                <MapCanvas topology={topology} selected={selected} onSelect={(next) => setSelected(next)} />
              </>
            )}
          {/* The key, in words: each line's meaning is its shape as well as its hue (D41) — and the
              number in a node, which no key named until the window showed it (POLISH4). A kind out of
              sight leaves the key with its lines. */}
          <ul className="m-0 mt-2 flex list-none flex-wrap gap-x-4 gap-y-1 p-0 text-meta text-ink-faint">
            <li>{t('map.legend.open')}</li>
            {shownLines.has('quests') && <li>{t('map.legend.quests')}</li>}
            {shownLines.has('quests') && <li>{t('map.legend.closed')}</li>}
            {shownLines.has('asks') && <li>{t('map.legend.asks')}</li>}
            {shownLines.has('chains') && <li>{t('map.legend.chains')}</li>}
            {shownLines.has('depends') && <li>{t('map.legend.depends')}</li>}
            {shownLines.has('knowledge') && <li>{t('map.legend.knowledge')}</li>}
            <li>{t('map.legend.working')}</li>
          </ul>
          {topology.outside > 0 && (
            <p className="m-0 mt-2 text-small text-ink-soft">{t('map.outside', { count: topology.outside })}</p>
          )}
        </Card>
        <Card>
          <MapDetail
            topology={all}
            selected={selected}
            onOpenConvergence={onOpenConvergence}
            onOpenCode={setCode}
            onOpenQuest={onOpenQuest}
          />
        </Card>
      </div>
    </section>
  );
}
