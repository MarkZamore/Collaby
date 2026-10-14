// src/shared/utils/workspaceApi.js
import { apiRequest, buildApiUrl } from './apiClient';

const WORK = '/management';

const request = (path, accessToken, options = {}) =>
  apiRequest(buildApiUrl(WORK, path), {
    accessToken,
    ...options,
  });

export const getBootstrapData = (token) => request('/bootstrap', token);

export const getInbox = (token) => request('/me/inbox', token);

export const getWorkspaces = (token) => request('/workspaces', token);

export const createWorkspace = (token, data) =>
  request('/workspaces', token, {
    method: 'POST',
    body: JSON.stringify(data),
  });

export const getWorkspaceById = (token, id) => request(`/workspaces/${id}`, token);

export const deleteWorkspace = (token, id) =>
  request(`/workspaces/${id}`, token, { method: 'DELETE' });

export const getWorkspaceMembers = (token, workspaceId) =>
  request(`/workspaces/${workspaceId}/members`, token);

export const addWorkspaceMember = (token, workspaceId, data) =>
  request(`/workspaces/${workspaceId}/members`, token, {
    method: 'POST',
    body: JSON.stringify(data),
  });

export const updateMemberRole = (token, workspaceId, userId, data) =>
  request(`/workspaces/${workspaceId}/members/${userId}`, token, {
    method: 'PUT',
    body: JSON.stringify(data),
  });

export const removeMember = (token, workspaceId, userId) =>
  request(`/workspaces/${workspaceId}/members/${userId}`, token, {
    method: 'DELETE',
  });

export const createProject = (token, workspaceId, data) =>
  request(`/workspaces/${workspaceId}/projects`, token, {
    method: 'POST',
    body: JSON.stringify(data),
  });

export const getProjects = (token, workspaceId) =>
  request(`/workspaces/${workspaceId}/projects`, token);

export const getProjectById = (token, workspaceId, projectId) =>
  request(`/workspaces/${workspaceId}/projects/${projectId}`, token);

export const deleteProject = (token, projectId) =>
  request(`/projects/${projectId}`, token, { method: 'DELETE' });

export const getProjectStatuses = (token, projectId) =>
  request(`/projects/${projectId}/statuses`, token);

export const createStatus = (token, projectId, data) =>
  request(`/projects/${projectId}/statuses`, token, {
    method: 'POST',
    body: JSON.stringify(data),
  });

export const updateStatus = (token, statusId, data) =>
  request(`/statuses/${statusId}`, token, {
    method: 'PUT',
    body: JSON.stringify(data),
  });

export const deleteStatus = (token, statusId) =>
  request(`/statuses/${statusId}`, token, {
    method: 'DELETE',
  });

export const reorderStatuses = (token, projectId, statusIds) =>
  request(`/projects/${projectId}/statuses/reorder`, token, {
    method: 'PUT',
    body: JSON.stringify({ statusIds }),
  });

export const createTask = (token, projectId, data) =>
  request(`/projects/${projectId}/tasks`, token, {
    method: 'POST',
    body: JSON.stringify(data),
  });

export const getProjectTasks = (token, projectId, params = {}) => {
  const qs = new URLSearchParams();
  if (params.statusId) qs.set('statusId', params.statusId);
  if (params.assigneeId) qs.set('assigneeId', params.assigneeId);
  if (params.priority) qs.set('priority', params.priority);
  const suffix = qs.toString() ? `?${qs}` : '';
  return request(`/projects/${projectId}/tasks${suffix}`, token);
};

export const getTaskById = (token, taskId) => request(`/tasks/${taskId}`, token);

export const updateTask = (token, taskId, data) =>
  request(`/tasks/${taskId}`, token, {
    method: 'PUT',
    body: JSON.stringify(data),
  });

export const deleteTask = (token, taskId) =>
  request(`/tasks/${taskId}`, token, { method: 'DELETE' });

export const moveTask = (token, taskId, data) =>
  request(`/tasks/${taskId}/move`, token, {
    method: 'PUT',
    body: JSON.stringify(data),
  });

export const assignTask = (token, taskId, data) =>
  request(`/tasks/${taskId}/assign`, token, {
    method: 'PUT',
    body: JSON.stringify(data),
  });

export const createSubTask = (token, taskId, data) =>
  request(`/tasks/${taskId}/subtasks`, token, {
    method: 'POST',
    body: JSON.stringify(data),
  });

export const updateSubTask = (token, subtaskId, data) =>
  request(`/subtasks/${subtaskId}`, token, {
    method: 'PUT',
    body: JSON.stringify(data),
  });

export const deleteSubTask = (token, subtaskId) =>
  request(`/subtasks/${subtaskId}`, token, {
    method: 'DELETE',
  });

export const createComment = (token, taskId, data) =>
  request(`/tasks/${taskId}/comments`, token, {
    method: 'POST',
    body: JSON.stringify(data),
  });

export const updateComment = (token, commentId, data) =>
  request(`/comments/${commentId}`, token, {
    method: 'PUT',
    body: JSON.stringify(data),
  });

export const deleteComment = (token, commentId) =>
  request(`/comments/${commentId}`, token, {
    method: 'DELETE',
  });

export const getProjectLabels = (token, projectId) =>
  request(`/projects/${projectId}/labels`, token);

export const createProjectLabel = (token, projectId, data) =>
  request(`/projects/${projectId}/labels`, token, {
    method: 'POST',
    body: JSON.stringify(data),
  });

export const updateProjectLabel = (token, labelId, data) =>
  request(`/labels/${labelId}`, token, {
    method: 'PUT',
    body: JSON.stringify(data),
  });

export const deleteProjectLabel = (token, labelId) =>
  request(`/labels/${labelId}`, token, {
    method: 'DELETE',
  });

export const assignLabelToTask = (token, taskId, data) =>
  request(`/tasks/${taskId}/labels`, token, {
    method: 'POST',
    body: JSON.stringify(data),
  });

export const removeLabelFromTask = (token, taskId, labelId) =>
  request(`/tasks/${taskId}/labels/${labelId}`, token, {
    method: 'DELETE',
  });

export const updateTaskStickers = (token, taskId, stickerValues) =>
  request(`/tasks/${taskId}/stickers`, token, {
    method: 'PUT',
    body: JSON.stringify({ stickerValues }),
  });

export const updateBoardStickers = (token, projectId, boardStickerIds) =>
  request(`/projects/${projectId}/board-stickers`, token, {
    method: 'PUT',
    body: JSON.stringify({ boardStickerIds }),
  });

export const getInvitations = (token, workspaceId) =>
  request(`/Workspaces/${workspaceId}/invitations`, token);

export const createInvitation = (token, workspaceId, data) =>
  request(`/Workspaces/${workspaceId}/invitations`, token, {
    method: 'POST',
    body: JSON.stringify(data),
  });

export const revokeInvitation = (token, workspaceId, invitationId) =>
  request(`/Workspaces/${workspaceId}/invitations/${invitationId}`, token, {
    method: 'DELETE',
  });

export const getInvitationByToken = (token, inviteToken) =>
  request(`/invitations/${inviteToken}`, token);

export const acceptInvitation = (token, inviteToken) =>
  request('/invitations/accept', token, {
    method: 'POST',
    body: JSON.stringify({ token: inviteToken }),
  });

export const searchBackgroundImages = (token, query, perPage = 12, page = 1) =>
  request(
    `/images/search?q=${encodeURIComponent(query)}&perPage=${Math.min(30, Math.max(1, perPage))}&page=${Math.max(1, page)}`,
    token,
  );

export const getNotifications = (token, limit = 50, offset = 0) =>
  request(`/notifications?limit=${limit}&offset=${offset}`, token);

export const getUnreadCount = (token) => request('/notifications/unread-count', token);

export const markNotificationRead = (token, notificationId) =>
  request(`/notifications/${notificationId}/read`, token, { method: 'PUT' });

export const markAllNotificationsRead = (token) =>
  request('/notifications/read-all', token, { method: 'PUT' });

export const setNotificationAction = (token, notificationId, status) =>
  request(`/notifications/${notificationId}/action`, token, {
    method: 'PUT',
    body: JSON.stringify({ status }),
  });
