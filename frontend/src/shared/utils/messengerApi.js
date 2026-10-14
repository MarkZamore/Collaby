// src/shared/utils/messengerApi.js
import { apiRequest, buildApiUrl } from './apiClient';

const MESSENGER = '/messenger';

const request = (path, accessToken, options = {}) =>
  apiRequest(buildApiUrl(MESSENGER, path), {
    accessToken,
    ...options,
  });

export const getChats = (token) => request('/chats', token);

export const getChat = (token, chatId) => request(`/chats/${chatId}`, token);

export const createPrivateChat = (token, participantId) =>
  request('/chats/private', token, {
    method: 'POST',
    body: JSON.stringify({ ParticipantId: participantId }),
  });

export const createProjectChat = (token, data) =>
  request('/chats/project', token, {
    method: 'POST',
    body: JSON.stringify({
      Name: data.name,
      Description: data.description || null,
      ProjectId: data.projectId,
      WorkspaceId: data.workspaceId,
      MemberIds: data.memberIds || null,
    }),
  });

export const createChannel = (token, data) =>
  request('/chats/channel', token, {
    method: 'POST',
    body: JSON.stringify({
      Name: data.name,
      Description: data.description || null,
      WorkspaceId: data.workspaceId,
      MemberIds: data.memberIds || null,
    }),
  });

export const updateChat = (token, chatId, data) =>
  request(`/chats/${chatId}`, token, {
    method: 'PUT',
    body: JSON.stringify({ Name: data.name, Description: data.description }),
  });

export const addChatMembers = (token, chatId, userIds) =>
  request(`/chats/${chatId}/members`, token, {
    method: 'POST',
    body: JSON.stringify({ UserIds: userIds }),
  });

export const removeChatMember = (token, chatId, userId) =>
  request(`/chats/${chatId}/members/${userId}`, token, {
    method: 'DELETE',
  });

export const leaveChat = (token, chatId) =>
  request(`/chats/${chatId}/leave`, token, { method: 'POST', body: JSON.stringify({}) });

export const clearChat = (token, chatId) =>
  request(`/chats/${chatId}/clear`, token, { method: 'POST', body: JSON.stringify({}) });

export const deleteChat = (token, chatId) =>
  request(`/chats/${chatId}`, token, { method: 'DELETE' });

export const markChatAsRead = (token, chatId) =>
  request(`/chats/${chatId}/read`, token, { method: 'POST', body: JSON.stringify({}) });

export const getMessages = (token, chatId, { limit = 50, before } = {}) => {
  const params = new URLSearchParams({ limit: String(limit) });
  if (before) params.set('before', before);
  return request(`/chats/${chatId}/messages?${params}`, token);
};

export const sendMessage = (token, chatId, data) => {
  const body = { Text: data.text };
  if (data.type) body.Type = data.type;
  if (data.replyToMessageId) body.ReplyToMessageId = data.replyToMessageId;
  if (data.file) body.File = data.file;

  return request(`/chats/${chatId}/messages`, token, {
    method: 'POST',
    body: JSON.stringify(body),
  });
};

export const updateMessage = (token, chatId, messageId, text) =>
  request(`/chats/${chatId}/messages/${messageId}`, token, {
    method: 'PUT',
    body: JSON.stringify({ Text: text }),
  });

export const deleteMessage = (token, chatId, messageId) =>
  request(`/chats/${chatId}/messages/${messageId}`, token, {
    method: 'DELETE',
  });

export const toggleReaction = (token, chatId, messageId, emoji) =>
  request(`/chats/${chatId}/messages/${messageId}/reactions`, token, {
    method: 'POST',
    body: JSON.stringify({ Emoji: emoji }),
  });

export const getPresenceBatch = (token, userIds) =>
  request('/presence/batch', token, {
    method: 'POST',
    body: JSON.stringify(userIds),
  });
