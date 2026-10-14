// API-обёртка для звонков.
import { apiRequest, buildApiUrl } from './apiClient';

const CALLS = '/calls';

const request = (path, accessToken, options = {}) =>
  apiRequest(buildApiUrl(CALLS, path), {
    accessToken,
    ...options,
  });

export const startCall = (token, data) =>
  request('', token, {
    method: 'POST',
    body: JSON.stringify({
      ChatId: data.chatId || null,
      Type: data.type || 'audio',
      ParticipantIds: data.participantIds,
    }),
  });

export const answerCall = (token, callId, data = {}) =>
  request(`/${callId}/answer`, token, {
    method: 'POST',
    body: JSON.stringify({
      HasVideo: data.hasVideo ?? false,
      HasAudio: data.hasAudio ?? true,
    }),
  });

export const declineCall = (token, callId) =>
  request(`/${callId}/decline`, token, { method: 'POST', body: JSON.stringify({}) });

export const endCall = (token, callId) =>
  request(`/${callId}/end`, token, { method: 'POST', body: JSON.stringify({}) });

export const leaveCall = (token, callId) =>
  request(`/${callId}/leave`, token, { method: 'POST', body: JSON.stringify({}) });

export const getCall = (token, callId) => request(`/${callId}`, token);

export const getActiveCall = (token) => request('/active', token);

export const getCallHistory = (token, limit = 50) =>
  request(`/history?limit=${limit}`, token);
