import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { Icon, type IconName, Segmented, StripMark } from '../design/ui';
import { AGENTS_MODES, type AgentsMode } from './modes';

/** Each mode's glyph on the strip. */
const MODE_GLYPH: Record<AgentsMode, IconName> = { agents: 'terminal', setup: 'workflow' };

/**
 * **The Agents place's list head** (COMPANYVIEW1b, the company-view design §1): a two-way choice, *Agents · Setup*, over the mode's own
 * list, as Knowledge's head is. It stays in reach above the list's rows, outside their scroll.
 *
 * A molecule: the mode in front arrives, and each choice goes out.
 */
export function AgentsModes({ mode, onMode }: { mode: AgentsMode; onMode: (mode: AgentsMode) => void }) {
  const { t } = useTranslation();
  return (
    <div className="shrink-0 border-b border-line px-2 py-1.5">
      <Segmented
        label={t('nav.agents')}
        value={mode}
        options={AGENTS_MODES.map((each) => ({ value: each, label: t(`nav.${each}`) }))}
        onChange={onMode}
        fill
      />
    </div>
  );
}

/**
 * The list closed to its strip: the two modes, each by its glyph and named, the one in front marked, and under them what
 * the mode's own strip holds (the agents' marks, or the five sections'). The strip stands for the head's choice, so a list closed to
 * give a page room still switches between the modes.
 */
export function AgentsStrip({ mode, onMode, children }: { mode: AgentsMode; onMode: (mode: AgentsMode) => void; children?: ReactNode }) {
  const { t } = useTranslation();
  return (
    <>
      <ul className="m-0 grid list-none justify-items-center gap-1 px-0 py-1.5">
        {AGENTS_MODES.map((each) => (
          <StripMark
            key={each}
            label={t(`nav.${each}`)}
            initialOf={t(`nav.${each}`)}
            face={<Icon name={MODE_GLYPH[each]} size={15} />}
            current={mode === each}
            onPress={() => onMode(each)}
          />
        ))}
      </ul>
      {children}
    </>
  );
}
