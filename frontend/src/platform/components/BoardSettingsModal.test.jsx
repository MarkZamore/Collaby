import '@testing-library/jest-dom/vitest';
import React from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { SkillTreeTab } from './BoardSettingsModal';

const authState = vi.hoisted(() => ({
  accessToken: 'test-access-token',
}));

const platformState = vi.hoisted(() => ({
  activeProjectId: 'project-1',
  currentUserRole: 'Owner',
}));

const apiMocks = vi.hoisted(() => ({
  getSkillTree: vi.fn(),
  getSkillTreeConfig: vi.fn(),
  resetSkillTreeConfig: vi.fn(),
  updateSkillTreeConfig: vi.fn(),
}));

const translationState = vi.hoisted(() => ({
  t: (key, params = {}) => {
    const dictionary = {
      'boardSettings.skillTree.title': 'Project skill tree',
      'boardSettings.skillTree.description': 'Configure the six project skill tree slots that team members choose from as the project progresses.',
      'boardSettings.skillTree.projectScope': 'This configuration only affects the current project.',
      'boardSettings.skillTree.rewardJsonLabel': 'Reward JSON',
      'boardSettings.skillTree.activeLabel': 'Active',
      'boardSettings.skillTree.placeholders.title': 'Node title',
      'boardSettings.skillTree.placeholders.description': 'Short description of the reward and why a teammate would choose it',
      'boardSettings.skillTree.actions.save': 'Save tree',
      'boardSettings.skillTree.actions.saving': 'Saving tree...',
      'boardSettings.skillTree.actions.reset': 'Reset to template',
      'boardSettings.skillTree.actions.resetting': 'Resetting...',
      'boardSettings.skillTree.actions.loading': 'Loading skill tree configuration...',
      'boardSettings.skillTree.errors.forbidden': 'Only a project owner or admin can change the skill tree configuration.',
      'boardSettings.skillTree.errors.load': 'Failed to load the skill tree configuration.',
      'boardSettings.skillTree.errors.save': 'Failed to save the skill tree configuration.',
      'boardSettings.skillTree.errors.reset': 'Failed to reset the skill tree configuration.',
    };

    if (key === 'boardSettings.skillTree.tierLabel') {
      return `Tier ${params.tier}`;
    }

    if (key === 'boardSettings.skillTree.branchLabel') {
      return `Branch ${params.branch}`;
    }

    if (key === 'boardSettings.skillTree.slotHint') {
      return `Tier ${params.tier} - branch ${params.branch}`;
    }

    return dictionary[key] ?? key;
  },
}));

vi.mock('react-i18next', () => ({
  useTranslation: () => ({
    t: translationState.t,
  }),
}));

vi.mock('../../shared/context/AuthContext', () => ({
  useAuth: () => authState,
}));

vi.mock('../context/PlatformContext', () => ({
  usePlatform: () => platformState,
}));

vi.mock('../../shared/utils/gamificationApi', () => apiMocks);

describe('SkillTreeTab', () => {
  beforeEach(() => {
    platformState.currentUserRole = 'Owner';
    apiMocks.getSkillTree.mockReset();
    apiMocks.getSkillTreeConfig.mockReset();
    apiMocks.resetSkillTreeConfig.mockReset();
    apiMocks.updateSkillTreeConfig.mockReset();
  });

  it('falls back to read-only skill tree data when config access is forbidden', async () => {
    platformState.currentUserRole = 'Member';
    apiMocks.getSkillTree.mockResolvedValue([
      {
        id: 'node-1',
        tier: 1,
        branch: 'A',
        title: 'Execution Focus',
        description: 'Sharpen delivery speed for your first specialization.',
        rewardJson: '{ "title": "Executor" }',
        isActive: true,
      },
    ]);

    render(<SkillTreeTab />);

    await waitFor(() => expect(apiMocks.getSkillTree).toHaveBeenCalledWith('test-access-token', 'project-1'));

    expect(apiMocks.getSkillTreeConfig).not.toHaveBeenCalled();
    expect(screen.getByText('Only a project owner or admin can change the skill tree configuration.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Save tree' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Reset to template' })).toBeDisabled();
    expect(screen.getByDisplayValue('Execution Focus')).toBeDisabled();
    expect(screen.getByDisplayValue('Sharpen delivery speed for your first specialization.')).toBeDisabled();
  });

  it('keeps the skill tree editable for project owners when config loads successfully', async () => {
    apiMocks.getSkillTreeConfig.mockResolvedValue([
      {
        id: 'node-1',
        tier: 1,
        branch: 'A',
        title: 'Execution Focus',
        description: 'Sharpen delivery speed for your first specialization.',
        rewardJson: '{ "title": "Executor" }',
        isActive: true,
      },
    ]);

    render(<SkillTreeTab />);

    await waitFor(() => expect(apiMocks.getSkillTreeConfig).toHaveBeenCalledWith('test-access-token', 'project-1'));

    expect(apiMocks.getSkillTree).not.toHaveBeenCalled();
    expect(screen.getByRole('button', { name: 'Save tree' })).toBeEnabled();
    expect(screen.getByRole('button', { name: 'Reset to template' })).toBeEnabled();
    expect(screen.getByDisplayValue('Execution Focus')).toBeEnabled();
  });
});
