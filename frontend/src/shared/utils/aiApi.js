const AI_BASE = `${import.meta.env.VITE_API_BASE_URL || '/api'}/ai`;

const request = async (path, accessToken, options = {}) => {
  const headers = {
    ...(accessToken ? { Authorization: `Bearer ${accessToken}` } : {}),
    ...(options.headers || {}),
  };
  const isFormData = options.body instanceof FormData;

  if (!isFormData && !headers['Content-Type']) {
    headers['Content-Type'] = 'application/json';
  }

  const response = await fetch(`${AI_BASE}${path}`, {
    credentials: 'include',
    headers,
    ...options,
  });

  if (!response.ok) {
    const body = await response.json().catch(() => ({}));
    const message = body.error || body.message || (typeof body.detail === 'string' ? body.detail : `Request failed (${response.status})`);
    const error = new Error(message);
    error.status = response.status;
    throw error;
  }

  if (response.status === 204) return null;
  return response.json();
};

const postJson = (path, token, payload) =>
  request(path, token, {
    method: 'POST',
    body: JSON.stringify(payload),
  });

export const sendAiChat = (token, payload) =>
  postJson('/chat', token, payload);

export const getAiFollowUps = (token, lastMessage) =>
  postJson('/followups', token, { lastMessage });

export const getAiRuntime = (token) =>
  request('/runtime', token);

export const sendMentorChat = (token, payload) =>
  postJson('/mentor/chat', token, payload);

export const transcribeMentorInput = (token, payload) => {
  if (payload?.file instanceof File) {
    const form = new FormData();
    form.append('workspaceId', payload.workspaceId);
    if (payload.projectId) {
      form.append('projectId', payload.projectId);
    }
    if (payload.language) {
      form.append('language', payload.language);
    }
    if (payload.transcriptText) {
      form.append('transcriptText', payload.transcriptText);
    }
    form.append('file', payload.file);

    return request('/mentor/transcribe', token, {
      method: 'POST',
      body: form,
    });
  }

  return postJson('/mentor/transcribe', token, {
    workspaceId: payload.workspaceId,
    projectId: payload.projectId || null,
    language: payload.language || null,
    transcriptText: payload.transcriptText || '',
  });
};

export const createMentorDraft = (token, payload) =>
  postJson('/mentor/drafts', token, payload);

export const saveMentorContext = (token, payload) =>
  postJson('/mentor/context', token, payload);

export const listMentorContext = (token, params) => {
  const searchParams = new URLSearchParams({ workspaceId: params.workspaceId });
  if (params.projectId) {
    searchParams.set('projectId', params.projectId);
  }
  if (params.status) {
    searchParams.set('status', params.status);
  }

  return request(`/mentor/context?${searchParams.toString()}`, token);
};

export const sendAssistantChat = (token, payload) =>
  postJson('/assistant/chat', token, payload);

export const searchAssistantKnowledge = (token, payload) =>
  postJson('/assistant/search', token, payload);

export const syncAssistantKnowledge = (token, payload) =>
  postJson('/knowledge/sync', token, payload);
