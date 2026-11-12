import { afterEach, describe, expect, it } from 'vitest';
import { cleanup, render, screen } from '@testing-library/react';
import i18n from '../../locales/i18n';
import { type PressView, PressViewLine } from '../PressViewLine';

// A department opened is drawn on the Confirm card from its route's view (`REGISTRY_CREATE`): what the press means with every value it carries
// (name, workspace, the full folder, the line, the identity, that no remote is made), each step in its order and the first lines of each quest
// it publishes, above the command the press is.

const FACTS = {
  name: 'ledger', workspace: 'aurora', folder: '/work/aurora/ledger', purpose: 'keeps the books', line: 'main', author: 'Fixture <fixture@example.test>',
};

const CREATE: PressView = {
  kind: 'repository-create',
  facts: FACTS,
  command: 'daoris repository create ledger --workspace aurora --purpose "keeps the books" --work "Build the first screen."',
  also: [
    { kind: 'repository-create-no-remote', facts: {} },
    { kind: 'repository-create-step-folder', facts: { n: '1', folder: FACTS.folder } },
    { kind: 'repository-create-step-history', facts: { n: '2', line: 'main', author: FACTS.author } },
    { kind: 'repository-create-step-registration', facts: { n: '3', name: 'ledger', workspace: 'aurora' } },
    { kind: 'repository-create-step-driving', facts: { n: '4', workspace: 'aurora' } },
    { kind: 'repository-create-step-setup', facts: { n: '5', name: 'ledger' } },
    { kind: 'repository-create-quest-setup', facts: { title: 'Set up this repository for every agent (2026-11-12)', head: 'What is asked, and whose it is. Take up the shared doctrine.' } },
    { kind: 'repository-create-quest-work', facts: { title: 'Build the first screen.', head: 'Build the first screen.' } },
  ],
};

describe('a new department on the Confirm card', () => {
  afterEach(async () => {
    cleanup();
    await i18n.changeLanguage('en');
  });

  it('names every value, each step in its order, the quests it asks and the command it is', () => {
    render(<PressViewLine view={CREATE} />);

    expect(screen.getByText(/^Opens ledger, a new repository in workspace aurora, for keeps the books\./)).toHaveTextContent('/work/aurora/ledger');
    expect(screen.getByText(/^Opens ledger/)).toHaveTextContent('main');
    expect(screen.getByText(/^Opens ledger/)).toHaveTextContent('Fixture <fixture@example.test>');
    expect(screen.getByText('No git remote is made and nothing is pushed: publishing it is yours to do, later.')).toBeInTheDocument();
    expect(screen.getByText('1. Makes the folder /work/aurora/ledger.')).toBeInTheDocument();
    expect(screen.getByText('2. Runs git init on main and makes one empty commit as Fixture <fixture@example.test>.')).toBeInTheDocument();
    expect(screen.getByText('3. Registers ledger in workspace aurora as a repository, not adopted yet.')).toBeInTheDocument();
    expect(screen.getByText('4. Drives it, nothing held, under the workflow and landing aurora already follows.')).toBeInTheDocument();
    expect(screen.getByText("5. Asks ledger's own session, as a quest, to set it up.")).toBeInTheDocument();
    expect(screen.getByText(/^Asks the quest “Set up this repository for every agent \(2026-11-12\)”, which opens: What is asked/)).toBeInTheDocument();
    expect(screen.getByText(/^Then, once that set-up is done, asks the quest “Build the first screen\.”, which opens: Build the first screen\./)).toBeInTheDocument();
    expect(screen.getByText(CREATE.command)).toBeInTheDocument();
    const order = screen.getAllByText(/^\d\. /).map((each) => each.textContent?.slice(0, 2));
    expect(order).toEqual(['1.', '2.', '3.', '4.', '5.']);
  });

  it('says a drive left off for a missing remote, and a creation that goes on from an unfinished one', () => {
    const also: NonNullable<PressView['also']> = [
      { kind: 'repository-create-step-driving-off', facts: { n: '4', name: 'ledger', step: 'push' } },
      { kind: 'repository-create-resumes', facts: { plan: 'abcdef123456' } },
    ];
    render(<PressViewLine view={{ ...CREATE, also }} />);

    expect(screen.getByText('4. Leaves driving off: the workflow it follows has a push step and ledger has no git remote yet.')).toBeInTheDocument();
    expect(screen.getByText('It goes on from the unfinished creation abcdef123456: its folder already holds what that made.')).toBeInTheDocument();
  });

  it('has a sentence of its own for a folder department, with no history and no remote line', () => {
    const view: PressView = {
      kind: 'repository-create-folder',
      facts: { name: 'notes', workspace: 'aurora', folder: '/work/aurora/notes', purpose: 'loose files' },
      command: 'daoris repository create notes --kind folder --purpose "loose files"',
      also: [{ kind: 'repository-create-step-registration-folder', facts: { n: '2', name: 'notes', workspace: 'aurora' } }],
    };
    render(<PressViewLine view={view} />);

    expect(screen.getByText(/^Opens notes, a new folder department in workspace aurora, for loose files\./)).toHaveTextContent('/work/aurora/notes');
    expect(screen.getByText('2. Registers notes in workspace aurora as a folder department.')).toBeInTheDocument();
  });

  it('words every part in 中文, the command and the values as written', async () => {
    await i18n.changeLanguage('zh');
    render(<PressViewLine view={CREATE} />);

    expect(screen.getByText(/^为「keeps the books」开设新仓库 ledger（工作区 aurora）/)).toHaveTextContent('/work/aurora/ledger');
    expect(screen.getByText('不会创建远程仓库，也不会推送：何时发布由你决定，之后再按。')).toBeInTheDocument();
    expect(screen.getByText('1. 创建文件夹 /work/aurora/ledger。')).toBeInTheDocument();
    expect(screen.getByText('2. 在主线 main 上执行 git init，并以 Fixture <fixture@example.test> 的身份做一次空提交。')).toBeInTheDocument();
    expect(screen.getByText('3. 把 ledger 作为仓库注册到工作区 aurora，尚未采用。')).toBeInTheDocument();
    expect(screen.getByText('5. 以委托的形式，请 ledger 自己的会话来配置它。')).toBeInTheDocument();
    expect(screen.getByText(/^发出委托「Set up this repository for every agent \(2026-11-12\)」，开头是：What is asked/)).toBeInTheDocument();
    expect(screen.getByText(/^该配置完成后，再发出委托「Build the first screen\.」，开头是：/)).toBeInTheDocument();
    expect(screen.getByText(CREATE.command)).toBeInTheDocument();
  });
});
