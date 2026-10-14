import { describe, expect, it, vi } from 'vitest';

import { executeAiAction } from './aiAssistantTools';

const makeDeps = () => {
  const deps = {
    t: (key, params = {}) => params.name || params.title || params.action || key,
    navigate: vi.fn(),
    activeProjectId: 'project-1',
    activeWorkspaceId: 'workspace-1',
    activeProject: {
      name: 'Platform',
      labels: [{ id: 'label-1', name: 'Bug', color: '#ef4444' }],
      boardData: {
        columns: {
          'status-1': { id: 'status-1', title: 'Backlog', taskIds: [] },
          'status-2': { id: 'status-2', title: 'В работе', taskIds: ['task-1'] },
        },
        tasks: {
          'task-1': { id: 'task-1', title: 'Write docs', statusId: 'status-2' },
        },
      },
    },
    workspaces: [
      {
        id: 'workspace-1',
        name: 'Product team',
        projects: [{ id: 'project-1', name: 'Platform' }],
      },
    ],
    stickerCatalog: [{ id: 'priority', label: 'Priority' }],
    createWorkspace: vi.fn().mockResolvedValue('workspace-1'),
    createProject: vi.fn().mockResolvedValue({ workspaceId: 'workspace-1', projectId: 'project-1' }),
    addColumn: vi.fn().mockResolvedValue('status-3'),
    reorderColumnsToOrder: vi.fn().mockResolvedValue(undefined),
    addTask: vi.fn().mockResolvedValue(undefined),
    updateTask: vi.fn().mockResolvedValue(undefined),
    deleteTask: vi.fn().mockResolvedValue(undefined),
    moveTask: vi.fn().mockResolvedValue(undefined),
    addSubTask: vi.fn().mockResolvedValue(undefined),
    createLabel: vi.fn().mockResolvedValue({ id: 'label-1' }),
    assignLabel: vi.fn().mockResolvedValue(undefined),
    removeLabel: vi.fn().mockResolvedValue(undefined),
    deleteLabel: vi.fn().mockResolvedValue(undefined),
    addComment: vi.fn().mockResolvedValue(undefined),
    inviteMember: vi.fn().mockResolvedValue(undefined),
    changeMemberRole: vi.fn().mockResolvedValue(undefined),
    deleteProject: vi.fn().mockResolvedValue(undefined),
    selectProject: vi.fn(),
    selectWorkspace: vi.fn(),
    addBoardSticker: vi.fn().mockResolvedValue(undefined),
    removeBoardSticker: vi.fn().mockResolvedValue(undefined),
  };

  return deps;
};

describe('executeAiAction', () => {
  it('supports create_tasks alias with legacy task fields', async () => {
    const deps = makeDeps();

    const result = await executeAiAction(
      'create_tasks',
      {
        items: [{ name: 'Prepare release', status: 'В работе', priority: 'High' }],
      },
      deps,
    );

    expect(result.success).toBe(true);
    expect(deps.addTask).toHaveBeenCalledWith(
      expect.objectContaining({
        title: 'Prepare release',
        statusId: 'status-2',
        priority: 'high',
      }),
    );
  });

  it('supports add_subtask alias with legacy task_id parameter', async () => {
    const deps = makeDeps();

    const result = await executeAiAction(
      'add_subtask',
      {
        task_id: 'Write docs',
        name: 'Draft API section',
      },
      deps,
    );

    expect(result.success).toBe(true);
    expect(deps.addSubTask).toHaveBeenCalledWith('task-1', 'Draft API section');
  });

  it('resolves workspace and project by legacy aliases or names', async () => {
    const deps = makeDeps();

    const result = await executeAiAction(
      'switch_project',
      {
        project_id: 'Platform',
      },
      deps,
    );

    expect(result.success).toBe(true);
    expect(deps.selectProject).toHaveBeenCalledWith('project-1');
    expect(deps.navigate).toHaveBeenCalledWith('/dashboard');
  });

  it('resolves label references by name', async () => {
    const deps = makeDeps();

    const result = await executeAiAction(
      'assign_label',
      {
        task_id: 'Write docs',
        label: 'Bug',
      },
      deps,
    );

    expect(result.success).toBe(true);
    expect(deps.assignLabel).toHaveBeenCalledWith('task-1', 'label-1');
  });

  it('fails safely when sticker cannot be resolved', async () => {
    const deps = makeDeps();

    const result = await executeAiAction(
      'remove_board_sticker',
      {
        sticker: 'Unknown sticker',
      },
      deps,
    );

    expect(result.success).toBe(false);
    expect(deps.removeBoardSticker).not.toHaveBeenCalled();
  });
});
