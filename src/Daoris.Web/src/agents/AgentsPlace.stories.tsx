import { useState } from 'react';
import type { Meta, StoryObj } from '@storybook/react-vite';
import { useTranslation } from 'react-i18next';
import { useListPanes } from '../work/listPanes';
import { ViewFrame } from '../work/ViewFrame';
import { agentRows } from './agents';
import { AgentList, AgentStrip } from './AgentList';
import { type AgentActs, AgentPage } from './AgentPage';
import { CLAUDE_TOOL, CLAUDE_USE, CODEX_USE, RULES, TOOLS, USAGE, WORKSPACES } from './agentsFixtures';

// The Agents place whole (UX6e, D150 §5): its list pane and the chosen agent's page in the frame, as the window draws it,
// at whatever width the story is given: the design's 1546 px with the list open, and 680 px, where the list is a strip.

const nothing = () => {};
const ACTS: AgentActs = {
  onReadAgain: nothing, onSignInNew: nothing, onAddKey: nothing, onSignIn: nothing, onTryNow: nothing, onDefault: nothing,
  onRemove: nothing, onSaveSettings: nothing, onDoor: nothing, onPin: nothing,
  scope: { onOrder: nothing, onUse: nothing, onInherit: nothing },
  rules: { onSwitchDefault: nothing, onRemove: nothing, onAdd: nothing, onAnswer: nothing },
};

function Place() {
  const { t } = useTranslation();
  const lists = useListPanes();
  const [chosen, setChosen] = useState<string | null>('claude-code');
  const [over, setOver] = useState(false);
  const rows = agentRows(TOOLS, { agents: [CLAUDE_USE, CODEX_USE] }).filter((row) => row.installed);
  return (
    <div className="flex h-[52rem] w-full border border-line bg-page">
      <ViewFrame
        layout={{
          list: {
            view: 'agents',
            name: t('nav.agents'),
            labels: { open: t('agents.list.open'), close: t('agents.list.close'), resize: t('agents.list.resize') },
            chosen,
            strip: <AgentStrip rows={rows} chosen={chosen} onChoose={setChosen} />,
            body: <AgentList rows={rows} chosen={chosen} onChoose={setChosen} />,
          },
          main: (
            <AgentPage
              tool={CLAUDE_TOOL}
              use={CLAUDE_USE}
              rules={{ ...RULES, proposals: [] }}
              usage={USAGE}
              workspaces={WORKSPACES}
              adapter="claude-code"
              acts={ACTS}
            />
          ),
        }}
        lists={lists}
        over={over}
        onOver={setOver}
      />
    </div>
  );
}

const meta: Meta<typeof Place> = {
  title: 'Agents/AgentsPlace',
  component: Place,
  parameters: { layout: 'fullscreen' },
};
export default meta;

/** The design's machine: Claude Code chosen, two of its three accounts signed out, the list beside it. */
export const ClaudeCodeChosen: StoryObj<typeof Place> = {};
