import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  usePickFolder, usePluginAction, usePluginInstall, usePluginNew, usePlugins, usePluginTry, usePluginUpdate,
} from '../shell';
import {
  Button, Card, failure, Inline, type Notify, PathText, Pill, Prose, SettingRow, Tip, useErrorNotify,
} from '../ui';
import { PluginKitCard, TrialReport, type KitPoint, type PluginTrialResult } from './PluginKit';
import { PluginOffersCard } from './PluginOffers';
import { PluginSourceLine, PluginUpdatePlan, updatable, type PluginUpdatePlanShown } from './PluginUpdate';

/**
 * This machine's plugins (D64): one row per folder under the home's `plugins/` — what it declares,
 * what it speaks on, whether it is running, and why it contributes nothing when it does not.
 *
 * **Two doors, one folder** (D50): the switch is a row in `plugins.json` that `daoris plugin
 * enable|disable` edits too, and Remove takes the install folder while naming what the plugin kept.
 * **No plugin code runs in this page** — a plugin's word reaches the console under `plugin:<id>`.
 *
 * A plugin that speaks can be tried where it stands (PLUG8): the shell starts it as the driver would and
 * the report sits under its row. Beneath the catalogue, the kit a plugin is made with.
 *
 * Each row says where its plugin came from, and one with a record is updated by two presses (PLUG9 c):
 * *Update…* asks what would change, shown under the row, and *Update now* makes it. Beneath the catalogue,
 * Daoris's own plugins the install carries, each installed only by its press (PLUG9 d, D103).
 */
export function PluginsDomain({ notify }: { notify: Notify }) {
  const { t } = useTranslation();
  const catalog = usePlugins();
  const act = usePluginAction();
  const trying = usePluginTry();
  const updating = usePluginUpdate();
  const installing = usePluginInstall();
  // Each installed plugin's last trial, by id, shown under its row until it is tried again.
  const [trials, setTrials] = useState<Record<string, PluginTrialResult>>({});
  // What an update of each plugin would change, by id, shown under its row until it is pressed or put away.
  const [plans, setPlans] = useState<Record<string, PluginUpdatePlanShown>>({});
  useErrorNotify(catalog.error, notify);

  // Defensive about the shape, for SES1's reason: a shell older than this surface answers something
  // else entirely to a question it has never heard, and the page must not go blank for it.
  const plugins = Array.isArray(catalog.data?.plugins) ? catalog.data.plugins : null;
  if (!catalog.data || !plugins) return null;

  const run = (id: string, action: 'enable' | 'disable' | 'remove') => act.mutate({ id, action }, {
    onSuccess: (result) => notify(t(
      action === 'remove'
        ? (result.data ? 'plugin.removedKept' : 'plugin.removed')
        : action === 'enable' ? 'plugin.enabled' : 'plugin.disabled',
      { id, data: result.data ?? '' })),
    onError: failure(notify),
  });

  const what = (plugin: (typeof plugins)[number]) => {
    const parts = [
      plugin.harnesses.length > 0 ? t('plugin.declares', { harnesses: plugin.harnesses.join(', ') }) : null,
      plugin.points.length > 0 ? t('plugin.speaks', { points: plugin.points.join(', ') }) : null,
    ].filter(Boolean);
    return parts.length > 0 ? parts.join('; ') : t('plugin.quiet');
  };

  const tryInstalled = (id: string) => trying.mutate({ id }, {
    onSuccess: (trial) => setTrials((was) => ({ ...was, [id]: trial })),
    onError: failure(notify),
  });
  const putAway = (id: string) => setPlans((was) => {
    const kept = { ...was };
    delete kept[id];
    return kept;
  });
  // The first press asks what would change; the plan's own press makes it.
  const askUpdate = (id: string) => updating.mutate({ id }, {
    onSuccess: (plan) => setPlans((was) => ({ ...was, [id]: { ...plan, changes: Array.isArray(plan.changes) ? plan.changes : [] } })),
    onError: failure(notify),
  });
  const update = (id: string) => updating.mutate({ id, apply: true }, {
    onSuccess: () => {
      putAway(id);
      notify(t('plugin.update.done', { id }));
    },
    onError: failure(notify),
  });
  const install = (id: string) => installing.mutate(id, {
    onSuccess: (added) => notify(t('plugin.offers.installed', { id: added.id })),
    onError: failure(notify),
  });
  // An older shell has never heard of the kit, and gets no card for it.
  const kit = Array.isArray(catalog.data.kit?.points) ? catalog.data.kit.points : null;
  // Nor of the install's own plugins (PLUG9 d).
  const offers = Array.isArray(catalog.data.offers) ? catalog.data.offers : [];

  return (
    <>
    <Card className="mt-3.5">
      <SettingRow
        label={t('plugin.folder')}
        hint={t('plugin.terminal')}
        why={t('plugin.body')}
        control={<PathText path={catalog.data.folder} className="text-small text-ink-faint" />}
      />

      {plugins.length === 0 ? (
        <Prose className="mt-3 text-small"><Inline text={t('plugin.none')} /></Prose>
      ) : plugins.map((plugin) => (
        <SettingRow
          key={plugin.id}
          label={(
            <span className="flex flex-wrap items-center gap-2">
              <span>{plugin.name}</span>
              {plugin.version && <span className="font-mono text-small text-ink-faint">{plugin.version}</span>}
              {plugin.running && <Pill tone="done">{t('plugin.running')}</Pill>}
              {!plugin.enabled && <Pill tone="neutral">{t('plugin.off')}</Pill>}
            </span>
          )}
          hint={(
            <span className="flex flex-col gap-0.5">
              {/* A refused plugin declares nothing BECAUSE it was refused — saying "declares nothing"
                  above the sentence that says why would be the same fact twice, the second time
                  wrong. Its sentence stands alone beneath. */}
              {!plugin.problem && <span>{what(plugin)}{plugin.description ? ` — ${plugin.description}` : ''}</span>}
              <span className="truncate font-mono text-meta">{plugin.folder}</span>
              {/* Where it came from (PLUG9 c): an older shell sends nothing, and nothing is said. */}
              <PluginSourceLine source={plugin.source} />
            </span>
          )}
          control={(
            <>
              {/* A plugin that speaks can be tried; one that only declares runs nothing to try. */}
              {kit && !plugin.problem && plugin.points.length > 0 && (
                <Button
                  variant="ghost"
                  disabled={trying.isPending}
                  aria-label={t('plugin.kit.tryNamed', { id: plugin.id })}
                  onClick={() => tryInstalled(plugin.id)}
                >
                  {t('plugin.kit.try')}
                </Button>
              )}
              {/* Only where there is a source to read: one with no record has nothing to update from. */}
              {updatable(plugin.source) && (
                <Button
                  variant="ghost"
                  disabled={updating.isPending}
                  aria-label={t('plugin.update.askNamed', { id: plugin.id })}
                  onClick={() => askUpdate(plugin.id)}
                >
                  {t('plugin.update.ask')}
                </Button>
              )}
              <Button
                variant="ghost"
                disabled={act.isPending}
                onClick={() => run(plugin.id, plugin.enabled ? 'disable' : 'enable')}
              >
                {t(plugin.enabled ? 'plugin.disable' : 'plugin.enable')}
              </Button>
              <Tip content={t('plugin.forgetTip')}>
                <Button variant="ghost" disabled={act.isPending} onClick={() => run(plugin.id, 'remove')}>
                  {t('plugin.forget')}
                </Button>
              </Tip>
            </>
          )}
        >
          {/* The driver's own sentence, verbatim — a version this build does not speak, a
              conflict naming both sides, a manifest that would not parse. Content, not chrome. */}
          {plugin.problem && (
            <p className="max-w-prose border-l-[3px] border-warn bg-page/60 px-3.5 py-2 text-body text-ink-soft">
              <Inline text={plugin.problem} />
            </p>
          )}
          {trials[plugin.id] && <TrialReport trial={trials[plugin.id]!} />}
          {plans[plugin.id] && (
            <PluginUpdatePlan
              plan={plans[plugin.id]!}
              busy={updating.isPending}
              onApply={update}
              onCancel={() => putAway(plugin.id)}
            />
          )}
        </SettingRow>
      ))}
    </Card>
    <PluginOffersCard offers={offers} busy={installing.isPending} onInstall={install} />
    {kit && <PluginKitSection notify={notify} points={kit} />}
    </>
  );
}

/**
 * The kit a plugin is made with (PLUG8, D101): the screen's half of `daoris-driver plugins new|try`.
 * New writes into the folder the person picks or types and installs nothing; Try runs a folder's plugin.
 */
function PluginKitSection({ notify, points }: { notify: Notify; points: KitPoint[] }) {
  const { t } = useTranslation();
  const make = usePluginNew();
  const trying = usePluginTry();
  const pick = usePickFolder();
  const [made, setMade] = useState<string | null>(null);
  const [trial, setTrial] = useState<PluginTrialResult | null>(null);

  return (
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
  );
}
