import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import i18n from '../locales/i18n';
import { InTheme } from '../plugins/storyIcons';
import { chinese } from '../design/storyLanguage';
import { storyWidths } from '../design/storyWidths';
import { NameAccount, RenameAccount, ReSignIn } from './AddAccount';
import { accountState } from './agents';
import { READ } from './agentsFixtures';

// *Add an account…*'s steps (UX7b, the UX7 design §4.5; POOL1c, the account-pool design §4.1), each a molecule drawn alone: step
// 1 steering a person signing an account back in to its row, step 3 saying the new account joined Daoris's accounts and asking
// its name, and a rename. Step 2 is the tool's own sign-in, `SignIn`, which is an organism. Each drawn at the main area's
// widths (the UI/UX design R6), in English and 中文, light and dark. The words are the catalogue's, read as each story renders,
// so a story in 中文 is in 中文.

const nothing = () => {};
const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;

const meta: Meta = {
  title: 'Agents/AddAccount',
  decorators: [storyWidths.wide],
};
export default meta;

/** Step 1: account-2 reads unknown and account-5 signed out, in the set's order, each with its own *Sign in*. */
export const SigningBackIn: StoryObj<typeof ReSignIn> = {
  render: () => (
    <ReSignIn
      accounts={[
        { id: 'account-2', name: 'account-2', state: accountState({ login: 'unknown', read: null }, null, true) },
        { id: 'account-5', name: 'old@example.invalid', state: accountState({ login: 'out', read: READ }, null, true) },
      ]}
      onSignIn={nothing}
      onNew={nothing}
      onClose={nothing}
    />
  ),
};

/** The same at the main area's 400 px floor, in 中文. */
export const SigningBackInNarrowChinese: StoryObj<typeof ReSignIn> = {
  ...SigningBackIn, decorators: [storyWidths.narrow, chinese],
};

/**
 * Step 3: who signed in, that it joined Daoris's accounts and every workspace's work may run on it, its name with the email
 * offered, and one press, *Done*. Nothing asks where it runs (D197 point 4).
 */
export const NameStep: StoryObj<typeof NameAccount> = {
  render: () => (
    <NameAccount
      agent="claude-code"
      account="acct-5e1f0a2b"
      title={i18n.t('agents.add.title')}
      signedIn="spare@example.invalid"
      offered="spare@example.invalid"
      fallback="spare@example.invalid"
      onDone={nothing}
    />
  ),
};

/** The same at half the main area. */
export const NameStepHalf: StoryObj<typeof NameAccount> = { ...NameStep, decorators: [storyWidths.half] };

/** The same at the 400 px floor, in dark. */
export const NameStepNarrowDark: StoryObj<typeof NameAccount> = { ...NameStep, decorators: [storyWidths.narrow, dark] };

/** The same in 中文. */
export const NameStepChinese: StoryObj<typeof NameAccount> = { ...NameStep, decorators: [chinese] };

/** The same in 中文 at the 400 px floor, in dark. */
export const NameStepNarrowChineseDark: StoryObj<typeof NameAccount> = { ...NameStep, decorators: [storyWidths.narrow, chinese, dark] };

/** A key added (ACCTUX3): *Added the API key …*, the same sentence, and its name left empty to be called by its handle. */
export const NameStepKey: StoryObj<typeof NameAccount> = {
  render: () => (
    <NameAccount
      agent="claude-code"
      account="acct-9c8d7e6f"
      title={i18n.t('harness.profile.addKey')}
      lead={i18n.t('agents.add.keyAdded', { handle: '…k3y9' })}
      offered=""
      fallback={i18n.t('harness.profile.keyName', { handle: '…k3y9' })}
      onDone={nothing}
    />
  ),
};

/** The name refused (ACCTEDIT1): the step stays open with the name typed, the driver's sentence under it, whole. */
export const NameStepRefused: StoryObj<typeof NameAccount> = {
  render: () => (
    <NameAccount
      agent="claude-code"
      account="acct-5e1f0a2b"
      title={i18n.t('agents.add.title')}
      signedIn="spare@example.invalid"
      offered="spare"
      fallback="spare@example.invalid"
      refusal={'accounts.json could not be read, so `acct-5e1f0a2b` was not renamed — writing it would lose every other '
        + 'account\'s name. Fix the file or remove it, then rename again.'}
      onDone={nothing}
    />
  ),
};

/** A rename under its row (ACCT2): the name in its field, and the terminal's twin. */
export const Rename: StoryObj<typeof RenameAccount> = {
  render: () => <RenameAccount agent="claude-code" account="acct-5e1f0a2b" current={null} onSave={nothing} onCancel={nothing} />,
};

/**
 * A name no one spelling holds in every shell (ACCTQUOTE1): `R&D` is a name, and its twin names `<name>` rather than a
 * command that runs `D` in Command Prompt.
 */
export const RenameUnspellable: StoryObj<typeof RenameAccount> = {
  render: () => <RenameAccount agent="claude-code" account="acct-5e1f0a2b" current="R&D" onSave={nothing} onCancel={nothing} />,
};

/** A rename refused (ACCTEDIT1): the name typed kept in its field, and the driver's sentence under it, whole. */
export const RenameRefused: StoryObj<typeof RenameAccount> = {
  render: () => (
    <RenameAccount
      agent="claude-code"
      account="acct-5e1f0a2b"
      current="spare"
      refusal={'accounts.json could not be read, so `acct-5e1f0a2b` was not renamed — writing it would lose every other '
        + 'account\'s name. Fix the file or remove it, then rename again.'}
      onSave={nothing}
      onCancel={nothing}
    />
  ),
};
