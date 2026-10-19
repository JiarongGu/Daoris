import { useTranslation } from 'react-i18next';
import type { SystemWiring } from '../../data/wiring';
import { PathText, SettingRow } from '../../design/ui';
import { size } from '../../locales/format';
import { Door, Fact, WiringCard } from './WiringParts';

/** The variable that names the store's file, else it lies in the home's data folder (§3.8). */
const DB_VARIABLE = 'DAORIS_KNOWLEDGE_DB';

/**
 * The knowledge store (the this-Daoris design §3.8): its file and what named it, its size counting the write-ahead log, the
 * family root the hosted service indexes (what this window handed it), and the tier the service reports, with the door to AI
 * features, where the tier is set.
 *
 * @remarks
 * A molecule: drawn from props. The tier is the service's (§3.2), so the organism hands it in with its why. An adopted
 * service's root is its own, and the row says so.
 */
export function WiringKnowledge({ knowledge, tier, tierWhy, onAi }: {
  knowledge: SystemWiring['knowledge'];
  tier: string | null;
  tierWhy: string | null;
  onAi: () => void;
}) {
  const { t } = useTranslation();
  return (
    <WiringCard id="settings-daoris-knowledge" title={t('settings.daoris.card.knowledge')}>
      <SettingRow
        label={t('settings.daoris.label.store')}
        hint={knowledge.from ? t(`settings.daoris.knowledge.from.${knowledge.from}`, { name: DB_VARIABLE }) : undefined}
        control={<Fact value={knowledge.path} why={knowledge.pathWhy}><PathText path={knowledge.path ?? ''} className="text-small text-ink-soft" /></Fact>}
      />
      <SettingRow
        label={t('settings.daoris.label.storeSize')}
        control={<Fact value={knowledge.bytes} why={knowledge.bytesWhy}><span className="text-small text-ink-soft">{size(knowledge.bytes ?? 0)}</span></Fact>}
      />
      <SettingRow
        label={t('settings.daoris.label.familyRoot')}
        control={<Fact value={knowledge.familyRoot} why={knowledge.familyRootWhy}><PathText path={knowledge.familyRoot ?? ''} className="text-small text-ink-soft" /></Fact>}
      />
      <SettingRow
        label={t('settings.daoris.label.tier')}
        control={(
          <>
            <Fact value={tier} why={tierWhy}><span className="text-small text-ink-soft">{tier}</span></Fact>
            <Door label={t('settings.daoris.button.ai')} onPress={onAi} />
          </>
        )}
      />
    </WiringCard>
  );
}
