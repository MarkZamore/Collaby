import React from 'react';
import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import GanttView from './GanttView';

const platformState = vi.hoisted(() => ({
  value: null,
}));

vi.mock('../context/PlatformContext', () => ({
  usePlatform: () => platformState.value,
}));

vi.mock('../context/PreferencesContext', () => ({
  usePreferences: () => ({ language: 'ru' }),
}));

const createBoardData = () => ({
  columnOrder: ['column-1'],
  columns: {
    'column-1': {
      id: 'column-1',
      title: 'Готово',
      taskIds: ['task-1'],
    },
  },
  tasks: {
    'task-1': {
      id: 'task-1',
      content: 'Task with object assignee',
      assignee: {
        id: 'user-1',
        name: 'qa_20260322_0902_b',
        initials: 'QB',
      },
    },
  },
});

describe('GanttView', () => {
  it('renders task rows when assignee is an object-shaped value', () => {
    platformState.value = {
      activeProject: {
        id: 'project-1',
        name: 'Project Alpha',
      },
    };

    render(<GanttView data={createBoardData()} scale="weeks" />);

    expect(screen.getByText('Task with object assignee')).toBeInTheDocument();
    expect(screen.getByText('Q')).toBeInTheDocument();
  });
});
