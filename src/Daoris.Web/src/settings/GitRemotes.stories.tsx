import type { Meta, StoryObj } from '@storybook/react-vite';
import { CardStack, Card, CardHeader } from '../design/ui';
import { chinese } from '../design/storyLanguage';
import { storyWidths } from '../design/storyWidths';
import { GitHosts, GitRemotes, type GitRemoteShown } from './GitRemotes';
import { DEPLOY, PERSONAL, WORK } from './gitFixtures';

// A repository's *Git remote* and a workspace's *Git* on their Setup (GITAUTH1e, the git-auth design §7.3): remotes served by
// a repository's choice, its workspace's and a host's default; one nothing serves; one on the person's own sign-ins; a local path;
// and a workspace's hosts. At the main area's three widths and in 中文.

const none = async () => ({});

const ORIGIN: GitRemoteShown = {
  name: 'origin', address: 'github.com:org-a/engine', host: 'github.com', way: 'ssh', serves: 'identity', identity: WORK.id, label: 'Work',
  source: 'workspace', passed: [], chosen: null,
};
const UPSTREAM: GitRemoteShown = {
  name: 'upstream', address: 'https://github.com/upstream/engine.git', host: 'github.com', way: 'https', serves: 'identity', identity: PERSONAL.id,
  label: 'Personal', source: 'default', passed: [], chosen: null,
};
const NOTHING: GitRemoteShown = {
  name: 'deploy', address: 'ssh.dev.azure.com:v3/org-a/engine', host: 'dev.azure.com', way: 'ssh', serves: 'none', passed: [], chosen: null,
};
const OWN: GitRemoteShown = {
  name: 'mirror', address: 'gitlab.com:org-a/engine', host: 'gitlab.com', way: 'ssh', serves: 'own', source: 'machine', passed: [], chosen: null,
};
const LOCAL: GitRemoteShown = {
  name: 'backup', address: '../engine-backup.git', host: null, way: null, serves: 'local', passed: [], chosen: null,
};
const PASSED: GitRemoteShown = {
  ...ORIGIN, chosen: 'gi-gone', source: 'default', passed: ["engine's origin points at gi-gone, which is gone"],
};

const Remotes = ({ remotes }: { remotes: GitRemoteShown[] }) => (
  <CardStack>
    <Card>
      <CardHeader title="Git remote" />
      <div className="mt-2"><GitRemotes remotes={remotes} identities={[WORK, PERSONAL, DEPLOY]} acts={{ use: none, onSetUp: () => {} }} /></div>
    </Card>
  </CardStack>
);

const meta = {
  title: 'Settings/GitRemotes',
  component: Remotes,
  args: { remotes: [ORIGIN, UPSTREAM] },
  decorators: [storyWidths.half],
} satisfies Meta<typeof Remotes>;

export default meta;
type Story = StoryObj<typeof meta>;

/** Two remotes served by a workspace's choice and a host's default. */
export const Served: Story = {};

export const ServedNarrow: Story = { decorators: [storyWidths.narrow] };

export const ServedChinese: Story = { decorators: [chinese] };

/** A remote nothing serves, said in the danger ink with the door to set one up; the person's own sign-ins marked; a local path. */
export const NothingServesIt: Story = { args: { remotes: [ORIGIN, NOTHING, OWN, LOCAL] } };

export const NothingServesItNarrow: Story = { args: { remotes: [ORIGIN, NOTHING, OWN, LOCAL] }, decorators: [storyWidths.narrow] };

export const NothingServesItChinese: Story = { args: { remotes: [ORIGIN, NOTHING, OWN, LOCAL] }, decorators: [chinese] };

/** A choice passed over, since the identity it pointed at is gone. */
export const AChoicePassedOver: Story = { args: { remotes: [PASSED] } };

const Hosts = () => (
  <CardStack>
    <Card>
      <CardHeader title="Git" />
      <div className="mt-2">
        <GitHosts
          hosts={[
            { host: 'github.com', way: 'ssh', chosen: WORK.id, remotes: ['engine: origin', 'site: origin'] },
            { host: 'github.com', way: 'https', chosen: null, remotes: ['engine: upstream'] },
            { host: 'dev.azure.com', way: 'ssh', chosen: null, remotes: ['engine: deploy'] },
          ]}
          identities={[WORK, PERSONAL]}
          acts={{ use: none, onSetUp: () => {} }}
        />
      </div>
    </Card>
  </CardStack>
);

/** A workspace's Git: a row per host and way its repositories reach, and the identity it chooses. */
export const WorkspaceHosts: StoryObj<typeof Hosts> = { render: () => <Hosts />, decorators: [storyWidths.half] };

export const WorkspaceHostsNarrow: StoryObj<typeof Hosts> = { render: () => <Hosts />, decorators: [storyWidths.narrow] };

export const WorkspaceHostsChinese: StoryObj<typeof Hosts> = { render: () => <Hosts />, decorators: [chinese, storyWidths.half] };
