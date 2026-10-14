import '@testing-library/jest-dom/vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { vi } from 'vitest';
import FirstWorkspaceModal from './FirstWorkspaceModal';

vi.mock('react-hot-toast', () => ({
  default: {
    success: vi.fn(),
    error: vi.fn(),
  },
}));

describe('FirstWorkspaceModal', () => {
  it('keeps the submit button disabled until both names are entered', async () => {
    const user = userEvent.setup();

    render(
      <FirstWorkspaceModal
        onCreateWorkspace={vi.fn()}
        isDark={false}
        onClose={vi.fn()}
      />,
    );

    const submit = screen.getByTestId('first-workspace-submit');
    expect(submit).toBeDisabled();

    await user.type(screen.getByTestId('first-workspace-name'), 'CI Workspace');
    expect(submit).toBeDisabled();

    await user.type(screen.getByTestId('first-project-name'), 'CI Project');
    expect(submit).toBeEnabled();
  });

  it('submits the workspace and project names through the callback', async () => {
    const user = userEvent.setup();
    const onCreateWorkspace = vi.fn().mockResolvedValue(undefined);
    const onClose = vi.fn();

    render(
      <FirstWorkspaceModal
        onCreateWorkspace={onCreateWorkspace}
        isDark={false}
        onClose={onClose}
      />,
    );

    await user.type(screen.getByTestId('first-workspace-name'), 'CI Workspace');
    await user.type(screen.getByTestId('first-project-name'), 'CI Project');
    await user.click(screen.getByTestId('first-workspace-submit'));

    await waitFor(() =>
      expect(onCreateWorkspace).toHaveBeenCalledWith('CI Workspace', 'CI Project'),
    );
    await waitFor(() => expect(onClose).toHaveBeenCalled());
  });
});
