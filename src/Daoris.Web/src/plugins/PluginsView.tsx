import { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  usePickFolder, usePluginAction, usePluginInstall, usePluginNew, usePlugins, usePluginTry, usePluginUpdate,
} from '../shell';
import { sentence } from '../format';
import { Drawer, failure, type Notify, useErrorNotify } from '../ui';
import { type KitPoint, PluginKitCard, type PluginTrialResult } from '../settings/PluginKit';
import type { PluginUpdatePlanShown } from '../settings/PluginUpdate';
import { ListMore } from '../work/ListPane';
import type { ViewLayout } from '../work/ViewFrame';
import { chosenOf, type OfferShown, pluginGroups, type PluginShown } from './catalog';
import { OfferPage } from './OfferPage';
import { PluginList, PluginStrip } from './PluginList';
import { PluginMainNotice, PluginPage } from './PluginPage';

/** The driver's word for a plugin this machine does not hold: read by its code, never by its sentence (D48 §6). */
const UNKNOWN = 'PLUGIN_UNKNOWN';
const unknown = (error: unknown) => (error as { code?: unknown } | null)?.code === UNKNOWN;

const list = <T,>(value: unknown): T[] => (Array.isArray(value) ? value as T[] : []);

/**
 * A catalogue as an older or a stranger shell may answer it, read defensively (SES1's reason): a field it never sent is
 * an empty list, never a blank page.
 */
function installedOf(raw: unknown): PluginShown[] {
  return list<PluginShown>(raw).map((plugin) => ({
    ...plugin,
    harnesses: list<string>(plugin.harnesses),
    points: list<string>(plugin.points),
  }));
}
function offersOf(raw: unknown): OfferShown[] {
  return list<OfferShown>(raw).map((offer) => ({
    ...offer,
    harnesses: list<string>(offer.harnesses),
    points: list<string>(offer.points),
    servers: list<string>(offer.servers),
    needs: list<string>(offer.needs),
  }));
}

/**
 * **The Plugins view** (PLUGUI1b, D119 §3): what it hands the frame (D118 §5), its list pane and its main area, built on
 * today's `PLUGINS` answer. The organism: it holds the catalogue and a plugin's acts, so the list, the strip, the page
 * and the offer's page below it hold none.
 *
 * @remarks
 * **A hook, because a view hands the frame a value** (`ViewLayout`): the list and the main area are drawn in two
 * places the frame decides, so one component could not hold both. The application holds it on every view, and it asks
 * the driver for nothing until the view is in front (`active`).
 *
 * **What the page holds lives as long as the application**: a trial's report, an update's plan, *Remove…*'s ask. A
 * trial's report therefore stays on its plugin's page across a change of view, until Daoris closes; PLUGUI1g keeps it
 * on the machine.
 *
 * **Every act has its terminal twin** (D50), named at the page's foot: `daoris plugin enable|disable|update|remove`,
 * `daoris plugin add --offer`, and `daoris-driver plugins new|try`.
 */
export function usePluginsView({ active, chosen, onChoose, notify, onAsk }: {
  /** The view is in front: only then is the catalogue asked for. */
  active: boolean;
  /** The list's chosen item, which the application remembers (`daoris.list.plugins.chosen`). */
  chosen: string | null;
  onChoose: (item: string | null) => void;
  notify: Notify;
  /** Ask Daoris, opened on a first message (SETUP1b's `askSetup`); absent where there is no Ask Daoris. */
  onAsk?: (message: string) => void;
}): ViewLayout {
  const { t, i18n } = useTranslation();
  const catalog = usePlugins({ enabled: active });
  const act = usePluginAction();
  const trying = usePluginTry();
  const updating = usePluginUpdate();
  const installing = usePluginInstall();
  // Each plugin's last trial and its update's plan, by id; the plugin whose removal asks.
  const [trials, setTrials] = useState<Record<string, PluginTrialResult>>({});
  const [plans, setPlans] = useState<Record<string, PluginUpdatePlanShown>>({});
  const [asking, setAsking] = useState<string | null>(null);
  const [kit, setKit] = useState<'make' | 'try' | null>(null);

  // An error keeps the last answer and is said once, while the view is in front (D118 §3h).
  useErrorNotify(active ? catalog.error : null, notify);

  const answered = Array.isArray(catalog.data?.plugins);
  const plugins = answered ? installedOf(catalog.data?.plugins) : [];
  const offers = offersOf(catalog.data?.offers);
  // An older shell has never heard of the kit (PLUG8), and is offered nothing that needs it.
  const kitPoints = Array.isArray(catalog.data?.kit?.points) ? catalog.data.kit.points as KitPoint[] : null;
  const groups = pluginGroups(plugins, offers, i18n.language);
  const loading = active && !answered && catalog.isPending;
  const unanswered = !answered && catalog.error ? sentence(catalog.error) : null;

  // A plugin the driver no longer holds: the catalogue is asked again, and the page says it has gone.
  const failed = (error: unknown) => {
    if (unknown(error)) void catalog.refetch();
    else failure(notify)(error);
  };

  const askMessage = t('plugin.list.askMessage');
  // The ＋'s kinds (D119 §3.1): asking first, since a plugin is made as an ask with its tests (PLUG9); then the kit.
  const kinds = [
    ...(onAsk ? [{ id: 'ask', label: t('plugin.list.ask') }] : []),
    ...(kitPoints ? [{ id: 'make', label: t('plugin.list.make') }] : []),
  ];
  const make = (kind?: string) => {
    if (kind === 'make') setKit('make');
    else if (kind === 'ask') onAsk?.(askMessage);
  };

  const switchOne = (id: string, action: 'enable' | 'disable') => act.mutate({ id, action }, {
    onSuccess: () => notify(t(action === 'enable' ? 'plugin.enabled' : 'plugin.disabled', { id })),
    onError: failed,
  });
  const remove = (id: string) => act.mutate({ id, action: 'remove' }, {
    onSuccess: (result) => {
      setAsking(null);
      notify(t(result.data ? 'plugin.removedKept' : 'plugin.removed', { id, data: result.data ?? '' }));
      // Removed by the person's own press, so the page goes back to choosing, not to *gone*.
      onChoose(null);
    },
    onError: failed,
  });
  const tryOne = (id: string) => trying.mutate({ id }, {
    onSuccess: (trial) => setTrials((was) => ({ ...was, [id]: trial })),
    onError: failed,
  });
  const putAway = (id: string) => setPlans((was) => {
    const kept = { ...was };
    delete kept[id];
    return kept;
  });
  // The first press asks what would change; the plan's own press makes it.
  const askUpdate = (id: string) => updating.mutate({ id }, {
    onSuccess: (plan) => setPlans((was) => ({ ...was, [id]: { ...plan, changes: list(plan.changes) } })),
    onError: failed,
  });
  const update = (id: string) => updating.mutate({ id, apply: true }, {
    onSuccess: () => {
      putAway(id);
      notify(t('plugin.update.done', { id }));
    },
    onError: failed,
  });
  // Once an offer is installed, the list chooses the installed plugin (D119 §3.1).
  const install = (id: string) => installing.mutate(id, {
    onSuccess: (added) => {
      notify(t('plugin.offers.installed', { id: added.id }));
      onChoose(added.id);
    },
    onError: failure(notify),
  });

  const shown = chosenOf(chosen, plugins, offers);
  // Which installed plugin a running trial is of: a folder's trial is the kit's, in its drawer.
  const tried = trying.isPending && trying.variables && 'id' in trying.variables ? trying.variables.id : null;

  const main = loading
    ? <PluginMainNotice state="loading" />
    : unanswered
      ? <PluginMainNotice state="unanswered" sentence={unanswered} />
      : shown.kind === 'plugin'
        ? (
          <PluginPage
            key={shown.plugin.id}
            plugin={shown.plugin}
            kitPoints={kitPoints}
            canTry={kitPoints !== null}
            trial={trials[shown.plugin.id] ?? null}
            trying={tried === shown.plugin.id}
            plan={plans[shown.plugin.id] ?? null}
            updating={updating.isPending}
            acting={act.isPending}
            asking={asking === shown.plugin.id}
            onSwitch={switchOne}
            onTry={tryOne}
            onAskUpdate={askUpdate}
            onApplyUpdate={update}
            onCancelUpdate={() => putAway(shown.plugin.id)}
            onAskRemove={setAsking}
            onRemove={remove}
            onCancelRemove={() => setAsking(null)}
          />
        )
        : shown.kind === 'offer'
          ? <OfferPage key={shown.offer.id} offer={shown.offer} kitPoints={kitPoints} installing={installing.isPending} onInstall={install} />
          : <PluginMainNotice state={shown.kind === 'gone' && answered ? 'gone' : 'none'} actions={kinds} onAct={make} />;

  return {
    list: {
      view: 'plugins',
      name: t('nav.plugins'),
      labels: { open: t('plugin.list.open'), close: t('plugin.list.close'), resize: t('plugin.list.resize') },
      make: kinds.length > 0 ? { label: t('plugin.list.add'), kinds, onMake: make } : undefined,
      // The list's ⋯ (D119 §3.1): *Try a folder…* opens the kit's drawer at its trial.
      more: kitPoints ? <ListMore label={t('plugin.list.more')} items={[{ id: 'try', label: t('plugin.list.tryFolder') }]} onChoose={() => setKit('try')} /> : undefined,
      strip: answered ? <PluginStrip groups={groups} chosen={chosen} onChoose={onChoose} /> : undefined,
      loading,
      empty: answered && plugins.length === 0 && offers.every((offer) => offer.installed)
        ? { headline: t('plugin.empty.headline'), body: t('plugin.empty.body') }
        : undefined,
      chosen,
      // A remembered plugin reopens only while this machine still holds it (UX6b): one removed opens nothing chosen.
      standing: !chosen ? undefined : !answered ? 'unread' : shown.kind === 'gone' ? 'gone' : 'live',
      body: <PluginList groups={groups} chosen={chosen} installing={installing.isPending} unanswered={unanswered} onChoose={onChoose} onInstall={install} />,
    },
    main: (
      <>
        {main}
        {kit && kitPoints && <KitDrawer step={kit} points={kitPoints} notify={notify} onClose={() => setKit(null)} />}
      </>
    ),
  };
}

/**
 * The kit's drawer (PLUG8, D101; D119 §3.4): *Make a plugin*'s form as it stands, and *Try a folder* under it. The
 * list's ⋯ opens it at its trial, with the folder's field to type in. It installs nothing, and says so.
 */
function KitDrawer({ step, points, notify, onClose }: {
  step: 'make' | 'try';
  points: KitPoint[];
  notify: Notify;
  onClose: () => void;
}) {
  const { t } = useTranslation();
  const make = usePluginNew();
  const trying = usePluginTry();
  const pick = usePickFolder();
  const [made, setMade] = useState<string | null>(null);
  const [trial, setTrial] = useState<PluginTrialResult | null>(null);
  const body = useRef<HTMLDivElement>(null);

  // At its trial: the focus in the folder's field, once the drawer has taken the focus it takes on opening.
  useEffect(() => {
    if (step !== 'try') return undefined;
    const timer = window.setTimeout(() => {
      const field = body.current?.querySelector<HTMLInputElement>(`input[aria-label="${t('plugin.kit.tryFolderField')}"]`);
      field?.scrollIntoView?.({ block: 'nearest' });
      field?.focus();
    }, 0);
    return () => window.clearTimeout(timer);
  }, [step, t]);

  return (
    <Drawer title={t(step === 'make' ? 'plugin.kit.title' : 'plugin.kit.tryFolder')} onClose={onClose}>
      <div ref={body}>
        <PluginKitCard
          points={points}
          busy={make.isPending || trying.isPending}
          made={made}
          trial={trial}
          onPick={async () => {
            try {
              return (await pick.mutateAsync())?.path ?? null;
            } catch (error) {
              failure(notify)(error);
              return null;
            }
          }}
          onMake={(plugin) => make.mutate(plugin, {
            onSuccess: (result) => {
              setMade(result.folder);
              notify(t('plugin.kit.made', { id: result.id, folder: result.folder }));
            },
            onError: failure(notify),
          })}
          onTry={(folder) => trying.mutate({ folder }, { onSuccess: setTrial, onError: failure(notify) })}
        />
      </div>
    </Drawer>
  );
}
