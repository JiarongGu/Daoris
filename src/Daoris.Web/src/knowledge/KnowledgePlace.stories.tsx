import { useState } from 'react';
import type { Meta, StoryObj } from '@storybook/react-vite';
import { useTranslation } from 'react-i18next';
import type { Convergence } from '../api';
import { useListPanes } from '../work/listPanes';
import { ViewFrame } from '../work/ViewFrame';
import { EntryPage } from './EntryPage';
import { BODIES, ENTRY, FINDINGS, HITS, RESTATEMENT } from './fixtures';
import { FindingList } from './FindingList';
import { FindingPage } from './FindingPage';
import { HitList } from './HitList';
import { KnowledgeModes, KnowledgeStrip } from './KnowledgeModes';
import type { KnowledgeMode } from './modes';
import { type EntryReading, findingId } from './records';

// The Knowledge place whole (UX6i, D150 §2.2): Search and Convergence as one place, its list pane under the two-way choice
// and the mode's page in the main area, as the window draws it, at whatever width the story is given: the design's
// 1546 px with the list open, and 680 px, where the list is a strip whose marks are the two modes. The choice switches in
// the story as it does on the window; each mode's fixtures are the stories' of its list and its page.

const nothing = () => {};

/** Every entry of a finding read, from the fixtures' bodies; one not among them is gone. */
const read = (finding: Convergence): Record<string, EntryReading> => Object.fromEntries(finding.entries.map((entry) => {
  const body = BODIES[entry.id];
  return [entry.id, body ? { state: 'read', entry: body } : { state: 'gone' }];
}));

function Place({ start }: { start: KnowledgeMode }) {
  const { t } = useTranslation();
  const lists = useListPanes();
  const [mode, setMode] = useState(start);
  const [over, setOver] = useState(false);
  const search = mode === 'search';
  const chosen = search ? ENTRY.id : findingId(RESTATEMENT);
  return (
    <div className="flex h-[52rem] w-full border border-line bg-page">
      <ViewFrame
        layout={{
          list: {
            view: 'knowledge',
            chosenIn: mode,
            name: t('nav.knowledge'),
            labels: search
              ? { open: t('search.list.open'), close: t('search.list.close'), resize: t('search.list.resize') }
              : { open: t('convergence.list.open'), close: t('convergence.list.close'), resize: t('convergence.list.resize') },
            chosen,
            head: <KnowledgeModes mode={mode} onMode={setMode} />,
            strip: <KnowledgeStrip mode={mode} onMode={setMode} />,
            body: search
              ? (
                <HitList
                  query="chunk hydration" onQuery={nothing} localOnly onLocalOnly={nothing} marked="chunk hydration" semantic={false}
                  answer={{ state: 'answered', hits: HITS, more: false, byMeaning: false, nothing: false, searching: false }}
                  chosen={chosen} onChoose={nothing} onConverge={() => setMode('convergence')}
                />
              )
              : (
                <FindingList
                  threshold={0.75} onThreshold={nothing} semantic={false}
                  answer={{ state: 'answered', findings: FINDINGS, at: 0.75, more: false, comparing: false }}
                  chosen={chosen} onChoose={nothing}
                />
              ),
          },
          main: search ? <EntryPage entry={ENTRY} /> : <FindingPage finding={RESTATEMENT} readings={read(RESTATEMENT)} />,
        }}
        lists={lists}
        over={over}
        onOver={setOver}
      />
    </div>
  );
}

const meta: Meta<typeof Place> = {
  title: 'Knowledge/KnowledgePlace',
  component: Place,
  parameters: { layout: 'fullscreen' },
};
export default meta;

/** On Search: three hits for *chunk hydration*, the first chosen and read as it is written beside them. */
export const OnSearch: StoryObj<typeof Place> = { args: { start: 'search' } };

/** On Convergence: three findings at 0.75, a drifted copy chosen, the service's sentence and both entries whole. */
export const OnConvergence: StoryObj<typeof Place> = { args: { start: 'convergence' } };
