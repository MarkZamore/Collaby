const ACTION_ALIASES = {
  create_tasks: 'bulk_create_tasks',
  create_columns: 'bulk_create_columns',
  add_subtask: 'create_subtask',
  add_label: 'assign_label',
  switch_workspace: 'select_workspace',
  switch_project: 'select_project',
  remove_task: 'delete_task',
  remove_project: 'delete_project',
};

const PRIORITY_ALIASES = {
  low: 'low',
  medium: 'medium',
  high: 'high',
  critical: 'critical',
  Low: 'low',
  Medium: 'medium',
  High: 'high',
  Critical: 'critical',
  normal: 'medium',
  major: 'high',
  urgent: 'critical',
};

const normalizeText = (value) => (value ? String(value).trim().replace(/\s+/g, ' ').toLowerCase() : '');
const firstDefined = (...values) => values.find((value) => value !== undefined && value !== null && value !== '');
const normalizePriority = (value) => {
  const normalized = firstDefined(value, '');
  return PRIORITY_ALIASES[normalized] || PRIORITY_ALIASES[String(normalized).toLowerCase()] || normalized;
};
const getWorkspaceRef = (params = {}) => firstDefined(params.workspaceId, params.workspace_id, params.workspace, params.name);
const getProjectRef = (params = {}) => firstDefined(params.projectId, params.project_id, params.project, params.name);
const getTaskRef = (params = {}) => firstDefined(params.taskId, params.task_id, params.parentTaskId, params.task);
const getStatusRef = (params = {}) => firstDefined(params.statusId, params.status, params.columnName, params.column_id);
const getLabelRef = (params = {}) => firstDefined(params.labelId, params.label_id, params.label);
const getStickerRef = (params = {}) => firstDefined(params.stickerId, params.sticker_id, params.sticker);
const getUserRef = (params = {}) => firstDefined(params.userId, params.user_id, params.assigneeId, params.assignee_id);
const getActionName = (params = {}, fallback) => firstDefined(params.name, params.title, fallback);
const normalizeTaskPayload = (params = {}) => ({
  title: getActionName(params),
  description: firstDefined(params.description, params.content, ''),
  statusId: getStatusRef(params),
  priority: normalizePriority(params.priority),
  dueDate: firstDefined(params.dueDate, params.due_date) || null,
  assigneeId: firstDefined(params.assigneeId, params.assignee_id) || null,
});

const resolveWorkspaceId = (workspaces, nameOrId) => {
  if (!nameOrId) return null;
  if ((workspaces || []).some((workspace) => workspace.id === nameOrId)) return nameOrId;

  const normalizedValue = normalizeText(nameOrId);
  const match = (workspaces || []).find((workspace) => {
    const name = normalizeText(workspace.name);
    return name === normalizedValue || name.includes(normalizedValue) || normalizedValue.includes(name);
  });
  return match?.id || null;
};

const resolveProjectId = (workspaces, nameOrId) => {
  if (!nameOrId) return null;

  for (const workspace of workspaces || []) {
    const projects = workspace.projects || [];
    if (projects.some((project) => project.id === nameOrId)) return nameOrId;

    const normalizedValue = normalizeText(nameOrId);
    const match = projects.find((project) => {
      const name = normalizeText(project.name);
      return name === normalizedValue || name.includes(normalizedValue) || normalizedValue.includes(name);
    });
    if (match) return match.id;
  }

  return null;
};

const resolveColumnId = (activeProject, nameOrId) => {
  if (!nameOrId) return null;
  const columns = Object.values(activeProject?.boardData?.columns || {});
  if (columns.some((column) => column.id === nameOrId)) return nameOrId;

  const normalizedValue = normalizeText(nameOrId);
  const match = columns.find((column) => {
    const title = normalizeText(column.title || column.rawTitle || '');
    return title === normalizedValue || title.includes(normalizedValue) || normalizedValue.includes(title);
  });
  return match?.id || null;
};

const resolveTaskId = (activeProject, nameOrId) => {
  if (!nameOrId) return null;
  const tasks = Object.values(activeProject?.boardData?.tasks || {});
  if (tasks.some((task) => task.id === nameOrId)) return nameOrId;

  const normalizedValue = normalizeText(nameOrId);
  const match = tasks.find((task) => {
    const title = normalizeText(task.title || task.content || '');
    return title === normalizedValue || title.includes(normalizedValue) || normalizedValue.includes(title);
  });
  return match?.id || null;
};

const resolveLabelId = (activeProject, nameOrId) => {
  if (!nameOrId) return null;
  const labels = activeProject?.labels || [];
  if (labels.some((label) => label.id === nameOrId)) return nameOrId;

  const normalizedValue = normalizeText(nameOrId);
  const match = labels.find((label) => {
    const name = normalizeText(label.name);
    return name === normalizedValue || name.includes(normalizedValue) || normalizedValue.includes(name);
  });
  return match?.id || null;
};

const resolveStickerId = (stickerCatalog, nameOrId) => {
  if (!nameOrId) return null;
  if ((stickerCatalog || []).some((sticker) => sticker.id === nameOrId)) return nameOrId;

  const normalizedValue = normalizeText(nameOrId);
  const match = (stickerCatalog || []).find((sticker) => {
    const label = normalizeText(sticker.label || '');
    const id = normalizeText(sticker.id || '');
    return label === normalizedValue || id === normalizedValue || label.includes(normalizedValue) || normalizedValue.includes(label);
  });
  return match?.id || null;
};

const buildBoardSummary = (activeProject, t) => {
  if (!activeProject?.boardData) return t('aiAssistant.boardSummaryEmpty');

  const columns = activeProject.boardData.columns || {};
  const tasksMap = activeProject.boardData.tasks || {};
  const lines = [t('aiAssistant.boardSummaryTitle', { project: activeProject.name })];

  Object.values(columns).forEach((column) => {
    const overdueCount = (column.taskIds || []).filter((taskId) => {
      const task = tasksMap[taskId];
      if (!task?.dueDate) return false;
      return new Date(task.dueDate) < new Date();
    }).length;

    lines.push(
      t('aiAssistant.boardSummaryColumn', {
        column: column.title || column.rawTitle,
        count: (column.taskIds || []).length,
        overdue: overdueCount,
      }),
    );
  });

  return lines.join('\n');
};

export const executeAiAction = async (rawAction, params, deps) => {
  const {
    t,
    navigate,
    activeProject,
    activeProjectId,
    activeWorkspaceId,
    workspaces,
    stickerCatalog,
    createWorkspace,
    createProject,
    addColumn,
    reorderColumnsToOrder,
    addTask,
    updateTask,
    deleteTask,
    moveTask,
    addSubTask,
    createLabel,
    assignLabel,
    removeLabel,
    deleteLabel,
    addComment,
    inviteMember,
    changeMemberRole,
    deleteProject,
    selectProject,
    selectWorkspace,
    addBoardSticker,
    removeBoardSticker,
  } = deps;

  const action = ACTION_ALIASES[rawAction] || rawAction;
  const priority = normalizePriority(params?.priority);

  switch (action) {
    case 'create_workspace': {
      const workspaceName = getActionName(params, 'Новый воркспейс');
      const workspaceId = await createWorkspace(workspaceName);
      return workspaceId
        ? { success: true, message: t('aiAssistant.results.workspaceCreated', { name: workspaceName }), navigateToWorkspaceId: workspaceId }
        : { success: false, message: t('aiAssistant.results.workspaceCreateFailed', { name: workspaceName }) };
    }
    case 'select_workspace': {
      const workspaceId = resolveWorkspaceId(workspaces, getWorkspaceRef(params));
      if (!workspaceId) return { success: false, message: t('aiAssistant.results.workspaceNotFound') };
      selectWorkspace(workspaceId);
      navigate('/dashboard');
      return { success: true, message: t('aiAssistant.results.workspaceSelected'), navigateToWorkspaceId: workspaceId };
    }
    case 'create_project': {
      const workspaceId = resolveWorkspaceId(workspaces, getWorkspaceRef(params)) || activeWorkspaceId;
      const projectName = getActionName(params, 'Новый проект');
      if (!workspaceId) return { success: false, message: t('aiAssistant.results.workspaceRequired') };
      const created = await createProject(workspaceId, projectName, {
        description: firstDefined(params.description, params.content, ''),
      });
      return created
        ? {
            success: true,
            message: t('aiAssistant.results.projectCreated', { name: projectName }),
            navigateToWorkspaceId: created.workspaceId,
            navigateToProjectId: created.projectId,
          }
        : { success: false, message: t('aiAssistant.results.projectCreateFailed') };
    }
    case 'select_project': {
      const projectId = resolveProjectId(workspaces, getProjectRef(params));
      if (!projectId) return { success: false, message: t('aiAssistant.results.projectNotFound') };
      selectProject(projectId);
      navigate('/dashboard');
      return { success: true, message: t('aiAssistant.results.projectSelected'), navigateToProjectId: projectId };
    }
    case 'delete_project': {
      const workspaceId = resolveWorkspaceId(workspaces, getWorkspaceRef(params)) || activeWorkspaceId;
      const projectId = resolveProjectId(workspaces, getProjectRef(params)) || activeProjectId;
      if (!workspaceId || !projectId) return { success: false, message: t('aiAssistant.results.projectRequired') };
      await deleteProject(workspaceId, projectId);
      return { success: true, message: t('aiAssistant.results.projectDeleted') };
    }
    case 'create_column': {
      const columnName = getActionName(params, 'Новая колонка');
      const statusId = await addColumn(columnName, { color: params.color, isFinal: Boolean(params.isFinal) });
      return statusId
        ? { success: true, message: t('aiAssistant.results.columnCreated', { name: columnName }) }
        : { success: false, message: t('aiAssistant.results.columnCreateFailed', { name: columnName }) };
    }
    case 'bulk_create_columns': {
      const columns = Array.isArray(params.columns) ? params.columns : Array.isArray(params.items) ? params.items : [];
      let createdCount = 0;
      for (const column of columns) {
        const statusId = await addColumn(getActionName(column), { color: column.color, isFinal: Boolean(column.isFinal) });
        if (statusId) createdCount += 1;
      }
      return {
        success: createdCount > 0,
        message: t('aiAssistant.results.columnsCreated', { created: createdCount, total: columns.length }),
      };
    }
    case 'reorder_columns': {
      const requestedOrder = (params.columnIds || params.statusIds || []).map((columnId) => resolveColumnId(activeProject, columnId)).filter(Boolean);
      if (requestedOrder.length < 2) return { success: false, message: t('aiAssistant.results.columnsReorderFailed') };
      await reorderColumnsToOrder(requestedOrder);
      return { success: true, message: t('aiAssistant.results.columnsReordered') };
    }
    case 'create_task': {
      const taskPayload = normalizeTaskPayload(params);
      await addTask({
        title: taskPayload.title || t('aiAssistant.defaults.taskTitle'),
        description: taskPayload.description || '',
        statusId: resolveColumnId(activeProject, taskPayload.statusId),
        priority: taskPayload.priority,
        dueDate: taskPayload.dueDate,
        assigneeId: taskPayload.assigneeId,
      });
      return { success: true, message: t('aiAssistant.results.taskCreated', { title: taskPayload.title || t('aiAssistant.defaults.taskTitle') }) };
    }
    case 'bulk_create_tasks': {
      const items = Array.isArray(params.tasks) ? params.tasks : Array.isArray(params.items) ? params.items : [];
      let createdCount = 0;
      for (const item of items) {
        const taskPayload = normalizeTaskPayload(item);
        await addTask({
          title: taskPayload.title || t('aiAssistant.defaults.taskTitle'),
          description: taskPayload.description || '',
          statusId: resolveColumnId(activeProject, taskPayload.statusId),
          priority: taskPayload.priority,
          dueDate: taskPayload.dueDate,
          assigneeId: taskPayload.assigneeId,
        });
        createdCount += 1;
      }
      return { success: createdCount > 0, message: t('aiAssistant.results.tasksCreated', { created: createdCount, total: items.length }) };
    }
    case 'update_task': {
      const taskId = resolveTaskId(activeProject, getTaskRef(params));
      if (!taskId) return { success: false, message: t('aiAssistant.results.taskNotFound') };
      await updateTask(taskId, {
        title: firstDefined(params.title, params.name),
        description: firstDefined(params.description, params.content),
        priority,
        dueDate: firstDefined(params.dueDate, params.due_date),
      });
      return { success: true, message: t('aiAssistant.results.taskUpdated') };
    }
    case 'delete_task': {
      const taskId = resolveTaskId(activeProject, getTaskRef(params));
      if (!taskId) return { success: false, message: t('aiAssistant.results.taskNotFound') };
      await deleteTask(taskId);
      return { success: true, message: t('aiAssistant.results.taskDeleted') };
    }
    case 'move_task': {
      const taskId = resolveTaskId(activeProject, getTaskRef(params));
      const statusId = resolveColumnId(activeProject, getStatusRef(params));
      if (!taskId || !statusId) return { success: false, message: t('aiAssistant.results.taskMoveFailed') };
      await moveTask(taskId, statusId);
      return { success: true, message: t('aiAssistant.results.taskMoved') };
    }
    case 'create_subtask': {
      const taskId = resolveTaskId(activeProject, getTaskRef(params));
      const subtaskTitle = getActionName(params, t('aiAssistant.defaults.subtaskTitle'));
      if (!taskId) return { success: false, message: t('aiAssistant.results.taskNotFound') };
      await addSubTask(taskId, subtaskTitle);
      return { success: true, message: t('aiAssistant.results.subtaskCreated', { title: subtaskTitle }) };
    }
    case 'create_label': {
      const labelName = getActionName(params, 'Новая метка');
      const label = await createLabel(labelName, params.color || '#6366F1');
      return label
        ? { success: true, message: t('aiAssistant.results.labelCreated', { name: labelName }) }
        : { success: false, message: t('aiAssistant.results.labelCreateFailed', { name: labelName }) };
    }
    case 'assign_label': {
      const taskId = resolveTaskId(activeProject, getTaskRef(params));
      const labelId = resolveLabelId(activeProject, getLabelRef(params));
      if (!taskId || !labelId) return { success: false, message: t('aiAssistant.results.labelAssignFailed') };
      await assignLabel(taskId, labelId);
      return { success: true, message: t('aiAssistant.results.labelAssigned') };
    }
    case 'remove_label': {
      const taskId = resolveTaskId(activeProject, getTaskRef(params));
      const labelId = resolveLabelId(activeProject, getLabelRef(params));
      if (!taskId || !labelId) return { success: false, message: t('aiAssistant.results.labelRemoveFailed') };
      await removeLabel(taskId, labelId);
      return { success: true, message: t('aiAssistant.results.labelRemoved') };
    }
    case 'delete_label': {
      const labelId = resolveLabelId(activeProject, getLabelRef(params));
      if (!labelId) return { success: false, message: t('aiAssistant.results.labelNotFound') };
      await deleteLabel(labelId);
      return { success: true, message: t('aiAssistant.results.labelDeleted') };
    }
    case 'add_comment': {
      const taskId = resolveTaskId(activeProject, getTaskRef(params));
      if (!taskId) return { success: false, message: t('aiAssistant.results.taskNotFound') };
      await addComment(taskId, firstDefined(params.text, params.comment, params.content, ''));
      return { success: true, message: t('aiAssistant.results.commentAdded') };
    }
    case 'invite_member': {
      const workspaceId = resolveWorkspaceId(workspaces, getWorkspaceRef(params)) || activeWorkspaceId;
      const userId = getUserRef(params);
      if (!workspaceId || !userId) return { success: false, message: t('aiAssistant.results.memberInviteFailed') };
      await inviteMember(workspaceId, userId, params.role || 'Member');
      return { success: true, message: t('aiAssistant.results.memberInvited') };
    }
    case 'update_member_role': {
      const workspaceId = resolveWorkspaceId(workspaces, getWorkspaceRef(params)) || activeWorkspaceId;
      const userId = getUserRef(params);
      if (!workspaceId || !userId || !params.role) return { success: false, message: t('aiAssistant.results.memberRoleFailed') };
      await changeMemberRole(workspaceId, userId, params.role);
      return { success: true, message: t('aiAssistant.results.memberRoleUpdated') };
    }
    case 'add_board_sticker': {
      const stickerId = resolveStickerId(stickerCatalog, getStickerRef(params));
      if (!stickerId) return { success: false, message: t('aiAssistant.results.stickerAddFailed') };
      await addBoardSticker(stickerId);
      return { success: true, message: t('aiAssistant.results.stickerAdded') };
    }
    case 'remove_board_sticker': {
      const stickerId = resolveStickerId(stickerCatalog, getStickerRef(params));
      if (!stickerId) return { success: false, message: t('aiAssistant.results.stickerRemoveFailed') };
      await removeBoardSticker(stickerId);
      return { success: true, message: t('aiAssistant.results.stickerRemoved') };
    }
    case 'get_board_summary':
      return { success: true, message: buildBoardSummary(activeProject, t) };
    default:
      return { success: false, message: t('aiAssistant.results.unsupportedAction', { action }) };
  }
};
