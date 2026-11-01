import type { Meta, StoryObj } from '@storybook/react-vite';
import { chinese } from '../../design/storyLanguage';
import { storyWidths } from '../../design/storyWidths';
import { InTheme } from '../../plugins/storyIcons';
import { AgentsModes } from '../AgentsModes';
import { PlaceRolesView } from './PlaceRoles';
import { usePlaceShell } from './placeShell';
import { findPlace, placeAsk, repositoryId, rowOfFound, SetupList, SetupMain, type SetupState } from './SetupPlaces';
import { AGENTS, CATALOGUE, CHAIN, PLACES } from './setupFixtures';

// The Agents place's Setup mode (AGENTSPLACE1e, the Agents-place design §3): the tree of places beside a place's head, at the widths the
// frame gives it, 80, 39 and 25 rem, in both languages and both themes.

const nothing = () => {};

const state = (fold: string | null): SetupState => ({
  search: '', onSearch: nothing, needsLook: false, onNeedsLook: nothing, fold, onFold: nothing, foldAfter: null, onFoldAfter: nothing,
  onMore: nothing, read: PLACES(fold ? { fold } : {}), status: 'ok',
});

const editor = { agents: AGENTS, offers: {}, staffing: [], catalogue: CATALOGUE, read: 'ok' as const, busy: false, onSet: nothing, onSetActor: nothing };

function Place({ chosen = repositoryId('aurora', 'engine'), fold = null }: { chosen?: string; fold?: string | null }) {
  const now = state(fold);
  const found = findPlace(now.read, chosen);
  const shell = usePlaceShell();
  const ask = found && found.kind !== 'imported' ? placeAsk(found) : null;
  return (
    <div className="flex min-h-[420px] border border-line bg-page">
      <div className="w-[280px] shrink-0 border-r border-line">
        <AgentsModes mode="setup" onMode={nothing} />
        <SetupList state={now} chosen={chosen} onChoose={nothing} />
      </div>
      <div className="min-w-0 flex-1 p-3">
        <SetupMain
          found={found}
          state="ok"
          id={chosen}
          row={rowOfFound(found, null)}
          shell={shell}
          bodies={{ roles: ask && <PlaceRolesView place={ask} read="ok" answer={CHAIN(ask)} editor={editor} appointed={null} /> }}
          onGo={nothing}
        />
      </div>
    </div>
  );
}

const meta: Meta<typeof Place> = { title: 'Agents/SetupPlaces', component: Place, decorators: [storyWidths.wide] };
export default meta;

type Story = StoryObj<typeof Place>;

/** A repository's head with its Roles (AGENTSPLACE1g1), and the workspace's imported ones folded into one row. */
export const Repository: Story = {};
export const RepositoryHalf: Story = { decorators: [storyWidths.half] };
export const RepositoryNarrow: Story = { decorators: [storyWidths.narrow] };

/** A repository that needs a look, and the fold open on a page of the imported ones. */
export const NeedsALookWithFold: Story = { args: { chosen: repositoryId('aurora', 'site'), fold: 'aurora' } };

/** The same in 中文, at each width. */
export const RepositoryChinese: Story = { decorators: [chinese] };
export const RepositoryChineseHalf: Story = { decorators: [storyWidths.half, chinese] };
export const RepositoryChineseNarrow: Story = { decorators: [storyWidths.narrow, chinese] };

/** The same in dark, at each width. */
export const RepositoryDark: Story = { render: (args) => <InTheme theme="dark"><Place {...args} /></InTheme> };
export const RepositoryDarkHalf: Story = { render: (args) => <InTheme theme="dark"><Place {...args} /></InTheme>, decorators: [storyWidths.half] };
export const RepositoryDarkNarrow: Story = { render: (args) => <InTheme theme="dark"><Place {...args} /></InTheme>, decorators: [storyWidths.narrow] };
