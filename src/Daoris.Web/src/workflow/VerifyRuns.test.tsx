import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../locales/i18n';
import { FAILED_GATE, lineOf, NOT_NOW, RUNNING, UNKNOWN_HELD, VERIFIED, WAITING } from './lineFixtures';
import { VerifyRuns } from './VerifyRuns';

// FABRIC-PIPELINE1d (the pipeline design §4.4): a repository's verifications on its Workflow tab, each the branch and commit it judged, who
// asked it, its verdict in a check's status words and each gate as its check ran it; an unknown held for the person offers *Run again* and
// *Not now*. A molecule: every state is reached by its props.

/** The verifications' rows, in order, each its own item (a row's gates are a list of their own inside it). */
const rows = () => Array.from(screen.getByRole('list', { name: /^(Verifications|验证)$/ }).children) as HTMLElement[];

describe("a repository's verifications", () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('draws each verification with its verdict, what it judged and its gates, newest first as answered', () => {
    render(<VerifyRuns answer={lineOf(VERIFIED, FAILED_GATE, RUNNING, WAITING, NOT_NOW)} />);

    const [verified, failed, running, waiting, notNow] = rows();
    expect(verified).toHaveTextContent('verified');
    expect(verified).toHaveTextContent('feature/header at fedcba9');
    expect(verified).toHaveTextContent(/asked by you/);
    expect(within(verified).getByRole('list', { name: 'Its gates' })).toHaveTextContent('build pass · exit 0 · 42 s');
    expect(failed).toHaveTextContent('failed: web-unit');
    expect(failed).toHaveTextContent('fedcba9');
    expect(failed).not.toHaveTextContent(' at ');
    expect(failed).toHaveTextContent('web-unit fail · exit 1 · 61 s');
    expect(running).toHaveTextContent('waiting for its gates');
    expect(waiting).toHaveTextContent('waiting for its turn');
    expect(notNow).toHaveTextContent('not now');
    expect(screen.queryByRole('button', { name: 'Run again' })).toBeNull();
  });

  it("offers Run again and Not now on an unknown held for the person, and sends their word on the held attempt", async () => {
    const onHeld = vi.fn().mockResolvedValue({ done: true });
    render(<VerifyRuns answer={lineOf(UNKNOWN_HELD)} onHeld={onHeld} />);

    const [held] = rows();
    expect(held).toHaveTextContent('not known: ran past its time');
    await userEvent.click(within(held).getByRole('button', { name: 'Run again' }));
    expect(onHeld).toHaveBeenCalledWith({ unit: 'run:v-2222222222222222', node: 'verify', attempt: 1, resolution: 'again' });
    await userEvent.click(within(held).getByRole('button', { name: 'Not now' }));
    expect(onHeld).toHaveBeenLastCalledWith({ unit: 'run:v-2222222222222222', node: 'verify', attempt: 1, resolution: 'stop' });
  });

  it('says none, the read on its way and a refusal in its place', () => {
    const { unmount } = render(<VerifyRuns answer={lineOf()} />);
    expect(screen.getByText(/Nothing is verified here yet/)).toBeInTheDocument();
    unmount();

    const reading = render(<VerifyRuns reading />);
    expect(screen.getByText("Reading this repository's verifications…")).toBeInTheDocument();
    reading.unmount();

    render(<VerifyRuns refusal="Daoris's driver is still coming up." />);
    expect(screen.getByText("Daoris's driver is still coming up.")).toBeInTheDocument();
  });

  it('says its verdicts in Chinese', async () => {
    await i18n.changeLanguage('zh');
    render(<VerifyRuns answer={lineOf(VERIFIED, FAILED_GATE, UNKNOWN_HELD)} onHeld={vi.fn()} />);

    const [verified, failed, held] = rows();
    expect(verified).toHaveTextContent('已验证');
    expect(verified).toHaveTextContent('feature/header，提交 fedcba9');
    expect(failed).toHaveTextContent('失败：web-unit');
    expect(failed).toHaveTextContent('退出码 1');
    expect(held).toHaveTextContent('未知：运行超时');
    expect(within(held).getByRole('button', { name: '再运行一次' })).toBeInTheDocument();
  });
});
