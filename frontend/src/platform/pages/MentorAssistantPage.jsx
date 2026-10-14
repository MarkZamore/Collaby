import React, { useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  Bot,
  BookOpen,
  Check,
  CheckCircle2,
  ChevronDown,
  ChevronRight,
  FileAudio,
  FileText,
  MessageSquare,
  Mic,
  RefreshCw,
  Save,
  Send,
  ShieldCheck,
  Upload,
} from 'lucide-react';
import DashboardLayout from '../components/DashboardLayout';
import { usePlatform } from '../context/PlatformContext';
import { useAuth } from '../../shared/context/AuthContext';
import {
  createMentorDraft,
  getAiRuntime,
  listMentorContext,
  saveMentorContext,
  sendMentorChat,
  syncAssistantKnowledge,
  transcribeMentorInput,
} from '../../shared/utils/aiApi';

const createSessionId = () => {
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
    return crypto.randomUUID();
  }
  return `${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 10)}`;
};

const formatDate = (value) => {
  if (!value) return '';
  try {
    return new Date(value).toLocaleDateString(undefined, { day: 'numeric', month: 'short', year: 'numeric' });
  } catch {
    return value;
  }
};

/* ── Toast / notification banner ── */
const Toast = ({ message, type = 'info' }) => {
  if (!message) return null;
  const bg = type === 'error'
    ? 'color-mix(in srgb, var(--color-error, #ef4444) 12%, transparent)'
    : type === 'success'
      ? 'color-mix(in srgb, var(--color-success, #22c55e) 12%, transparent)'
      : 'var(--app-layer-base)';
  const color = type === 'error'
    ? 'var(--color-error, #ef4444)'
    : type === 'success'
      ? 'var(--color-success, #22c55e)'
      : 'var(--app-text-main)';

  return (
    <div
      className="flex items-center gap-2 rounded-xl px-4 py-2.5 text-sm"
      style={{ backgroundColor: bg, color, border: '1px solid currentColor', borderColor: `color-mix(in srgb, ${color} 25%, transparent)` }}
    >
      {type === 'success' ? <CheckCircle2 size={14} /> : type === 'error' ? <ShieldCheck size={14} /> : null}
      {message}
    </div>
  );
};

/* ── Tab bar ── */
const TabBar = ({ tabs, active, onChange }) => (
  <div className="flex gap-1 rounded-xl p-1" style={{ backgroundColor: 'var(--app-layer-base)' }}>
    {tabs.map((tab) => (
      <button
        key={tab.id}
        type="button"
        onClick={() => onChange(tab.id)}
        className="flex items-center gap-2 rounded-lg px-4 py-2 text-sm font-semibold transition-all"
        style={{
          backgroundColor: active === tab.id ? 'var(--app-layer-surface)' : 'transparent',
          color: active === tab.id ? 'var(--app-text-main)' : 'var(--app-text-secondary)',
          boxShadow: active === tab.id ? '0 1px 4px rgba(0,0,0,0.06)' : 'none',
        }}
      >
        <tab.icon size={14} />
        {tab.label}
      </button>
    ))}
  </div>
);

/* ── Chat bubble ── */
const ChatBubble = ({ item }) => {
  const isUser = item.role === 'user';
  return (
    <div className={`flex ${isUser ? 'justify-end' : 'justify-start'}`}>
      {!isUser && (
        <div
          className="mr-2.5 mt-1 flex h-8 w-8 shrink-0 items-center justify-center rounded-xl"
          style={{ background: 'var(--gradient-orange)', boxShadow: '0 2px 8px rgba(255,107,53,0.18)' }}
        >
          <Bot size={15} className="text-white" />
        </div>
      )}
      <div className={`max-w-[80%] ${isUser ? '' : 'flex-1'}`}>
        <div
          className={`rounded-2xl px-4 py-3 text-sm leading-relaxed ${isUser ? 'rounded-br-md' : 'rounded-bl-md'}`}
          style={{
            backgroundColor: isUser ? 'var(--app-accent)' : 'var(--app-layer-surface)',
            color: isUser ? 'var(--app-accent-text)' : 'var(--app-text-main)',
            border: isUser ? 'none' : '1px solid var(--app-border)',
          }}
        >
          <div className="whitespace-pre-wrap">{item.content}</div>
        </div>
        {item.sources?.length > 0 && (
          <div className="mt-2 space-y-1">
            {item.sources.map((s, i) => (
              <div key={i} className="flex items-center gap-1.5 rounded-lg px-2 py-1 text-xs"
                style={{ backgroundColor: 'var(--app-layer-base)', color: 'var(--app-text-secondary)' }}>
                <FileText size={10} />
                <span className="truncate">{s.title || s.urlOrRef}</span>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
};

/* ── Context entry row ── */
const ContextRow = ({ item }) => {
  const [expanded, setExpanded] = useState(false);

  return (
    <div
      className="rounded-xl border transition-all"
      style={{ backgroundColor: 'var(--app-layer-surface)', borderColor: 'var(--app-border)' }}
    >
      <button
        type="button"
        onClick={() => setExpanded((p) => !p)}
        className="flex w-full items-center gap-3 px-4 py-3 text-left"
      >
        {expanded ? <ChevronDown size={14} style={{ color: 'var(--app-text-secondary)' }} /> : <ChevronRight size={14} style={{ color: 'var(--app-text-secondary)' }} />}
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2">
            <span className="truncate text-sm font-semibold" style={{ color: 'var(--app-text-main)' }}>
              {item.title}
            </span>
            <span
              className="shrink-0 rounded-md px-1.5 py-0.5 text-[10px] font-bold uppercase"
              style={{
                backgroundColor: 'color-mix(in srgb, var(--app-accent) 12%, transparent)',
                color: 'var(--app-accent)',
              }}
            >
              {item.kind}
            </span>
          </div>
          <div className="mt-0.5 flex items-center gap-3 text-xs" style={{ color: 'var(--app-text-secondary)' }}>
            {item.visibility && <span>{item.visibility}</span>}
            {item.updated_at && <span>{formatDate(item.updated_at)}</span>}
          </div>
        </div>
      </button>
      {expanded && item.content && (
        <div className="border-t px-4 py-3 text-sm leading-relaxed"
          style={{ borderTopColor: 'var(--app-border)', color: 'var(--app-text-secondary)' }}>
          <div className="whitespace-pre-wrap">{item.content}</div>
        </div>
      )}
    </div>
  );
};

/* ── Main content ── */
const MentorAssistantPageContent = () => {
  const { t } = useTranslation('platform');
  const { accessToken, currentUser } = useAuth();
  const {
    activeWorkspaceId,
    activeProjectId,
    activeProject,
    currentUserRole,
    setActivePage,
  } = usePlatform();
  const sessionIdRef = useRef(createSessionId());
  const chatEndRef = useRef(null);

  const [activeTab, setActiveTab] = useState('chat');
  const [messages, setMessages] = useState([]);
  const [mentorInput, setMentorInput] = useState('');
  const [mentorPending, setMentorPending] = useState(false);
  const [syncPending, setSyncPending] = useState(false);
  const [contextItems, setContextItems] = useState([]);
  const [transcriptText, setTranscriptText] = useState('');
  const [transcriptId, setTranscriptId] = useState(null);
  const [transcriptPending, setTranscriptPending] = useState(false);
  const [selectedAudioFile, setSelectedAudioFile] = useState(null);
  const [draftTitle, setDraftTitle] = useState('');
  const [draftMarkdown, setDraftMarkdown] = useState('');
  const [draftPending, setDraftPending] = useState(false);
  const [contextTitle, setContextTitle] = useState('');
  const [contextTaskId, setContextTaskId] = useState('');
  const [contextSavePending, setContextSavePending] = useState(false);
  const [toastMessage, setToastMessage] = useState('');
  const [toastType, setToastType] = useState('info');

  const canUseMentor = useMemo(
    () => Boolean(currentUser?.isSystemAdmin || currentUserRole === 'Owner' || currentUserRole === 'Admin'),
    [currentUser?.isSystemAdmin, currentUserRole],
  );

  const taskOptions = useMemo(
    () => Object.values(activeProject?.boardData?.tasks || {}).map((task) => ({
      id: task.id,
      title: task.title || task.content || task.id,
    })),
    [activeProject],
  );

  const showToast = (msg, type = 'info') => {
    setToastMessage(msg);
    setToastType(type);
    setTimeout(() => setToastMessage(''), 4000);
  };

  useEffect(() => { setActivePage('mentor'); }, [setActivePage]);
  useEffect(() => { getAiRuntime(accessToken).catch(() => {}); }, [accessToken]);
  useEffect(() => { chatEndRef.current?.scrollIntoView({ behavior: 'smooth' }); }, [messages]);

  const loadContext = async () => {
    if (!accessToken || !activeWorkspaceId) { setContextItems([]); return; }
    try {
      const response = await listMentorContext(accessToken, {
        workspaceId: activeWorkspaceId,
        projectId: activeProjectId,
        status: 'approved',
      });
      setContextItems(response.items || []);
    } catch {
      setContextItems([]);
    }
  };

  useEffect(() => { loadContext(); }, [accessToken, activeWorkspaceId, activeProjectId]);

  const handleSendMessage = async () => {
    const text = mentorInput.trim();
    if (!text || !accessToken || !activeWorkspaceId) return;

    const history = messages.map((m) => ({ role: m.role, content: m.content }));
    setMessages((prev) => [...prev, { id: `${Date.now()}-user`, role: 'user', content: text }]);
    setMentorInput('');
    setMentorPending(true);

    try {
      const response = await sendMentorChat(accessToken, {
        workspaceId: activeWorkspaceId,
        projectId: activeProjectId,
        message: text,
        history,
        sessionId: sessionIdRef.current,
      });
      setMessages((prev) => [...prev, {
        id: `${Date.now()}-assistant`,
        role: 'assistant',
        content: response.reply,
        sources: response.sources || [],
      }]);
    } catch (error) {
      setMessages((prev) => [...prev, {
        id: `${Date.now()}-error`,
        role: 'assistant',
        content: `Error: ${error.message || 'Failed to get response'}`,
      }]);
    } finally {
      setMentorPending(false);
    }
  };

  const handleSync = async () => {
    if (!accessToken || !activeWorkspaceId || !activeProjectId) {
      showToast(t('aiAssistant.mentor.syncNeedProject', { defaultValue: 'Select a project to sync.' }), 'error');
      return;
    }
    setSyncPending(true);
    try {
      const response = await syncAssistantKnowledge(accessToken, {
        workspaceId: activeWorkspaceId,
        projectId: activeProjectId,
      });
      showToast(t('aiAssistant.mentor.syncDone', { count: response.savedCount || 0, defaultValue: `Synced ${response.savedCount || 0} items.` }), 'success');
      await loadContext();
    } catch (error) {
      showToast(error.message || 'Sync failed', 'error');
    } finally {
      setSyncPending(false);
    }
  };

  const handleTranscript = async () => {
    if (!accessToken || !activeWorkspaceId) return;
    if (!transcriptText.trim() && !selectedAudioFile) {
      showToast(t('aiAssistant.mentor.needInput', { defaultValue: 'Provide text or audio file.' }), 'error');
      return;
    }
    setTranscriptPending(true);
    try {
      const response = await transcribeMentorInput(accessToken, {
        workspaceId: activeWorkspaceId,
        projectId: activeProjectId,
        language: 'ru',
        transcriptText,
        file: selectedAudioFile,
      });
      setTranscriptId(response.transcriptId || null);
      setTranscriptText(response.transcriptText || '');
      showToast(t('aiAssistant.mentor.transcriptSaved', { defaultValue: 'Transcript saved.' }), 'success');
    } catch (error) {
      showToast(error.message || 'Transcription failed', 'error');
    } finally {
      setTranscriptPending(false);
    }
  };

  const handleBuildDraft = async () => {
    if (!accessToken || !activeWorkspaceId) return;
    setDraftPending(true);
    try {
      const response = await createMentorDraft(accessToken, {
        workspaceId: activeWorkspaceId,
        projectId: activeProjectId,
        transcriptId,
        transcriptText,
        title: draftTitle || null,
      });
      setDraftTitle(response.title || '');
      setDraftMarkdown(response.draftMarkdown || '');
      setContextTitle(response.title || '');
      showToast(t('aiAssistant.mentor.draftReady', { defaultValue: 'Draft summary ready for review.' }), 'success');
    } catch (error) {
      showToast(error.message || 'Draft generation failed', 'error');
    } finally {
      setDraftPending(false);
    }
  };

  const handleSaveContext = async () => {
    if (!accessToken || !activeWorkspaceId) return;
    if (!contextTitle.trim() || !draftMarkdown.trim()) {
      showToast(t('aiAssistant.mentor.needTitleAndContent', { defaultValue: 'Title and content are required.' }), 'error');
      return;
    }
    setContextSavePending(true);
    try {
      await saveMentorContext(accessToken, {
        workspaceId: activeWorkspaceId,
        projectId: activeProjectId,
        taskId: contextTaskId || null,
        title: contextTitle.trim(),
        content: draftMarkdown.trim(),
        kind: 'approved-summary',
        status: 'approved',
        visibility: contextTaskId ? 'task-context' : 'hidden',
        sourceRef: transcriptId ? `transcript:${transcriptId}` : null,
      });
      showToast(t('aiAssistant.mentor.contextSaved', { defaultValue: 'Context approved and saved.' }), 'success');
      await loadContext();
    } catch (error) {
      showToast(error.message || 'Failed to save context', 'error');
    } finally {
      setContextSavePending(false);
    }
  };

  const tabs = [
    { id: 'chat', icon: MessageSquare, label: t('aiAssistant.mentor.tabChat', { defaultValue: 'Chat' }) },
    { id: 'transcribe', icon: Mic, label: t('aiAssistant.mentor.tabTranscribe', { defaultValue: 'Transcribe' }) },
    { id: 'context', icon: BookOpen, label: t('aiAssistant.mentor.tabContext', { defaultValue: 'Knowledge base' }) },
  ];

  /* ── Guard: no workspace ── */
  if (!activeWorkspaceId) {
    return (
      <div className="flex flex-1 items-center justify-center p-8">
        <div className="max-w-md text-center">
          <div className="mx-auto mb-5 flex h-16 w-16 items-center justify-center rounded-2xl"
            style={{ background: 'var(--gradient-orange)', boxShadow: '0 6px 20px rgba(255,107,53,0.2)' }}>
            <ShieldCheck size={28} className="text-white" />
          </div>
          <h1 className="text-xl font-bold" style={{ color: 'var(--app-text-main)' }}>
            {t('aiAssistant.mentor.title', { defaultValue: 'Mentor Context' })}
          </h1>
          <p className="mt-2 text-sm" style={{ color: 'var(--app-text-secondary)' }}>
            {t('aiAssistant.mentor.selectWorkspace', { defaultValue: 'Open a workspace first to manage project knowledge.' })}
          </p>
        </div>
      </div>
    );
  }

  /* ── Guard: no permission ── */
  if (!canUseMentor) {
    return (
      <div className="flex flex-1 items-center justify-center p-8">
        <div className="max-w-md text-center">
          <div className="mx-auto mb-5 flex h-16 w-16 items-center justify-center rounded-2xl"
            style={{ backgroundColor: 'var(--app-layer-base)', border: '1px solid var(--app-border)' }}>
            <ShieldCheck size={28} style={{ color: 'var(--app-text-secondary)' }} />
          </div>
          <h1 className="text-xl font-bold" style={{ color: 'var(--app-text-main)' }}>
            {t('aiAssistant.mentor.accessTitle', { defaultValue: 'Restricted access' })}
          </h1>
          <p className="mt-2 text-sm" style={{ color: 'var(--app-text-secondary)' }}>
            {t('aiAssistant.mentor.accessDescription', { defaultValue: 'Only workspace owners and admins can manage mentor context.' })}
          </p>
        </div>
      </div>
    );
  }

  return (
    <main className="flex flex-1 flex-col overflow-hidden">
      {/* ── Header ── */}
      <div
        className="flex flex-col gap-3 border-b px-4 py-3 sm:flex-row sm:items-center sm:justify-between sm:gap-4 sm:px-6 sm:py-4"
        style={{ borderBottomColor: 'var(--app-border)', backgroundColor: 'var(--app-layer-surface)' }}
      >
        <div className="flex items-center gap-3 min-w-0">
          <div
            className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl"
            style={{ background: 'var(--gradient-orange)', boxShadow: '0 3px 10px rgba(255,107,53,0.2)' }}
          >
            <ShieldCheck size={18} className="text-white" />
          </div>
          <div className="min-w-0">
            <h1 className="text-lg font-bold" style={{ color: 'var(--app-text-main)' }}>
              {t('aiAssistant.mentor.title', { defaultValue: 'Mentor Context' })}
            </h1>
            <p className="truncate text-xs" style={{ color: 'var(--app-text-secondary)' }}>
              {activeProject?.name || t('aiAssistant.mentor.workspaceWideLabel', { defaultValue: 'Workspace-wide' })}
              {contextItems.length > 0 && ` \u00b7 ${t('aiAssistant.mentor.entriesCount', { count: contextItems.length, defaultValue: '{{count}} knowledge entries' })}`}
            </p>
          </div>
        </div>

        <div className="flex items-center gap-2">
          <TabBar tabs={tabs} active={activeTab} onChange={setActiveTab} />
          <button
            type="button"
            onClick={handleSync}
            disabled={syncPending}
            className="ml-2 flex items-center gap-2 rounded-xl px-4 py-2 text-xs font-bold disabled:opacity-50"
            style={{ backgroundColor: 'var(--app-accent)', color: 'var(--app-accent-text)' }}
          >
            <RefreshCw size={13} className={syncPending ? 'animate-spin' : ''} />
            {t('aiAssistant.mentor.syncAction', { defaultValue: 'Sync project' })}
          </button>
        </div>
      </div>

      {/* Toast */}
      {toastMessage && (
        <div className="px-6 pt-3">
          <Toast message={toastMessage} type={toastType} />
        </div>
      )}

      {/* ── Tab content ── */}
      <div className="custom-scrollbar flex-1 overflow-y-auto px-6 py-5" style={{ minHeight: 0 }}>

        {/* ── CHAT TAB ── */}
        {activeTab === 'chat' && (
          <div className="mx-auto flex max-w-3xl flex-col" style={{ minHeight: '100%' }}>
            <div className="flex-1 space-y-4">
              {messages.length === 0 && (
                <div className="flex flex-col items-center justify-center gap-4 py-16 text-center">
                  <div className="flex h-16 w-16 items-center justify-center rounded-2xl"
                    style={{ background: 'color-mix(in srgb, var(--app-accent) 10%, transparent)', border: '1px solid color-mix(in srgb, var(--app-accent) 15%, transparent)' }}>
                    <MessageSquare size={28} style={{ color: 'var(--app-accent)' }} />
                  </div>
                  <div>
                    <p className="text-base font-bold" style={{ color: 'var(--app-text-main)' }}>
                      {t('aiAssistant.mentor.chatEmptyTitle', { defaultValue: 'Discuss project context' })}
                    </p>
                    <p className="mt-1 max-w-sm text-sm" style={{ color: 'var(--app-text-secondary)' }}>
                      {t('aiAssistant.mentor.chatEmptyDescription', { defaultValue: 'Ask about project status, risks, or team agreements before creating summaries.' })}
                    </p>
                  </div>
                </div>
              )}
              {messages.map((m) => <ChatBubble key={m.id} item={m} />)}
              {mentorPending && (
                <div className="flex justify-start">
                  <div className="ml-[42px] flex items-center gap-2 rounded-2xl rounded-bl-md border px-4 py-3"
                    style={{ backgroundColor: 'var(--app-layer-surface)', borderColor: 'var(--app-border)' }}>
                    <RefreshCw size={13} className="animate-spin" style={{ color: 'var(--app-accent)' }} />
                    <span className="text-xs" style={{ color: 'var(--app-text-secondary)' }}>
                      {t('aiAssistant.mentor.thinking', { defaultValue: 'Thinking...' })}
                    </span>
                  </div>
                </div>
              )}
              <div ref={chatEndRef} />
            </div>

            {/* Chat input */}
            <div className="mt-4 shrink-0">
              <div
                className="flex items-end gap-3 rounded-2xl border px-4 py-3"
                style={{ backgroundColor: 'var(--app-layer-surface)', borderColor: 'var(--app-border)' }}
              >
                <textarea
                  value={mentorInput}
                  onChange={(e) => setMentorInput(e.target.value)}
                  onKeyDown={(e) => { if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); handleSendMessage(); } }}
                  onInput={(e) => { e.target.style.height = 'auto'; e.target.style.height = `${Math.min(e.target.scrollHeight, 140)}px`; }}
                  placeholder={t('aiAssistant.mentor.chatPlaceholder', { defaultValue: 'Discuss project context, risks, decisions...' })}
                  className="flex-1 resize-none bg-transparent text-sm outline-none placeholder-current"
                  rows={1}
                  style={{ minHeight: 24, maxHeight: 140, color: 'var(--app-text-main)' }}
                />
                <button
                  type="button"
                  onClick={handleSendMessage}
                  disabled={mentorPending || !mentorInput.trim()}
                  className="shrink-0 rounded-xl p-2.5 transition-all disabled:opacity-30"
                  style={{
                    background: mentorInput.trim() ? 'var(--gradient-orange)' : 'transparent',
                    boxShadow: mentorInput.trim() ? '0 2px 8px rgba(255,107,53,0.2)' : 'none',
                  }}
                >
                  <Send size={16} className="text-white" />
                </button>
              </div>
            </div>
          </div>
        )}

        {/* ── TRANSCRIBE TAB ── */}
        {activeTab === 'transcribe' && (
          <div className="mx-auto max-w-3xl space-y-6">
            {/* Step 1: Input */}
            <section>
              <div className="mb-3 flex items-center gap-2">
                <div className="flex h-6 w-6 items-center justify-center rounded-lg text-xs font-bold text-white"
                  style={{ background: 'var(--gradient-orange)' }}>1</div>
                <h2 className="text-sm font-bold" style={{ color: 'var(--app-text-main)' }}>
                  {t('aiAssistant.mentor.step1Title', { defaultValue: 'Add notes or audio' })}
                </h2>
              </div>
              <textarea
                value={transcriptText}
                onChange={(e) => setTranscriptText(e.target.value)}
                placeholder={t('aiAssistant.mentor.transcriptPlaceholder', { defaultValue: 'Paste meeting notes, call transcript, or key decisions...' })}
                className="min-h-[160px] w-full rounded-2xl border px-4 py-3 text-sm outline-none"
                style={{ backgroundColor: 'var(--app-layer-surface)', borderColor: 'var(--app-border)', color: 'var(--app-text-main)' }}
              />
              <label
                className="mt-3 flex cursor-pointer items-center gap-3 rounded-xl border border-dashed px-4 py-3 transition-colors hover:border-solid"
                style={{ borderColor: 'var(--app-border)', color: 'var(--app-text-main)' }}
              >
                <div className="flex h-9 w-9 items-center justify-center rounded-lg"
                  style={{ backgroundColor: 'color-mix(in srgb, var(--app-accent) 12%, transparent)' }}>
                  {selectedAudioFile ? <FileAudio size={16} style={{ color: 'var(--app-accent)' }} /> : <Upload size={16} style={{ color: 'var(--app-accent)' }} />}
                </div>
                <div className="flex-1">
                  <span className="text-sm font-medium">{selectedAudioFile?.name || t('aiAssistant.mentor.uploadAudio', { defaultValue: 'Upload audio file' })}</span>
                  <p className="text-xs" style={{ color: 'var(--app-text-secondary)' }}>
                    {t('aiAssistant.mentor.audioHint', { defaultValue: 'Supports common audio formats for automatic transcription' })}
                  </p>
                </div>
                <input type="file" accept="audio/*" className="hidden" onChange={(e) => setSelectedAudioFile(e.target.files?.[0] || null)} />
              </label>
              <div className="mt-3 flex gap-2">
                <button
                  type="button"
                  onClick={handleTranscript}
                  disabled={transcriptPending}
                  className="flex items-center gap-2 rounded-xl px-4 py-2.5 text-sm font-bold disabled:opacity-50"
                  style={{ backgroundColor: 'var(--app-accent)', color: 'var(--app-accent-text)' }}
                >
                  {transcriptPending ? <RefreshCw size={14} className="animate-spin" /> : <Mic size={14} />}
                  {t('aiAssistant.mentor.saveTranscript', { defaultValue: 'Save transcript' })}
                </button>
              </div>
            </section>

            {/* Step 2: Generate draft */}
            <section>
              <div className="mb-3 flex items-center gap-2">
                <div className="flex h-6 w-6 items-center justify-center rounded-lg text-xs font-bold text-white"
                  style={{ background: 'var(--gradient-orange)' }}>2</div>
                <h2 className="text-sm font-bold" style={{ color: 'var(--app-text-main)' }}>
                  {t('aiAssistant.mentor.step2Title', { defaultValue: 'Generate summary' })}
                </h2>
              </div>
              <div className="flex items-center gap-3">
                <input
                  type="text"
                  value={draftTitle}
                  onChange={(e) => { setDraftTitle(e.target.value); setContextTitle(e.target.value); }}
                  placeholder={t('aiAssistant.mentor.summaryTitle', { defaultValue: 'Summary title (optional)' })}
                  className="flex-1 rounded-xl border px-4 py-2.5 text-sm outline-none"
                  style={{ backgroundColor: 'var(--app-layer-surface)', borderColor: 'var(--app-border)', color: 'var(--app-text-main)' }}
                />
                <button
                  type="button"
                  onClick={handleBuildDraft}
                  disabled={draftPending || (!transcriptText.trim() && !transcriptId)}
                  className="flex items-center gap-2 rounded-xl px-4 py-2.5 text-sm font-bold disabled:opacity-50"
                  style={{ backgroundColor: 'var(--app-accent)', color: 'var(--app-accent-text)' }}
                >
                  {draftPending ? <RefreshCw size={14} className="animate-spin" /> : <BookOpen size={14} />}
                  {t('aiAssistant.mentor.generateDraft', { defaultValue: 'Generate' })}
                </button>
              </div>
            </section>

            {/* Step 3: Review & approve */}
            <section>
              <div className="mb-3 flex items-center gap-2">
                <div className="flex h-6 w-6 items-center justify-center rounded-lg text-xs font-bold text-white"
                  style={{ background: 'var(--gradient-orange)' }}>3</div>
                <h2 className="text-sm font-bold" style={{ color: 'var(--app-text-main)' }}>
                  {t('aiAssistant.mentor.step3Title', { defaultValue: 'Review & approve' })}
                </h2>
              </div>
              <input
                type="text"
                value={contextTitle || draftTitle}
                onChange={(e) => { setContextTitle(e.target.value); setDraftTitle(e.target.value); }}
                placeholder={t('aiAssistant.mentor.summaryTitle', { defaultValue: 'Summary title' })}
                className="mb-3 w-full rounded-xl border px-4 py-2.5 text-sm outline-none"
                style={{ backgroundColor: 'var(--app-layer-surface)', borderColor: 'var(--app-border)', color: 'var(--app-text-main)' }}
              />
              <select
                value={contextTaskId}
                onChange={(e) => setContextTaskId(e.target.value)}
                className="mb-3 w-full rounded-xl border px-4 py-2.5 text-sm outline-none"
                style={{ backgroundColor: 'var(--app-layer-surface)', borderColor: 'var(--app-border)', color: 'var(--app-text-main)' }}
              >
                <option value="">{t('aiAssistant.mentor.workspaceWide', { defaultValue: 'Workspace-wide context' })}</option>
                {taskOptions.map((task) => (
                  <option key={task.id} value={task.id}>{task.title}</option>
                ))}
              </select>
              <textarea
                value={draftMarkdown}
                onChange={(e) => setDraftMarkdown(e.target.value)}
                placeholder={t('aiAssistant.mentor.draftPlaceholder', { defaultValue: 'Generated summary will appear here for editing...' })}
                className="min-h-[200px] w-full rounded-2xl border px-4 py-3 text-sm outline-none"
                style={{ backgroundColor: 'var(--app-layer-surface)', borderColor: 'var(--app-border)', color: 'var(--app-text-main)' }}
              />
              <div className="mt-3 flex items-center justify-between">
                <p className="flex items-center gap-1.5 text-xs" style={{ color: 'var(--app-text-secondary)' }}>
                  <Check size={12} />
                  {t('aiAssistant.mentor.approveHint', { defaultValue: 'Once approved, this becomes part of the project knowledge base.' })}
                </p>
                <button
                  type="button"
                  onClick={handleSaveContext}
                  disabled={contextSavePending}
                  className="flex items-center gap-2 rounded-xl px-5 py-2.5 text-sm font-bold disabled:opacity-50"
                  style={{
                    background: 'var(--gradient-orange)',
                    color: 'white',
                    boxShadow: '0 3px 12px rgba(255,107,53,0.2)',
                  }}
                >
                  {contextSavePending ? <RefreshCw size={14} className="animate-spin" /> : <Save size={14} />}
                  {t('aiAssistant.mentor.approveContext', { defaultValue: 'Approve & save' })}
                </button>
              </div>
            </section>
          </div>
        )}

        {/* ── CONTEXT TAB ── */}
        {activeTab === 'context' && (
          <div className="mx-auto max-w-3xl">
            <div className="mb-4 flex items-center justify-between">
              <div>
                <h2 className="text-base font-bold" style={{ color: 'var(--app-text-main)' }}>
                  {t('aiAssistant.mentor.contextTitle', { defaultValue: 'Approved knowledge' })}
                </h2>
                <p className="text-xs" style={{ color: 'var(--app-text-secondary)' }}>
                  {t('aiAssistant.mentor.contextSubtitle', { defaultValue: 'These entries are used by the AI assistant when answering team questions.' })}
                </p>
              </div>
              <button
                type="button"
                onClick={loadContext}
                className="flex items-center gap-2 rounded-xl border px-3 py-2 text-xs font-bold"
                style={{ borderColor: 'var(--app-border)', color: 'var(--app-text-secondary)' }}
              >
                <RefreshCw size={12} />
                {t('aiAssistant.mentor.refreshContext', { defaultValue: 'Refresh' })}
              </button>
            </div>

            {contextItems.length === 0 ? (
              <div className="flex flex-col items-center gap-3 py-12 text-center">
                <div className="flex h-14 w-14 items-center justify-center rounded-2xl"
                  style={{ backgroundColor: 'var(--app-layer-base)', border: '1px solid var(--app-border)' }}>
                  <BookOpen size={22} style={{ color: 'var(--app-text-secondary)' }} />
                </div>
                <p className="text-sm" style={{ color: 'var(--app-text-secondary)' }}>
                  {t('aiAssistant.mentor.contextEmpty', { defaultValue: 'No approved knowledge entries yet. Sync a project or create a summary to get started.' })}
                </p>
              </div>
            ) : (
              <div className="space-y-2">
                {contextItems.map((item, i) => (
                  <ContextRow key={item.id || `${item.title}-${i}`} item={item} />
                ))}
              </div>
            )}
          </div>
        )}
      </div>
    </main>
  );
};

const MentorAssistantPage = () => (
  <DashboardLayout>
    <MentorAssistantPageContent />
  </DashboardLayout>
);

export default MentorAssistantPage;
