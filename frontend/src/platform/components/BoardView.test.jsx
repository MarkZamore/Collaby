import React from 'react';
import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import BoardView from './BoardView';
import i18n from '../../shared/i18n/config';

const platformState = vi.hoisted(() => ({
  value: null,
}));

vi.mock('../context/PlatformContext', () => ({
  usePlatform: () => platformState.value,
}));

vi.mock('./TaskCard', () => ({
  default: ({ task }) => <div>{task.title}</div>,
  TaskCard: ({ task }) => <div>{task.title}</div>,
}));

const createBoardData = () => ({
  columnOrder: ['column-1'],
  columns: {
    'column-1': {
      id: 'column-1',
      title: 'To do',
      rawTitle: 'To do',
      taskIds: ['task-1'],
      order: 0,
    },
  },
  tasks: {
    'task-1': {
      id: 'task-1',
      title: 'Task Alpha',
      status: 'To do',
    },
  },
});

describe('BoardView structure permissions', () => {
  it('hides structure management controls for members while keeping task creation', () => {
    platformState.value = {
      addColumn: vi.fn(),
      updateColumnColor: vi.fn(),
      reorderColumns: vi.fn(),
    };

    render(
      <BoardView
        data={createBoardData()}
        filteredTasks={null}
        onDragEnd={vi.fn()}
        onTaskClick={vi.fn()}
        onAddTask={vi.fn()}
        viewSettings={{}}
        boardStickers={[]}
        readOnly={false}
        canManageStructure={false}
      />,
    );

    expect(screen.queryByTitle(i18n.t('platform:board.columnMenuTitle'))).not.toBeInTheDocument();
    expect(screen.queryByText(i18n.t('platform:board.createColumn'))).not.toBeInTheDocument();
    expect(screen.getByTitle(i18n.t('platform:board.addTask'))).toBeInTheDocument();
  });

  it('shows structure management controls for owners', () => {
    platformState.value = {
      addColumn: vi.fn(),
      updateColumnColor: vi.fn(),
      reorderColumns: vi.fn(),
    };

    render(
      <BoardView
        data={createBoardData()}
        filteredTasks={null}
        onDragEnd={vi.fn()}
        onTaskClick={vi.fn()}
        onAddTask={vi.fn()}
        viewSettings={{}}
        boardStickers={[]}
        readOnly={false}
        canManageStructure
      />,
    );

    expect(screen.getByTitle(i18n.t('platform:board.columnMenuTitle'))).toBeInTheDocument();
    expect(screen.getByText(i18n.t('platform:board.createColumn'))).toBeInTheDocument();
  });
});
