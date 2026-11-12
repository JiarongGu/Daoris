import { afterEach, describe, expect, it } from 'vitest';
import { cleanup, render, screen } from '@testing-library/react';
import i18n from '../../locales/i18n';
import { type PressView, PressViewLine } from '../PressViewLine';

// A room asked of a repository is drawn on the Confirm card from the view of `PLACE_APPLY` (its own kind): the repository and its workspace, the
// room's folder and the reason, the first lines of the one quest it asks, above the command the press is. Daoris writes none of the room's text.

const ROOM: PressView = {
  kind: 'place-room',
  facts: { repositories: 'engine', workspace: 'aurora', room: 'tools/web', purpose: 'the web app needs its own conventions' },
  command: 'daoris place engine --room tools/web --purpose "the web app needs its own conventions" --apply',
  also: [
    { kind: 'place-room-alone', facts: { repository: 'engine' } },
    { kind: 'place-room-quest', facts: { title: 'Add a room for tools/web', head: 'What is asked, and whose it is. Give `tools/web` a room of its own.' } },
  ],
};

describe("a room asked of a repository on the Confirm card", () => {
  afterEach(async () => {
    cleanup();
    await i18n.changeLanguage('en');
  });

  it('says whose it is, the folder and the reason, the quest it asks and the command it is', () => {
    render(<PressViewLine view={ROOM} />);

    const meaning = screen.getByText(/^Asks engine's own session \(workspace aurora\), as a quest, to give tools\/web a room of its own/);
    expect(meaning).toHaveTextContent('the web app needs its own conventions');
    expect(meaning).toHaveTextContent('Nothing is written here');
    expect(screen.getByText('engine is not driven here, so its room quest starts alone while general driving stays off.')).toBeInTheDocument();
    expect(screen.getByText(/^Asks the quest “Add a room for tools\/web”, which opens: What is asked, and whose it is\./)).toBeInTheDocument();
    expect(screen.getByText(ROOM.command)).toBeInTheDocument();
  });

  it('words it in 中文, the command as written', async () => {
    await i18n.changeLanguage('zh');
    render(<PressViewLine view={ROOM} />);

    const meaning = screen.getByText(/^以委托的形式，请 engine（工作区 aurora）自己的会话为 tools\/web 建一个自己的房间/);
    expect(meaning).toHaveTextContent('the web app needs its own conventions');
    expect(screen.getByText('engine 未被驱动，所以它的房间委托在通用驱动保持关闭时单独启动。')).toBeInTheDocument();
    expect(screen.getByText(/^发出委托「Add a room for tools\/web」，开头是：/)).toBeInTheDocument();
    expect(screen.getByText(ROOM.command)).toBeInTheDocument();
  });
});
