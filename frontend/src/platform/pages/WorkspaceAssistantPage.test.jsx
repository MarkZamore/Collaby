import '@testing-library/jest-dom/vitest';
import React from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import WorkspaceAssistantPage from './WorkspaceAssistantPage';

const platformState = vi.hoisted(() => ({
  value: null,
}));

const authState = vi.hoisted(() => ({
  value: null,
}));

const aiApiMocks = vi.hoisted(() => ({
  getAiRuntime: vi.fn(),
  searchAssistantKnowledge: vi.fn(),
  sendAssistantChat: vi.fn(),
}));

vi.mock('../components/DashboardLayout', () => ({
  default: ({ children }) => <>{children}</>,
}));

vi.mock('../context/PlatformContext', () => ({
  usePlatform: () => platformState.value,
}));

vi.mock('../../shared/context/AuthContext', () => ({
  useAuth: () => authState.value,
}));

vi.mock('../../shared/utils/aiApi', () => aiApiMocks);

describe('WorkspaceAssistantPage quick actions', () => {
  beforeEach(() => {
    window.HTMLElement.prototype.scrollIntoView = vi.fn();

    platformState.value = {
      activeWorkspaceId: 'workspace-1',
      activeProjectId: 'project-1',
      activeProject: {
        name: 'Project Alpha',
        boardData: { tasks: {} },
      },
      inboxTasksAll: [],
      setActivePage: vi.fn(),
    };

    authState.value = {
      accessToken: 'token-1',
      currentUser: { id: 'user-1', name: 'Owner QA' },
    };

    aiApiMocks.getAiRuntime.mockReset();
    aiApiMocks.searchAssistantKnowledge.mockReset();
    aiApiMocks.sendAssistantChat.mockReset();

    aiApiMocks.getAiRuntime.mockResolvedValue({ backend: 'local-qwen-gguf' });
    aiApiMocks.searchAssistantKnowledge.mockResolvedValue({ items: [] });
    aiApiMocks.sendAssistantChat.mockResolvedValue({
      answer: 'Сконцентрируйтесь на активной задаче.',
      sources: [],
      sections: null,
    });
  });

  it('submits the first quick action immediately', async () => {
    render(<WorkspaceAssistantPage />);

    fireEvent.click(screen.getByRole('button', { name: /focus on today/i }));

    await waitFor(() => {
      expect(aiApiMocks.sendAssistantChat).toHaveBeenCalledTimes(1);
    });

    expect(aiApiMocks.sendAssistantChat).toHaveBeenCalledWith(
      'token-1',
      expect.objectContaining({
        workspaceId: 'workspace-1',
        projectId: 'project-1',
        question: 'What should I focus on today?',
        includeExternalSources: false,
      }),
    );

    await waitFor(() => {
      expect(screen.getByText('Сконцентрируйтесь на активной задаче.')).toBeInTheDocument();
    });
  });

  it('submits the typed question when clicking the send button', async () => {
    render(<WorkspaceAssistantPage />);

    const input = screen.getByPlaceholderText(/ask a question about your project/i);
    const composer = input.parentElement;
    const sendButton = composer?.querySelector('button[type="button"]');

    expect(sendButton).not.toBeNull();

    fireEvent.change(input, {
      target: { value: 'Какие следующие шаги?' },
    });

    fireEvent.click(sendButton);

    await waitFor(() => {
      expect(aiApiMocks.sendAssistantChat).toHaveBeenCalledTimes(1);
    });

    expect(aiApiMocks.sendAssistantChat).toHaveBeenCalledWith(
      'token-1',
      expect.objectContaining({
        workspaceId: 'workspace-1',
        projectId: 'project-1',
        question: 'Какие следующие шаги?',
        includeExternalSources: false,
      }),
    );
  });
});
