import type { Meta, StoryObj } from '@storybook/react-vite';
import i18n from '../i18n';
import { PlaceAccount, RenameAccount, ReSignIn } from './AddAccount';
import { accountState, type JoinChoice } from './agents';
import { READ } from './agentsFixtures';

// *Add an account…*'s steps (UX7b, the UX7 design §4.5), each a molecule drawn alone: step 1 steering a person signing an
// account back in to its row, step 3 asking the new account's name and its lists, a row's *Use in a workspace…*, and a
// rename. Step 2 is the tool's own sign-in, `SignIn`, which is an organism. The words are the catalogue's, read as each
// story renders, so a story in 中文 is in 中文.

const nothing = () => {};

/** The install's two lists a new account may join: work's own, and this machine's, which forge starts on. */
const CHOICES: JoinChoice[] = [
  { workspace: 'work', holds: ['account-1', 'account-2', 'personal'], listed: true, usedBy: [], moves: false },
  { workspace: null, holds: [], listed: false, usedBy: ['forge'], moves: true },
];

const meta: Meta = {
  title: 'Agents/AddAccount',
  decorators: [(Story) => (
    <div className="w-[52rem] max-w-full bg-page p-4">
      <Story />
    </div>
  )],
};
export default meta;

/** Step 1: account-2 reads unknown and a list holds it, so its own *Sign in* is offered before a new account. */
export const SigningBackIn: StoryObj<typeof ReSignIn> = {
  render: () => (
    <ReSignIn
      accounts={[
        { id: 'account-2', name: 'account-2', state: accountState({ login: 'unknown', read: null }, null, true), runs: 'work' },
        {
          id: 'account-5', name: 'old@example.invalid', state: accountState({ login: 'out', read: READ }, null, false),
          runs: i18n.t('agents.runs.none'),
        },
      ]}
      onSignIn={nothing}
      onNew={nothing}
      onClose={nothing}
    />
  ),
};

/** Step 3: who signed in offered as its name, a workspace's own list and this machine's, which forge starts on. */
export const NameAndLists: StoryObj<typeof PlaceAccount> = {
  render: () => (
    <PlaceAccount
      agent="claude-code"
      account="acct-5e1f0a2b"
      title={i18n.t('agents.add.title')}
      signedIn="spare@example.invalid"
      offered="spare@example.invalid"
      choices={CHOICES}
      cancelLabel={i18n.t('agents.add.notNow')}
      onDone={nothing}
      onCancel={nothing}
    />
  ),
};

/** A row's *Use in a workspace…*: the same question with no name to ask. */
export const UseInAWorkspace: StoryObj<typeof PlaceAccount> = {
  render: () => (
    <PlaceAccount
      agent="claude-code"
      account="acct-5e1f0a2b"
      title={i18n.t('agents.place.title', { account: 'spare@example.invalid' })}
      choices={CHOICES}
      cancelLabel={i18n.t('common.cancel')}
      onDone={nothing}
      onCancel={nothing}
    />
  ),
};

/** A rename under its row (ACCT2): the name in its field, and the terminal's twin. */
export const Rename: StoryObj<typeof RenameAccount> = {
  render: () => <RenameAccount agent="claude-code" account="acct-5e1f0a2b" current={null} onSave={nothing} onCancel={nothing} />,
};
