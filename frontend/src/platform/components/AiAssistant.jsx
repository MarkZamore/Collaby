import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import {
  Check,
  ChevronLeft,
  Copy,
  Loader2,
  Maximize2,
  MessageSquare,
  Minimize2,
  Pencil,
  Pin,
  PinOff,
  Plus,
  RefreshCw,
  Send,
  Sparkles,
  Trash2,
  X,
} from 'lucide-react';
import { useAuth } from '../../shared/context/AuthContext';
import { getAiFollowUps, sendAiChat } from '../../shared/utils/aiApi';
import { usePreferences } from '../context/PreferencesContext';
import { usePlatform } from '../context/PlatformContext';
import { executeAiAction } from '../utils/aiAssistantTools';

const CHATS_STORAGE_KEY = 'collaby_ai_chats';
const ACTIVE_CHAT_KEY = 'collaby_ai_active_chat';
const MAX_STORED_MESSAGES = 100;

const createId = () => `${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 8)}`;
const createChat = () => ({ id: createId(), title: '', createdAt: Date.now(), messages: [] });

const escapeHtml = (value) => String(value)
  .replace(/&/g, '&amp;')
  .replace(/</g, '&lt;')
  .replace(/>/g, '&gt;')
  .replace(/"/g, '&quot;')
  .replace(/'/g, '&#39;');

const renderInlineMarkdown = (value) => escapeHtml(value)
  .replace(/`([^`]+)`/g, '<code class="ai-inline-code">$1</code>')
  .replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>')
  .replace(/\*(.+?)\*/g, '<em>$1</em>');

const renderTextMarkdownSegment = (segment) => {
  const lines = String(segment || '').split('\n');
  const blocks = [];
  let listItems = [];

  const flushList = () => {
    if (listItems.length === 0) return;
    blocks.push(`<ul>${listItems.join('')}</ul>`);
    listItems = [];
  };

  lines.forEach((line) => {
    const trimmed = line.trim();
    if (!trimmed) {
      flushList();
      return;
    }

    const headingMatch = /^(#{1,3})\s+(.+)$/.exec(trimmed);
    if (headingMatch) {
      flushList();
      const level = Math.min(4, headingMatch[1].length + 1);
      blocks.push(`<h${level}>${renderInlineMarkdown(headingMatch[2])}</h${level}>`);
      return;
    }

    const bulletMatch = /^[-*•]\s+(.+)$/.exec(trimmed);
    if (bulletMatch) {
      listItems.push(`<li>${renderInlineMarkdown(bulletMatch[1])}</li>`);
      return;
    }

    flushList();
    blocks.push(`<p>${renderInlineMarkdown(trimmed)}</p>`);
  });

  flushList();
  return blocks.join('');
};

const renderMarkdownHtml = (text) => {
  if (!text) return '';
  const input = String(text);
  const parts = [];
  const codeBlockPattern = /```(\w*)\n([\s\S]*?)```/g;
  let lastIndex = 0;
  let match;

  while ((match = codeBlockPattern.exec(input)) !== null) {
    const precedingText = input.slice(lastIndex, match.index);
    if (precedingText) {
      parts.push(renderTextMarkdownSegment(precedingText));
    }

    parts.push(`<pre class="ai-code-block"><code>${escapeHtml(match[2])}</code></pre>`);
    lastIndex = match.index + match[0].length;
  }

  const trailingText = input.slice(lastIndex);
  if (trailingText) {
    parts.push(renderTextMarkdownSegment(trailingText));
  }

  return parts.join('');
};

const stripReplyArtifacts = (reply) => String(reply || '')
  .replace(/```json\s*[\s\S]*?```/gi, '')
  .replace(/\{\s*"name"\s*:\s*"[^"]+"\s*,\s*"arguments"\s*:\s*\{[\s\S]*?\}\s*\}/g, '')
  .replace(/\n{3,}/g, '\n\n')
  .trim();

const copyToClipboard = async (text) => {
  if (navigator.clipboard && window.isSecureContext) {
    await navigator.clipboard.writeText(text);
    return;
  }
  const textarea = document.createElement('textarea');
  textarea.value = text;
  textarea.style.position = 'fixed';
  textarea.style.left = '-9999px';
  document.body.appendChild(textarea);
  textarea.select();
  try {
    document.execCommand('copy');
  } finally {
    document.body.removeChild(textarea);
  }
};

const getChatTitle = (chat, fallbackTitle) => {
  if (chat?.title) return chat.title;
  const firstUserMessage = (chat?.messages || []).find((message) => message.role === 'user');
  const trimmed = firstUserMessage?.content?.trim();
  if (!trimmed) return fallbackTitle;
  return trimmed.length > 36 ? `${trimmed.slice(0, 36)}...` : trimmed;
};

const ResultCard = ({ result, onNavigate, t }) => (
  <div
    className="rounded-xl px-3 py-2 text-[11px]"
    style={{
      backgroundColor: result.success ? 'rgba(34,197,94,0.08)' : 'rgba(239,68,68,0.08)',
      border: `1px solid ${result.success ? 'rgba(34,197,94,0.18)' : 'rgba(239,68,68,0.18)'}`,
      color: 'var(--app-text-main)',
    }}
  >
    <div className="flex items-center gap-2">
      {result.success ? <Check size={12} className="shrink-0 text-green-500" /> : <X size={12} className="shrink-0 text-red-500" />}
      <span className="flex-1">{result.message}</span>
      {(result.navigateToProjectId || result.navigateToWorkspaceId) ? (
        <button
          type="button"
          onClick={() => onNavigate(result)}
          className="rounded-md px-2 py-1 text-[10px] font-semibold"
          style={{ backgroundColor: 'var(--app-layer-surface)', border: '1px solid var(--app-border)' }}
        >
          {t('aiAssistant.openResult')}
        </button>
      ) : null}
    </div>
  </div>
);

const AiAssistant = ({ isOpen, onClose }) => {
  const { t, i18n } = useTranslation('platform');
  const navigate = useNavigate();
  const { accessToken } = useAuth();
  const { language } = usePreferences();
  const platform = usePlatform();
  const {
    activeProject,
    activeProjectId,
    activeWorkspaceId,
    boardStickerIds,
    currentUser,
    stickerCatalog,
    users,
    workspaces,
  } = platform;
  const [isFullscreen, setIsFullscreen] = useState(false);
  const [isPinned, setIsPinned] = useState(false);
  const [showChatList, setShowChatList] = useState(false);
  const [editingChatId, setEditingChatId] = useState(null);
  const [editingTitle, setEditingTitle] = useState('');
  const [input, setInput] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [followUps, setFollowUps] = useState([]);
  const [loadingFollowUps, setLoadingFollowUps] = useState(false);
  const [copiedId, setCopiedId] = useState(null);
  const [chats, setChats] = useState(() => {
    try {
      const parsed = JSON.parse(localStorage.getItem(CHATS_STORAGE_KEY) || '[]');
      return Array.isArray(parsed) ? parsed : [];
    } catch {
      return [];
    }
  });
  const [activeChatId, setActiveChatId] = useState(() => localStorage.getItem(ACTIVE_CHAT_KEY) || null);
  const inputRef = useRef(null);
  const chatEndRef = useRef(null);
  const activeChat = useMemo(() => chats.find((chat) => chat.id === activeChatId) || null, [activeChatId, chats]);
  const fallbackChatTitle = t('aiAssistant.untitledChat');
  const messages = activeChat?.messages || [];
  const assistantLanguage = useMemo(() => {
    const key = (language || i18n.language || 'ru').slice(0, 2);
    return { ru: 'русском', en: 'English', kk: 'қазақ тілінде' }[key] || 'русском';
  }, [i18n.language, language]);
  const suggestionsRaw = t('aiAssistant.defaultSuggestions', { returnObjects: true });
  const defaultSuggestions = Array.isArray(suggestionsRaw) ? suggestionsRaw : [];
  const buildContext = useCallback(() => {
    const boardData = activeProject?.boardData || {};
    return {
      workspaceId: activeWorkspaceId,
      projectId: activeProjectId || null,
      projectName: activeProject?.name || null,
      language: assistantLanguage,
      columns: Object.values(boardData.columns || {}).map((column) => ({
        id: column.id,
        title: column.title,
        name: column.rawTitle || column.title,
        color: column.color,
        taskIds: column.taskIds || [],
        isFinal: Boolean(column.isFinal),
      })),
      columnOrder: boardData.columnOrder || [],
      tasks: Object.values(boardData.tasks || {}).map((task) => ({
        id: task.id,
        title: task.title || task.content,
        content: task.content || task.title,
        description: task.description || '',
        statusId: task.statusId,
        priority: task.priority,
        assignee: task.assignee?.name || null,
        assigneeId: task.assigneeId || null,
        labels: (task.tags || []).map((label) => ({ id: label.id, name: label.name })),
      })),
      users: (users || []).map((user) => ({ id: user.id, name: user.name })),
      labels: (activeProject?.labels || []).map((label) => ({ id: label.id, name: label.name, color: label.color })),
      currentUser: currentUser?.name || null,
      workspaces: (workspaces || []).map((workspace) => ({
        id: workspace.id,
        name: workspace.name,
        projects: (workspace.projects || []).map((project) => ({ id: project.id, name: project.name })),
      })),
      stickerCatalog: (stickerCatalog || []).map((sticker) => ({ id: sticker.id, label: sticker.label })),
      boardStickerIds: boardStickerIds || [],
    };
  }, [activeProject, activeProjectId, activeWorkspaceId, assistantLanguage, boardStickerIds, currentUser?.name, stickerCatalog, users, workspaces]);

  const createAndActivateChat = useCallback(() => {
    const chat = createChat();
    setChats((prev) => [chat, ...prev]);
    setActiveChatId(chat.id);
    return chat.id;
  }, []);

  const ensureActiveChatId = useCallback(() => activeChatId || createAndActivateChat(), [activeChatId, createAndActivateChat]);

  const updateChatMessages = useCallback((chatId, updater) => {
    setChats((prev) => prev.map((chat) => (
      chat.id === chatId
        ? { ...chat, messages: typeof updater === 'function' ? updater(chat.messages || []) : updater }
        : chat
    )));
  }, []);

  const startNewChat = useCallback(() => {
    setFollowUps([]);
    setShowChatList(false);
    createAndActivateChat();
  }, [createAndActivateChat]);

  const renameChat = useCallback((chatId) => {
    const nextTitle = editingTitle.trim();
    setChats((prev) => prev.map((item) => (item.id === chatId ? { ...item, title: nextTitle } : item)));
    setEditingChatId(null);
    setEditingTitle('');
  }, [editingTitle]);

  const deleteChat = useCallback((chatId) => {
    let nextActiveChatId = null;
    setChats((prev) => {
      const remaining = prev.filter((item) => item.id !== chatId);
      nextActiveChatId = remaining[0]?.id || null;
      return remaining;
    });
    setActiveChatId((prev) => (prev === chatId ? nextActiveChatId : prev));
    setEditingChatId((prev) => (prev === chatId ? null : prev));
    setEditingTitle('');
    setFollowUps([]);
  }, []);

  const clearAllChats = useCallback(() => {
    if (!window.confirm(t('aiAssistant.confirmClearAll'))) return;
    setChats([]);
    setActiveChatId(null);
    setEditingChatId(null);
    setEditingTitle('');
    setFollowUps([]);
  }, [t]);

  useEffect(() => {
    localStorage.setItem(CHATS_STORAGE_KEY, JSON.stringify(chats.map((chat) => ({
      ...chat,
      messages: (chat.messages || []).slice(-MAX_STORED_MESSAGES),
    }))));
  }, [chats]);

  useEffect(() => {
    if (activeChatId) localStorage.setItem(ACTIVE_CHAT_KEY, activeChatId);
    else localStorage.removeItem(ACTIVE_CHAT_KEY);
  }, [activeChatId]);

  useEffect(() => {
    if (!isOpen) return;
    if (chats.length === 0) {
      createAndActivateChat();
      return;
    }
    if (!activeChatId || !chats.some((chat) => chat.id === activeChatId)) {
      setActiveChatId(chats[0]?.id || null);
    }
  }, [activeChatId, chats, createAndActivateChat, isOpen]);

  useEffect(() => {
    chatEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [messages]);

  useEffect(() => {
    if (!isOpen) return undefined;
    const timer = window.setTimeout(() => inputRef.current?.focus(), 100);
    return () => window.clearTimeout(timer);
  }, [activeChatId, isOpen]);

  const navigateToResult = useCallback((result) => {
    if (result.navigateToWorkspaceId) platform.selectWorkspace(result.navigateToWorkspaceId);
    if (result.navigateToProjectId) platform.selectProject(result.navigateToProjectId);
    setShowChatList(false);
    navigate('/dashboard');
  }, [navigate, platform]);
  const panelWidth = isFullscreen
    ? '100%'
    : showChatList
      ? 'min(760px, calc(100vw - 2rem))'
      : 'min(420px, calc(100vw - 2rem))';
  const panelHeight = isFullscreen ? '100%' : 'min(560px, calc(100vh - 2rem))';

  const refreshFollowUps = useCallback(async (reply) => {
    if (!reply) {
      setFollowUps([]);
      return;
    }
    setLoadingFollowUps(true);
    try {
      const payload = await getAiFollowUps(accessToken, reply);
      setFollowUps(Array.isArray(payload?.followUps) ? payload.followUps : []);
    } catch {
      setFollowUps([]);
    } finally {
      setLoadingFollowUps(false);
    }
  }, [accessToken]);

  const sendMessage = useCallback(async () => {
    const text = input.trim();
    if (!text || isLoading) return;
    const targetChatId = ensureActiveChatId();
    const currentMessages = activeChat?.messages || [];
    const userMessage = { id: createId(), role: 'user', content: text, timestamp: Date.now() };

    updateChatMessages(targetChatId, [...currentMessages, userMessage]);
    setInput('');
    setFollowUps([]);
    setIsLoading(true);

    try {
      const response = await sendAiChat(accessToken, {
        message: text,
        history: currentMessages.map((message) => ({ role: message.role, content: message.content })),
        context: buildContext(),
      });

      const actionResults = [];
      for (const action of response.actions || []) {
        try {
          actionResults.push(await executeAiAction(action.action, action.params || {}, { ...platform, navigate, t }));
        } catch (error) {
          actionResults.push({ success: false, message: error.message || t('aiAssistant.results.executionFailed') });
        }
      }

      const cleanReply = stripReplyArtifacts(response.reply) || (actionResults.length ? t('aiAssistant.executing') : t('aiAssistant.noAnswer'));
      updateChatMessages(targetChatId, (prevMessages) => [
        ...prevMessages,
        { id: createId(), role: 'assistant', content: cleanReply, timestamp: Date.now(), actionResults },
      ]);

      if (Array.isArray(response.followUps) && response.followUps.length > 0) setFollowUps(response.followUps);
      else if (!actionResults.length) await refreshFollowUps(cleanReply);
    } catch (error) {
      updateChatMessages(targetChatId, (prevMessages) => [
        ...prevMessages,
        {
          id: createId(),
          role: 'assistant',
          content: `${t('aiAssistant.errorPrefix')}: ${error.message || t('aiAssistant.apiError', { status: 'unknown' })}`,
          timestamp: Date.now(),
          isError: true,
        },
      ]);
    } finally {
      setIsLoading(false);
    }
  }, [accessToken, activeChat?.messages, buildContext, ensureActiveChatId, input, isLoading, navigate, platform, refreshFollowUps, t, updateChatMessages]);

  if (!isOpen) return null;
  return createPortal(
    <div className={isFullscreen ? 'fixed inset-0 z-[1000]' : 'fixed bottom-4 right-4 z-[1000] sm:bottom-6 sm:right-6'}>
      <div
        className={`flex overflow-hidden ${isFullscreen ? 'h-full w-full rounded-none' : 'rounded-2xl'}`}
        style={{
          width: panelWidth,
          height: panelHeight,
          backgroundColor: 'var(--app-sidebar-bg)',
          border: isFullscreen ? 'none' : '1px solid var(--app-border)',
          boxShadow: '0 25px 60px rgba(0,0,0,0.3), 0 0 0 1px var(--app-border)',
        }}
      >
        {showChatList ? (
          <div
            className={`flex shrink-0 flex-col overflow-hidden ${isFullscreen ? 'max-h-[42vh] w-full border-b md:max-h-none md:w-[260px] md:border-b-0 md:border-r' : 'w-[220px] border-r'}`}
            style={{ backgroundColor: 'var(--app-layer-surface)', borderColor: 'var(--app-border)' }}
          >
            <div className="flex items-center justify-between px-3 py-3">
              <div className="text-sm font-semibold" style={{ color: 'var(--app-text-main)' }}>{t('aiAssistant.chatsTitle')}</div>
              <button type="button" onClick={() => setShowChatList(false)} className="rounded-lg p-1 hover:bg-white/5">
                <ChevronLeft size={14} style={{ color: 'var(--app-text-secondary)' }} />
              </button>
            </div>
            <div className="flex gap-2 px-3 pb-3">
              <button type="button" onClick={startNewChat} className="flex flex-1 items-center justify-center gap-1 rounded-lg px-3 py-2 text-xs font-semibold" style={{ backgroundColor: 'var(--app-accent)', color: '#fff' }}>
                <Plus size={12} />
                {t('aiAssistant.newChat')}
              </button>
              <button type="button" onClick={clearAllChats} className="rounded-lg border px-3 py-2 text-xs font-semibold" style={{ borderColor: 'var(--app-border)', color: 'var(--app-text-secondary)' }}>
                <Trash2 size={12} />
              </button>
            </div>
            <div className="custom-scrollbar min-h-0 flex-1 space-y-1 overflow-y-auto px-2 pb-3">
              {chats.map((chat) => (
                <div key={chat.id} className="rounded-xl border px-3 py-2" style={{ borderColor: chat.id === activeChatId ? 'var(--app-accent)' : 'var(--app-border)' }}>
                  {editingChatId === chat.id ? (
                    <div className="space-y-2">
                      <input
                        value={editingTitle}
                        onChange={(event) => setEditingTitle(event.target.value)}
                        onKeyDown={(event) => {
                          if (event.key === 'Enter') renameChat(chat.id);
                          if (event.key === 'Escape') {
                            setEditingChatId(null);
                            setEditingTitle('');
                          }
                        }}
                        className="w-full rounded-lg border px-2 py-1 text-xs outline-none"
                        style={{ backgroundColor: 'var(--app-content-bg)', borderColor: 'var(--app-border)', color: 'var(--app-text-main)' }}
                        autoFocus
                      />
                      <button type="button" onClick={() => renameChat(chat.id)} className="rounded-md px-2 py-1 text-[11px] font-semibold" style={{ backgroundColor: 'var(--app-accent)', color: '#fff' }}>
                        {t('aiAssistant.saveChatName')}
                      </button>
                    </div>
                  ) : (
                    <>
                      <button type="button" onClick={() => { setActiveChatId(chat.id); setShowChatList(false); setFollowUps([]); }} className="w-full text-left">
                        <div className="truncate text-sm font-medium" style={{ color: 'var(--app-text-main)' }}>{getChatTitle(chat, fallbackChatTitle)}</div>
                        <div className="mt-1 text-[11px]" style={{ color: 'var(--app-text-secondary)' }}>{t('aiAssistant.messagesCount', { count: (chat.messages || []).length })}</div>
                      </button>
                      <div className="mt-2 flex gap-2">
                        <button type="button" onClick={() => { setEditingChatId(chat.id); setEditingTitle(getChatTitle(chat, fallbackChatTitle)); }} className="flex items-center gap-1 rounded-md px-2 py-1 text-[11px] font-semibold" style={{ color: 'var(--app-text-secondary)' }}>
                          <Pencil size={11} />
                          {t('aiAssistant.renameChat')}
                        </button>
                        <button type="button" onClick={() => deleteChat(chat.id)} className="flex items-center gap-1 rounded-md px-2 py-1 text-[11px] font-semibold" style={{ color: '#ef4444' }}>
                          <Trash2 size={11} />
                          {t('aiAssistant.deleteChat')}
                        </button>
                      </div>
                    </>
                  )}
                </div>
              ))}
              {chats.length === 0 ? <div className="rounded-xl px-3 py-4 text-center text-xs" style={{ color: 'var(--app-text-secondary)' }}>{t('aiAssistant.emptyChats')}</div> : null}
            </div>
          </div>
        ) : null}
        <div className="flex min-w-0 flex-1 flex-col overflow-hidden">
          <div className="flex h-11 shrink-0 items-center justify-between border-b px-4" style={{ backgroundColor: 'var(--app-layer-surface)', borderBottomColor: 'var(--app-border)' }}>
            <div className="flex min-w-0 items-center gap-2.5">
              <div className="flex h-6 w-6 items-center justify-center rounded-lg" style={{ backgroundColor: 'var(--app-accent)' }}>
                <Sparkles size={12} className="text-white" />
              </div>
              <div className="truncate text-sm font-semibold" style={{ color: 'var(--app-text-main)' }}>{activeChat ? getChatTitle(activeChat, fallbackChatTitle) : t('aiAssistant.title')}</div>
              {isPinned ? <Pin size={11} style={{ color: 'var(--app-accent)' }} /> : null}
            </div>
            <div className="flex items-center gap-1">
              <button type="button" onClick={() => setShowChatList((prev) => !prev)} className="rounded-lg p-1.5 hover:bg-white/5" title={showChatList ? t('aiAssistant.hideChats') : t('aiAssistant.showChats')}>
                <MessageSquare size={14} style={{ color: 'var(--app-text-secondary)' }} />
              </button>
              <button type="button" onClick={startNewChat} className="rounded-lg p-1.5 hover:bg-white/5" title={t('aiAssistant.newChat')}>
                <Plus size={14} style={{ color: 'var(--app-text-secondary)' }} />
              </button>
              <button type="button" onClick={() => setIsPinned((prev) => !prev)} className="rounded-lg p-1.5 hover:bg-white/5" title={isPinned ? t('aiAssistant.unpin') : t('aiAssistant.pinOnTop')}>
                {isPinned ? <PinOff size={14} style={{ color: 'var(--app-accent)' }} /> : <Pin size={14} style={{ color: 'var(--app-text-secondary)' }} />}
              </button>
              <button type="button" onClick={() => setIsFullscreen((prev) => !prev)} className="rounded-lg p-1.5 hover:bg-white/5" title={isFullscreen ? t('aiAssistant.minimize') : t('aiAssistant.fullscreen')}>
                {isFullscreen ? <Minimize2 size={14} style={{ color: 'var(--app-text-secondary)' }} /> : <Maximize2 size={14} style={{ color: 'var(--app-text-secondary)' }} />}
              </button>
              <button type="button" onClick={onClose} className="rounded-lg p-1.5 hover:bg-red-500/10" title={t('aiAssistant.close')}>
                <X size={14} style={{ color: 'var(--app-text-secondary)' }} />
              </button>
            </div>
          </div>
          <div className="custom-scrollbar flex-1 overflow-y-auto px-4 py-3" style={{ minHeight: 0 }}>
            {messages.length === 0 ? (
              <div className="flex h-full flex-col items-center justify-center gap-4 py-8 text-center">
                <div className="flex h-14 w-14 items-center justify-center rounded-2xl" style={{ backgroundColor: 'color-mix(in srgb, var(--app-accent) 15%, transparent)' }}>
                  <Sparkles size={24} style={{ color: 'var(--app-accent)' }} />
                </div>
                <div>
                  <p className="mb-1 text-sm font-semibold" style={{ color: 'var(--app-text-main)' }}>{t('aiAssistant.welcomeTitle')}</p>
                  <p className="max-w-[260px] text-xs" style={{ color: 'var(--app-text-secondary)' }}>{t('aiAssistant.welcomeDescription')}</p>
                </div>
                <div className="flex max-w-[340px] flex-wrap justify-center gap-2">
                  {defaultSuggestions.map((suggestion, index) => (
                    <button key={`${suggestion}-${index}`} type="button" onClick={() => setInput(suggestion)} className="rounded-xl px-3 py-1.5 text-left text-[11px]" style={{ backgroundColor: 'var(--app-content-bg)', border: '1px solid var(--app-border)', color: 'var(--app-text-main)' }}>
                      <MessageSquare size={10} className="mr-1.5 inline" style={{ color: 'var(--app-accent)', verticalAlign: 'middle' }} />
                      {suggestion}
                    </button>
                  ))}
                </div>
              </div>
            ) : (
              <div className="space-y-3">
                {messages.map((message) => (
                  <div key={message.id} className={`flex ${message.role === 'user' ? 'justify-end' : 'justify-start'}`}>
                    <div className={isFullscreen ? 'max-w-full md:max-w-[78%]' : 'max-w-[88%]'}>
                      <div className={`rounded-2xl px-3.5 py-2.5 text-[13px] leading-relaxed ${message.role === 'user' ? 'rounded-br-md' : 'rounded-bl-md'}`} style={{ backgroundColor: message.role === 'user' ? 'var(--app-accent)' : message.isError ? 'rgba(239,68,68,0.12)' : 'var(--app-content-bg)', color: message.role === 'user' ? '#fff' : message.isError ? '#ef4444' : 'var(--app-text-main)', border: message.role === 'user' ? 'none' : '1px solid var(--app-border)' }}>
                        {message.role === 'assistant'
                          ? <div className="ai-assistant-markdown max-w-none text-sm leading-relaxed" dangerouslySetInnerHTML={{ __html: renderMarkdownHtml(message.content) }} />
                          : <span style={{ whiteSpace: 'pre-wrap' }}>{message.content}</span>}
                      </div>
                      {Array.isArray(message.actionResults) && message.actionResults.length > 0 ? <div className="mt-2 space-y-2">{message.actionResults.map((result, index) => <ResultCard key={`${message.id}-${index}`} result={result} onNavigate={navigateToResult} t={t} />)}</div> : null}
                      {message.role === 'assistant' && !message.isError ? (
                        <div className="mt-1 flex justify-end">
                          <button type="button" onClick={() => { copyToClipboard(message.content).then(() => { setCopiedId(message.id); window.setTimeout(() => setCopiedId(null), 1500); }); }} className="flex items-center gap-1 rounded px-1.5 py-0.5 text-[11px]" style={{ color: copiedId === message.id ? '#22c55e' : 'var(--app-text-secondary)' }}>
                            {copiedId === message.id ? <Check size={11} /> : <Copy size={11} />}
                            <span>{copiedId === message.id ? t('aiAssistant.copied') : t('aiAssistant.copy')}</span>
                          </button>
                        </div>
                      ) : null}
                    </div>
                  </div>
                ))}
                {isLoading ? <div className="flex justify-start"><div className="rounded-2xl rounded-bl-md px-4 py-3" style={{ backgroundColor: 'var(--app-content-bg)', border: '1px solid var(--app-border)' }}><div className="flex items-center gap-2"><Loader2 size={14} className="animate-spin" style={{ color: 'var(--app-accent)' }} /><span className="text-xs" style={{ color: 'var(--app-text-secondary)' }}>{t('aiAssistant.thinking')}</span></div></div></div> : null}
                <div ref={chatEndRef} />
              </div>
            )}
          </div>
          {followUps.length > 0 && !isLoading ? <div className="flex shrink-0 flex-wrap gap-1.5 px-3 pt-2">{followUps.map((followUp, index) => <button key={`${followUp}-${index}`} type="button" onClick={() => { setInput(followUp); setFollowUps([]); window.setTimeout(() => inputRef.current?.focus(), 50); }} className="max-w-full rounded-lg px-2.5 py-1 text-left text-[11px]" style={{ backgroundColor: 'var(--app-content-bg)', border: '1px solid var(--app-border)', color: 'var(--app-text-secondary)' }}>{followUp}</button>)}</div> : null}
          {loadingFollowUps ? <div className="flex shrink-0 items-center gap-1.5 px-3 pt-2 text-[10px]" style={{ color: 'var(--app-text-secondary)' }}><RefreshCw size={10} className="animate-spin" /><span>{t('aiAssistant.loadingFollowUps')}</span></div> : null}
          <div className="shrink-0 border-t px-3 pb-3 pt-2" style={{ borderTopColor: 'var(--app-border)' }}>
            <div className="flex items-end gap-2 rounded-xl px-3 py-2" style={{ backgroundColor: 'var(--app-content-bg)', border: '1px solid var(--app-border)' }}>
              <textarea ref={inputRef} value={input} onChange={(event) => setInput(event.target.value)} onKeyDown={(event) => { if (event.key === 'Enter' && !event.shiftKey) { event.preventDefault(); sendMessage(); } }} onInput={(event) => { event.target.style.height = 'auto'; event.target.style.height = `${Math.min(event.target.scrollHeight, 120)}px`; }} placeholder={t('aiAssistant.inputPlaceholder')} className="flex-1 resize-none bg-transparent text-sm outline-none placeholder-current" rows={1} style={{ minHeight: 24, maxHeight: 120, color: 'var(--app-text-main)' }} />
              <button type="button" onClick={sendMessage} disabled={isLoading || !input.trim()} className="shrink-0 rounded-lg p-2 transition-all disabled:opacity-30" style={{ backgroundColor: input.trim() ? 'var(--app-accent)' : 'transparent' }}>
                <Send size={14} className="text-white" />
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>,
    document.body,
  );
};

export default AiAssistant;
