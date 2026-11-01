import type { Meta, StoryObj } from '@storybook/react-vite';
import { CardStack } from '../design/ui';
import { chinese } from '../design/storyLanguage';
import { storyWidths } from '../design/storyWidths';
import { GitDefaults, GitIdentities, GitIdentityRow, GitOwn } from './Git';
import { GitAdd } from './GitAdd';
import { ACTS, DEPLOY, EMPTY_PAGE, PAGE, PERSONAL, UNKNOWN_HOST, WORK } from './gitFixtures';

// Settings → Git (GITAUTH1e, the git-auth design §7.1-§7.2), in the shape the shell's `TOOLS_LIST` answers as `identities`: the
// three cards with an SSH identity reached, an HTTPS one reached and an SSH one the host refused; the empty set; a host not yet
// trusted with its keys against the published ones; the add form for both ways and its refusal; the new key to give the host; and the
// person's own sign-ins. Each at the main area's three widths, and in 中文.

const none = () => {};

const Page = () => (
  <CardStack>
    <GitIdentities identities={PAGE.identities} acts={ACTS} adding={false} onAdd={none} />
    <GitDefaults identities={PAGE.identities} acts={ACTS} onError={none} />
    <GitOwn hosts={PAGE.own} knownHosts={PAGE.hosts} acts={ACTS} />
  </CardStack>
);

const meta = {
  title: 'Settings/Git',
  component: Page,
  decorators: [storyWidths.wide],
} satisfies Meta<typeof Page>;

export default meta;
type Story = StoryObj<typeof meta>;

/** The page: an SSH identity reached, an HTTPS one, an SSH one the host refused, the defaults by host, and one host on the person's own sign-ins. */
export const TheThreeCards: Story = {};

export const TheThreeCardsHalf: Story = { decorators: [storyWidths.half] };

export const TheThreeCardsNarrow: Story = { decorators: [storyWidths.narrow] };

export const TheThreeCardsChinese: Story = { decorators: [chinese] };

export const TheThreeCardsChineseNarrow: Story = { decorators: [chinese, storyWidths.narrow] };

const Empty = () => (
  <CardStack>
    <GitIdentities identities={EMPTY_PAGE.identities} acts={ACTS} adding={false} onAdd={none} />
    <GitOwn hosts={[]} knownHosts={[]} acts={ACTS} />
  </CardStack>
);

/** No identity yet: what that means, and the press that adds one. */
export const Empty_: StoryObj<typeof Empty> = { render: () => <Empty /> };

export const EmptyNarrow: StoryObj<typeof Empty> = { render: () => <Empty />, decorators: [storyWidths.narrow] };

export const EmptyChinese: StoryObj<typeof Empty> = { render: () => <Empty />, decorators: [chinese] };

const Row = ({ ...props }: Partial<Parameters<typeof GitIdentityRow>[0]> & { id?: string }) => (
  <ul className="m-0 list-none p-0"><GitIdentityRow identity={WORK} acts={ACTS} {...props} /></ul>
);

/** A test of the connection is under way. */
export const Testing: StoryObj<typeof Row> = { render: () => <Row testing /> };

/** The host does not take the key: the act that fixes it is the next press, and the row says what failed. */
export const Refused: StoryObj<typeof Row> = { render: () => <Row identity={DEPLOY} /> };

/** An HTTPS identity is tested against a repository the person names. */
export const HttpsToTest: StoryObj<typeof Row> = { render: () => <Row identity={PERSONAL} /> };

/** The host is not yet trusted: each key it offers against the one it publishes, and the press that trusts one. */
export const HostNotYetTrusted: StoryObj<typeof Row> = { render: () => <Row answer={UNKNOWN_HOST} /> };

export const HostNotYetTrustedChinese: StoryObj<typeof Row> = { render: () => <Row answer={UNKNOWN_HOST} />, decorators: [chinese] };

export const HostNotYetTrustedNarrow: StoryObj<typeof Row> = { render: () => <Row answer={UNKNOWN_HOST} />, decorators: [storyWidths.narrow] };

const Add = (props: Partial<Parameters<typeof GitAdd>[0]>) => (
  <CardStack><GitAdd acts={ACTS} knownHosts={PAGE.hosts} onClose={none} {...props} /></CardStack>
);

/** Add a git identity, SSH: the host, a name, a new key, and who commits. */
export const AddSsh: StoryObj<typeof Add> = { render: () => <Add host="github.com" /> };

export const AddSshHalf: StoryObj<typeof Add> = { render: () => <Add host="github.com" />, decorators: [storyWidths.half] };

export const AddSshNarrow: StoryObj<typeof Add> = { render: () => <Add host="github.com" />, decorators: [storyWidths.narrow] };

export const AddSshChinese: StoryObj<typeof Add> = { render: () => <Add host="github.com" />, decorators: [chinese] };

/** Azure DevOps takes RSA keys alone, and the form says so. */
export const AddSshRsaAlone: StoryObj<typeof Add> = { render: () => <Add host="dev.azure.com" /> };

/** Add a git identity, HTTPS: the account and a browser sign-in or a pasted token. */
export const AddHttps: StoryObj<typeof Add> = { render: () => <Add host="github.com" wayToday="https" /> };

export const AddHttpsNarrow: StoryObj<typeof Add> = { render: () => <Add host="github.com" wayToday="https" />, decorators: [storyWidths.narrow] };

export const AddHttpsChinese: StoryObj<typeof Add> = { render: () => <Add host="github.com" wayToday="https" />, decorators: [chinese] };

const ADDED = { identity: 'gi-00000000000d', label: 'Work', host: 'github.com', way: 'ssh' as const, publicKey: WORK.publicKey };

/** The new key, to give the host: copy it, or open the host's page for it. */
export const NewKeyToGive: StoryObj<typeof Add> = { render: () => <Add added={ADDED} /> };

export const NewKeyToGiveNarrow: StoryObj<typeof Add> = { render: () => <Add added={ADDED} />, decorators: [storyWidths.narrow] };

export const NewKeyToGiveChinese: StoryObj<typeof Add> = { render: () => <Add added={ADDED} />, decorators: [chinese] };

/** An identity saved through Git Credential Manager. */
export const HttpsAdded: StoryObj<typeof Add> = { render: () => <Add added={{ ...ADDED, way: 'https', publicKey: null }} /> };
