import React, { useState, useRef, useEffect, useCallback, useMemo } from 'react';
import {
  Phone, Video, Send, Reply, ArrowDown, PhoneOff, PhoneMissed,
  AlertCircle, X, Hash, Pencil, Trash2, Check, CheckCheck,
  Search, Forward, Pin, PinOff, AtSign, ChevronUp, ChevronDown,
} from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';

import { useMessenger } from '../../context/MessengerContext';
import { usePlatform } from '../../context/PlatformContext';
import { usePreferences } from '../../context/PreferencesContext';
import { cn } from '../../../shared/utils/cn';
import { formatTime } from '../../../shared/utils/locale';

// ─── Mention patterns ─────────────────────────────────────────────
const TASK_MENTION_PATTERN = /@\[([^:]+):([^\]]+)\]/;
const USER_MENTION_PATTERN = /@\{([^:]+):([^}]+)\}/;
const ANY_MENTION_PATTERN = new RegExp(
  `${TASK_MENTION_PATTERN.source}|${USER_MENTION_PATTERN.source}`,
);

function parseMentions(text) {
  if (!text) return [{ type: 'text', raw: '' }];
  const parts = [];
  let lastIndex = 0;
  const regex = new RegExp(ANY_MENTION_PATTERN.source, 'g');
  let match;

  while ((match = regex.exec(text)) !== null) {
    if (match.index > lastIndex) {
      parts.push({ type: 'text', raw: text.slice(lastIndex, match.index) });
    }
    if (match[1] != null) {
      parts.push({ type: 'task-mention', taskId: match[1], taskTitle: match[2] });
    } else {
      parts.push({ type: 'user-mention', userId: match[3], userName: match[4] });
    }
    lastIndex = regex.lastIndex;
  }

  if (lastIndex < text.length) {
    parts.push({ type: 'text', raw: text.slice(lastIndex) });
  }
  return parts.length > 0 ? parts : [{ type: 'text', raw: text }];
}

// ─── URL detection ─────────────────────────────────────────────────
const URL_PATTERN = /(https?:\/\/[^\s<]+)/;

function renderTextWithLinks(raw) {
  if (!raw) return raw;
  const segments = [];
  let lastIdx = 0;
  let match;
  const regex = new RegExp(URL_PATTERN.source, 'g');

  while ((match = regex.exec(raw)) !== null) {
    if (match.index > lastIdx) segments.push(raw.slice(lastIdx, match.index));
    const url = match[1];
    segments.push(
      <a key={match.index} href={url} target="_blank" rel="noopener noreferrer"
        className="underline hover:opacity-80 transition-opacity" style={{ color: 'inherit' }}
        onClick={(e) => e.stopPropagation()}>{url}</a>,
    );
    lastIdx = regex.lastIndex;
  }
  if (lastIdx < raw.length) segments.push(raw.slice(lastIdx));
  return segments.length > 0 ? segments : raw;
}

// ─── Mention Chips ────────────────────────────────────────────────
const TaskMentionChip = ({ taskId, taskTitle, onTaskClick }) => (
  <button type="button" onClick={(e) => { e.stopPropagation(); onTaskClick?.(taskId); }}
    className="inline-flex items-center gap-0.5 px-1.5 py-0.5 rounded-md text-xs font-medium cursor-pointer transition-all hover:brightness-110"
    style={{ backgroundColor: 'rgba(99,102,241,0.2)', color: '#818cf8', border: '1px solid rgba(99,102,241,0.3)' }}>
    <Hash size={10} className="shrink-0" />{taskTitle}
  </button>
);

const UserMentionChip = ({ userName }) => (
  <span className="inline-flex items-center gap-0.5 px-1.5 py-0.5 rounded-md text-xs font-medium"
    style={{ backgroundColor: 'rgba(34,197,94,0.2)', color: '#4ade80', border: '1px solid rgba(34,197,94,0.3)' }}>
    <AtSign size={10} className="shrink-0" />{userName}
  </span>
);

// ─── Rich message text renderer ────────────────────────────────────
const RichMessageText = ({ text, onTaskClick, highlight }) => {
  const parts = parseMentions(text);
  return (
    <span className="whitespace-pre-wrap break-words">
      {parts.map((part, i) => {
        if (part.type === 'task-mention') {
          return <TaskMentionChip key={`tm-${i}`} taskId={part.taskId} taskTitle={part.taskTitle} onTaskClick={onTaskClick} />;
        }
        if (part.type === 'user-mention') {
          return <UserMentionChip key={`um-${i}`} userName={part.userName} />;
        }
        const content = renderTextWithLinks(part.raw);
        if (highlight && part.raw) {
          return <React.Fragment key={`t-${i}`}>{highlightText(part.raw, highlight)}</React.Fragment>;
        }
        return <React.Fragment key={`t-${i}`}>{content}</React.Fragment>;
      })}
    </span>
  );
};

function highlightText(text, query) {
  if (!query || !text) return renderTextWithLinks(text);
  const idx = text.toLowerCase().indexOf(query.toLowerCase());
  if (idx === -1) return renderTextWithLinks(text);
  return (
    <>
      {text.slice(0, idx)}
      <mark className="bg-yellow-400/40 rounded px-0.5">{text.slice(idx, idx + query.length)}</mark>
      {text.slice(idx + query.length)}
    </>
  );
}

// ─── Call message detection ───────────────────────────────────────
// Backend sends system call messages as plain text, e.g.:
//   "📞 Начат аудиозвонок"  /  "📞 Начат видеозвонок"
//   "📞 Звонок завершён (00:01)"
const CALL_START_RE = /(?:📞\s*)?Начат\s+(аудио|видео)звонок/i;
const CALL_END_RE = /(?:📞\s*)?Звонок завершён\s*\((\d+:\d+)\)/i;

function parseCallMessage(text) {
  if (!text) return null;
  const startMatch = text.match(CALL_START_RE);
  if (startMatch) return { kind: 'call_start', callType: startMatch[1].toLowerCase() };
  const endMatch = text.match(CALL_END_RE);
  if (endMatch) return { kind: 'call_end', duration: endMatch[1] };
  return null;
}

// Find call type and initiator for a call_end message by looking back at the preceding call_start
function resolveCallStart(messages, endIndex) {
  for (let i = endIndex - 1; i >= 0; i--) {
    const parsed = parseCallMessage(messages[i].text);
    if (parsed?.kind === 'call_start') return { callType: parsed.callType, initiatorId: messages[i].senderId };
  }
  return { callType: 'аудио', initiatorId: messages[endIndex]?.senderId };
}

// ─── Mention autocomplete dropdown (tasks + users) ─────────────────
const MentionDropdown = ({ tasks, members, query, onSelectTask, onSelectUser, position }) => {
  const filteredTasks = useMemo(() => {
    const q = query.toLowerCase();
    return Object.values(tasks)
      .filter((t) => {
        const title = (t.title || t.content || '').toLowerCase();
        return title.includes(q) || String(t.id || '').toLowerCase().includes(q);
      })
      .slice(0, 5);
  }, [tasks, query]);

  const filteredUsers = useMemo(() => {
    const q = query.toLowerCase();
    return (members || [])
      .filter((m) => {
        const name = (m.displayName || m.username || '').toLowerCase();
        return name.includes(q);
      })
      .slice(0, 5);
  }, [members, query]);

  if (filteredTasks.length === 0 && filteredUsers.length === 0) return null;

  return (
    <div className="absolute z-50 rounded-lg shadow-xl overflow-hidden border max-h-56 overflow-y-auto"
      style={{ bottom: position.bottom, left: 0, right: 0, backgroundColor: 'var(--app-layer-overlay, #1f1f1f)', borderColor: 'var(--app-border)' }}>
      {filteredUsers.length > 0 && (
        <>
          <div className="px-3 py-1 text-[10px] uppercase font-bold tracking-wider" style={{ color: 'var(--app-text-secondary)' }}>Users</div>
          {filteredUsers.map((user) => (
            <button key={user.userId} type="button" onClick={() => onSelectUser(user)}
              className="w-full flex items-center gap-2 px-3 py-2 text-left text-xs transition-colors hover:bg-white/10"
              style={{ color: 'var(--app-text-main)' }}>
              <AtSign size={12} className="shrink-0 text-green-400" />
              <span className="truncate flex-1">{user.displayName || user.username}</span>
            </button>
          ))}
        </>
      )}
      {filteredTasks.length > 0 && (
        <>
          <div className="px-3 py-1 text-[10px] uppercase font-bold tracking-wider" style={{ color: 'var(--app-text-secondary)' }}>Tasks</div>
          {filteredTasks.map((task) => (
            <button key={task.id} type="button" onClick={() => onSelectTask(task)}
              className="w-full flex items-center gap-2 px-3 py-2 text-left text-xs transition-colors hover:bg-white/10"
              style={{ color: 'var(--app-text-main)' }}>
              <Hash size={12} className="shrink-0 text-indigo-400" />
              <span className="truncate flex-1">{task.title || task.content || `Task ${task.id}`}</span>
              <span className="text-[10px] opacity-40 shrink-0">#{String(task.id).slice(0, 8)}</span>
            </button>
          ))}
        </>
      )}
    </div>
  );
};

// ─── Forward modal ─────────────────────────────────────────────────
const ForwardModal = ({ chats, currentChatId, currentUser, onForward, onClose }) => {
  const [filter, setFilter] = useState('');
  const filtered = useMemo(() =>
    chats
      .filter((c) => c.id !== currentChatId)
      .map((c) => ({ ...c, displayName: getChatDisplayName(c, currentUser, c.id, 'Chat') }))
      .filter((c) => c.displayName.toLowerCase().includes(filter.toLowerCase())),
    [chats, currentChatId, currentUser, filter],
  );

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50" onClick={onClose}>
      <div className="w-80 max-h-96 rounded-xl overflow-hidden shadow-2xl flex flex-col"
        style={{ backgroundColor: 'var(--app-layer-surface)', border: '1px solid var(--app-border)' }}
        onClick={(e) => e.stopPropagation()}>
        <div className="px-4 py-3 flex items-center gap-2" style={{ borderBottom: '1px solid var(--app-border)' }}>
          <Forward size={16} style={{ color: 'var(--app-accent)' }} />
          <span className="text-sm font-semibold" style={{ color: 'var(--app-text-main)' }}>Forward to...</span>
          <button onClick={onClose} className="ml-auto opacity-60 hover:opacity-100"><X size={14} /></button>
        </div>
        <div className="px-3 py-2">
          <input value={filter} onChange={(e) => setFilter(e.target.value)} placeholder="Search chats..."
            className="w-full bg-transparent outline-none text-xs px-2 py-1.5 rounded-lg"
            style={{ backgroundColor: 'var(--app-layer-base)', color: 'var(--app-text-main)', border: '1px solid var(--app-border)' }} />
        </div>
        <div className="flex-1 overflow-y-auto custom-scrollbar">
          {filtered.map((chat) => {
            const avatarUrl = chat.type === 'private'
              ? getOtherMember(chat, currentUser)?.avatarUrl
              : null;
            return (
              <button key={chat.id} onClick={() => onForward(chat.id)}
                className="w-full flex items-center gap-2 px-4 py-2.5 text-left text-xs transition-colors hover:bg-white/5"
                style={{ color: 'var(--app-text-main)' }}>
                {avatarUrl ? (
                  <img src={avatarUrl} alt="" className="w-7 h-7 rounded-full shrink-0 object-cover" />
                ) : (
                  <div className="w-7 h-7 rounded-full flex items-center justify-center text-[10px] font-bold shrink-0"
                    style={{ backgroundColor: stringToColor(chat.displayName), color: '#fff' }}>
                    {getInitials(chat.displayName)}
                  </div>
                )}
                <span className="truncate">{chat.displayName}</span>
              </button>
            );
          })}
          {filtered.length === 0 && (
            <div className="px-4 py-6 text-center text-xs opacity-40" style={{ color: 'var(--app-text-secondary)' }}>No chats found</div>
          )}
        </div>
      </div>
    </div>
  );
};

// ─── Pinned message bar ────────────────────────────────────────────
const PINNED_STORAGE_KEY = 'collaby-pinned-msgs';
function getPinnedMessages(chatId) {
  try { return JSON.parse(localStorage.getItem(PINNED_STORAGE_KEY) || '{}')[chatId] || []; } catch { return []; }
}
function setPinnedMessages(chatId, ids) {
  try {
    const all = JSON.parse(localStorage.getItem(PINNED_STORAGE_KEY) || '{}');
    all[chatId] = ids;
    localStorage.setItem(PINNED_STORAGE_KEY, JSON.stringify(all));
  } catch { /* noop */ }
}

// ─── Drafts ────────────────────────────────────────────────────────
const DRAFTS_KEY = 'collaby-chat-drafts';
function getDraft(chatId) {
  try { return (JSON.parse(localStorage.getItem(DRAFTS_KEY) || '{}'))[chatId] || ''; } catch { return ''; }
}
function saveDraft(chatId, text) {
  try {
    const all = JSON.parse(localStorage.getItem(DRAFTS_KEY) || '{}');
    if (text.trim()) { all[chatId] = text; } else { delete all[chatId]; }
    localStorage.setItem(DRAFTS_KEY, JSON.stringify(all));
  } catch { /* noop */ }
}

// ═══════════════════════════════════════════════════════════════════
// Main ChatWindow
// ═══════════════════════════════════════════════════════════════════
const ChatWindow = () => {
  const { t } = useTranslation('platform');
  const { language } = usePreferences();
  const {
    activeChat, chats, messages, sendMessage, loadMoreMessages,
    startTyping, stopTyping, typingUsers, onlineUsers,
    startCall, callNotif, dismissCallNotif, editMessage, removeMessage,
  } = useMessenger();
  const { currentUser, activeProject, activeWorkspace, setActivePage } = usePlatform();
  const navigate = useNavigate();

  const [text, setText] = useState('');
  const [replyTo, setReplyTo] = useState(null);
  const [showScrollDown, setShowScrollDown] = useState(false);
  const [mentionQuery, setMentionQuery] = useState(null);
  const [editingMessageId, setEditingMessageId] = useState(null);
  const [editText, setEditText] = useState('');
  const [deleteConfirmId, setDeleteConfirmId] = useState(null);

  // ─── Search state ──────────────────────────────────────────────
  const [searchOpen, setSearchOpen] = useState(false);
  const [searchQuery, setSearchQuery] = useState('');
  const [searchResults, setSearchResults] = useState([]);
  const [searchIndex, setSearchIndex] = useState(0);

  // ─── Forward state ─────────────────────────────────────────────
  const [forwardMsg, setForwardMsg] = useState(null);

  // ─── Pinned state ──────────────────────────────────────────────
  const [pinnedIds, setPinnedIds] = useState([]);

  const messagesEndRef = useRef(null);
  const messagesContainerRef = useRef(null);
  const inputRef = useRef(null);
  const editInputRef = useRef(null);
  const typingTimerRef = useRef(null);
  const prevChatRef = useRef(null);

  const chat = chats?.find((item) => item.id === activeChat);
  const chatMessages = messages[activeChat] || [];
  // Tasks — only for project chats (not private/channel)
  const isProjectChat = chat?.type === 'project' && chat?.projectId;
  const tasks = isProjectChat ? (activeProject?.boardData?.tasks || {}) : {};
  // Chat members (for calls & participant count)
  const chatMembers = chat?.members || [];
  // Mention users — only workspace members (not random chat members)
  const workspaceMembers = activeWorkspace?.members || [];

  // ─── Drafts: save on chat switch, restore on enter ─────────────
  useEffect(() => {
    // Save draft for previous chat
    if (prevChatRef.current && prevChatRef.current !== activeChat) {
      saveDraft(prevChatRef.current, text);
    }
    // Restore draft for new chat
    if (activeChat) {
      setText(getDraft(activeChat));
      setPinnedIds(getPinnedMessages(activeChat));
    }
    prevChatRef.current = activeChat;
    // Reset states on chat switch
    setReplyTo(null);
    setEditingMessageId(null);
    setSearchOpen(false);
    setSearchQuery('');
    setDeleteConfirmId(null);
  }, [activeChat]); // eslint-disable-line react-hooks/exhaustive-deps

  // ─── Search logic ──────────────────────────────────────────────
  useEffect(() => {
    if (!searchQuery.trim()) { setSearchResults([]); setSearchIndex(0); return; }
    const q = searchQuery.toLowerCase();
    const results = chatMessages
      .map((m, i) => ({ index: i, id: m.id }))
      .filter(({ index }) => (chatMessages[index].text || '').toLowerCase().includes(q));
    setSearchResults(results);
    setSearchIndex(0);
    // Scroll to first result
    if (results.length > 0) {
      const el = document.getElementById(`msg-${results[0].id}`);
      el?.scrollIntoView({ behavior: 'smooth', block: 'center' });
    }
  }, [searchQuery, chatMessages]);

  const navigateSearch = useCallback((dir) => {
    if (searchResults.length === 0) return;
    const next = (searchIndex + dir + searchResults.length) % searchResults.length;
    setSearchIndex(next);
    const el = document.getElementById(`msg-${searchResults[next].id}`);
    el?.scrollIntoView({ behavior: 'smooth', block: 'center' });
  }, [searchResults, searchIndex]);

  // ─── Auto-scroll ───────────────────────────────────────────────
  useEffect(() => {
    if (messagesEndRef.current) messagesEndRef.current.scrollIntoView({ behavior: 'smooth' });
  }, [chatMessages.length]);

  const handleScroll = useCallback(() => {
    const el = messagesContainerRef.current;
    if (!el) return;
    setShowScrollDown(el.scrollHeight - el.scrollTop - el.clientHeight > 100);
    if (el.scrollTop < 50) loadMoreMessages(activeChat);
  }, [activeChat, loadMoreMessages]);

  // ─── Send ──────────────────────────────────────────────────────
  const handleSend = async () => {
    if (!text.trim()) return;
    setMentionQuery(null);
    const draft = text;
    setText('');
    saveDraft(activeChat, '');
    try {
      await sendMessage(activeChat, draft, replyTo?.id);
      setReplyTo(null);
    } catch { setText(draft); }
  };

  // ─── Mention detection (@ triggers both users and tasks) ────────
  const updateMentionState = useCallback((value) => {
    const cursorPos = inputRef.current?.selectionStart ?? value.length;
    const before = value.slice(0, cursorPos);
    const atIdx = before.lastIndexOf('@');
    if (atIdx === -1) { setMentionQuery(null); return; }
    const afterAt = before.slice(atIdx + 1);
    if (afterAt.includes('\n') || (afterAt.includes(' ') && afterAt.length > 20)) { setMentionQuery(null); return; }
    setMentionQuery({ query: afterAt, atIndex: atIdx });
  }, []);

  const handleInput = (e) => {
    const val = e.target.value;
    setText(val);
    updateMentionState(val);
    if (!typingTimerRef.current) startTyping(activeChat);
    clearTimeout(typingTimerRef.current);
    typingTimerRef.current = setTimeout(() => { stopTyping(activeChat); typingTimerRef.current = null; }, 2000);
  };

  const insertMention = useCallback((mentionText) => {
    if (!mentionQuery) return;
    const before = text.slice(0, mentionQuery.atIndex);
    const after = text.slice(mentionQuery.atIndex + 1 + mentionQuery.query.length);
    const newText = `${before}${mentionText}${after || ' '}`;
    setText(newText);
    setMentionQuery(null);
    setTimeout(() => {
      if (inputRef.current) {
        const pos = before.length + mentionText.length + 1;
        inputRef.current.focus();
        inputRef.current.selectionStart = pos;
        inputRef.current.selectionEnd = pos;
      }
    }, 0);
  }, [text, mentionQuery]);

  const handleSelectTask = useCallback((task) => {
    insertMention(`@[${task.id}:${task.title || task.content || `Task ${task.id}`}]`);
  }, [insertMention]);

  const handleSelectUser = useCallback((user) => {
    insertMention(`@{${user.userId}:${user.displayName || user.username}}`);
  }, [insertMention]);

  const handleKeyDown = (e) => {
    if (e.key === 'Escape' && mentionQuery) { setMentionQuery(null); return; }
    if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); if (!mentionQuery) handleSend(); }
  };

  const handleTaskClick = useCallback((taskId) => {
    setActivePage('dashboard');
    navigate(`/dashboard?taskId=${encodeURIComponent(taskId)}`);
  }, [setActivePage, navigate]);

  // ─── Edit ──────────────────────────────────────────────────────
  const handleStartEdit = useCallback((msg) => { setEditingMessageId(msg.id); setEditText(msg.text); setDeleteConfirmId(null); }, []);
  const handleSaveEdit = useCallback(async () => {
    if (!editText.trim() || !editingMessageId) return;
    await editMessage(activeChat, editingMessageId, editText.trim());
    setEditingMessageId(null); setEditText('');
  }, [activeChat, editingMessageId, editText, editMessage]);
  const handleCancelEdit = useCallback(() => { setEditingMessageId(null); setEditText(''); }, []);
  const handleEditKeyDown = useCallback((e) => {
    if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); handleSaveEdit(); }
    if (e.key === 'Escape') handleCancelEdit();
  }, [handleSaveEdit, handleCancelEdit]);
  useEffect(() => {
    if (editingMessageId && editInputRef.current) {
      editInputRef.current.focus();
      editInputRef.current.selectionStart = editInputRef.current.value.length;
    }
  }, [editingMessageId]);

  // ─── Delete ────────────────────────────────────────────────────
  const handleDeleteMessage = useCallback(async (id) => { await removeMessage(activeChat, id); setDeleteConfirmId(null); }, [activeChat, removeMessage]);

  // ─── Forward ───────────────────────────────────────────────────
  const handleForward = useCallback(async (toChatId) => {
    if (!forwardMsg) return;
    const fwdText = `↪ Forwarded:\n${forwardMsg.text}`;
    await sendMessage(toChatId, fwdText);
    setForwardMsg(null);
  }, [forwardMsg, sendMessage]);

  // ─── Pin/Unpin ─────────────────────────────────────────────────
  const togglePin = useCallback((msgId) => {
    setPinnedIds((prev) => {
      const next = prev.includes(msgId) ? prev.filter((id) => id !== msgId) : [...prev, msgId];
      setPinnedMessages(activeChat, next);
      return next;
    });
  }, [activeChat]);

  const pinnedMessages = useMemo(() =>
    chatMessages.filter((m) => pinnedIds.includes(m.id)),
    [chatMessages, pinnedIds],
  );

  const scrollToBottom = () => messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });

  // ─── Header info ───────────────────────────────────────────────
  const chatDisplayName = getChatDisplayName(chat, currentUser, t('messenger.userFallback'), t('messenger.chatFallback'));
  const otherMember = chat?.type === 'private' ? getOtherMember(chat, currentUser) : null;
  const isOnline = otherMember && onlineUsers[otherMember.userId];
  const typingSet = typingUsers[activeChat];
  const typingList = typingSet ? Array.from(typingSet) : [];
  const typingNames = useMemo(() =>
    typingList.map((userId) => {
      const member = chatMembers.find((m) => m.userId === userId);
      return member?.displayName || member?.username || userId;
    }),
    [typingList, chatMembers],
  );

  // Active search highlight ID
  const activeSearchId = searchResults.length > 0 ? searchResults[searchIndex]?.id : null;

  return (
    <div className="flex-1 flex flex-col min-h-0">
      {/* ─── Header ─────────────────────────────────────────────── */}
      <div className="relative flex items-center gap-3 px-4 py-2.5 shrink-0"
        style={{ borderBottom: '1px solid var(--app-border)' }}>
        <div className="flex-1 min-w-0">
          <span className="text-sm font-semibold truncate block" style={{ color: 'var(--app-text-main)' }}>{chatDisplayName}</span>
          <div className="text-xs opacity-60" style={{ color: 'var(--app-text-secondary)' }}>
            {chat?.type === 'private'
              ? isOnline ? <span style={{ color: '#22c55e' }}>{t('messenger.chat.online')}</span> : t('messenger.chat.offline')
              : t('messenger.chat.participants', { count: chatMembers.length })}
          </div>
        </div>

        {callNotif && (
          <div className="absolute left-1/2 -translate-x-1/2 flex items-center gap-2 px-4 py-1.5 rounded-2xl shadow-lg"
            style={{ backgroundColor: callNotif.type === 'declined' ? '#dc2626' : callNotif.type === 'error' ? '#b45309' : '#1d4ed8', color: '#fff', border: '1px solid rgba(255,255,255,0.12)', zIndex: 10 }}>
            {callNotif.type === 'declined' ? <PhoneMissed size={14} /> : <PhoneOff size={14} />}
            <span className="text-xs font-medium whitespace-nowrap">{callNotif.message}</span>
            <button onClick={dismissCallNotif} className="opacity-60 hover:opacity-100 ml-1"><X size={12} /></button>
          </div>
        )}

        {/* Search toggle */}
        <button onClick={() => { setSearchOpen((v) => !v); if (searchOpen) { setSearchQuery(''); setSearchResults([]); } }}
          className="p-2 rounded-lg hover:opacity-80 transition-all"
          style={{ backgroundColor: searchOpen ? 'var(--app-accent)' : 'var(--app-layer-base)', color: searchOpen ? '#fff' : 'var(--app-text-secondary)' }}>
          <Search size={16} />
        </button>

        <button onClick={() => { const ids = chatMembers.filter((m) => m.userId !== currentUser?.id).map((m) => m.userId); if (ids.length > 0) startCall(ids, 'audio', activeChat); }}
          className="p-2 rounded-lg hover:opacity-80 transition-all"
          style={{ backgroundColor: 'var(--app-layer-base)', color: 'var(--app-text-secondary)' }} title={t('messenger.incomingCall.answerAudio')}>
          <Phone size={16} />
        </button>
        <button onClick={() => { const ids = chatMembers.filter((m) => m.userId !== currentUser?.id).map((m) => m.userId); if (ids.length > 0) startCall(ids, 'video', activeChat); }}
          className="p-2 rounded-lg hover:opacity-80 transition-all"
          style={{ backgroundColor: 'var(--app-layer-base)', color: 'var(--app-text-secondary)' }} title={t('messenger.incomingCall.answerVideo')}>
          <Video size={16} />
        </button>
      </div>

      {/* ─── Search bar ─────────────────────────────────────────── */}
      {searchOpen && (
        <div className="flex items-center gap-2 px-4 py-2 shrink-0" style={{ backgroundColor: 'var(--app-layer-base)', borderBottom: '1px solid var(--app-border)' }}>
          <Search size={14} style={{ color: 'var(--app-text-secondary)' }} />
          <input value={searchQuery} onChange={(e) => setSearchQuery(e.target.value)} placeholder="Search messages..."
            autoFocus className="flex-1 bg-transparent outline-none text-xs" style={{ color: 'var(--app-text-main)' }} />
          {searchResults.length > 0 && (
            <span className="text-[10px] shrink-0" style={{ color: 'var(--app-text-secondary)' }}>
              {searchIndex + 1}/{searchResults.length}
            </span>
          )}
          <button onClick={() => navigateSearch(-1)} className="p-0.5 rounded hover:bg-white/10"><ChevronUp size={14} style={{ color: 'var(--app-text-secondary)' }} /></button>
          <button onClick={() => navigateSearch(1)} className="p-0.5 rounded hover:bg-white/10"><ChevronDown size={14} style={{ color: 'var(--app-text-secondary)' }} /></button>
          <button onClick={() => { setSearchOpen(false); setSearchQuery(''); setSearchResults([]); }} className="p-0.5 rounded hover:bg-white/10"><X size={14} style={{ color: 'var(--app-text-secondary)' }} /></button>
        </div>
      )}

      {/* ─── Pinned messages bar ────────────────────────────────── */}
      {pinnedMessages.length > 0 && (
        <div className="px-4 py-2 shrink-0 flex items-center gap-2" style={{ backgroundColor: 'var(--app-layer-base)', borderBottom: '1px solid var(--app-border)' }}>
          <Pin size={12} style={{ color: 'var(--app-accent)' }} className="shrink-0" />
          <span className="text-xs truncate flex-1" style={{ color: 'var(--app-text-main)' }}>
            {pinnedMessages[pinnedMessages.length - 1]?.text || 'Pinned message'}
          </span>
          <button onClick={() => {
            const el = document.getElementById(`msg-${pinnedMessages[pinnedMessages.length - 1]?.id}`);
            el?.scrollIntoView({ behavior: 'smooth', block: 'center' });
          }} className="text-[10px] shrink-0 hover:underline" style={{ color: 'var(--app-accent)' }}>
            Show
          </button>
          <span className="text-[10px] shrink-0 opacity-50" style={{ color: 'var(--app-text-secondary)' }}>
            {pinnedMessages.length} pinned
          </span>
        </div>
      )}

      {/* ─── Messages ───────────────────────────────────────────── */}
      <div ref={messagesContainerRef} onScroll={handleScroll} className="flex-1 overflow-y-auto custom-scrollbar px-4 py-3 space-y-2 relative">
        {chatMessages.map((message, index) => {
          if (message.isDeleted || (!message.text && !message.type)) return null;

          // ── Call message handling ────────────────────────────
          const callInfo = parseCallMessage(message.text);
          // Hide "Начат аудио/видеозвонок" — we fold it into the end message
          if (callInfo?.kind === 'call_start') return null;
          // Render "Звонок завершён (XX:XX)" as WhatsApp-style call card
          if (callInfo?.kind === 'call_end') {
            const { callType, initiatorId } = resolveCallStart(chatMessages, index);
            const isCallOwn = initiatorId === currentUser?.id;
            const isVideo = callType === 'видео';
            const label = isVideo ? 'Видеозвонок' : 'Аудиозвонок';
            const iconColor = isVideo ? '#3b82f6' : '#22c55e';
            return (
              <div key={message.id} id={`msg-${message.id}`} className={cn('flex', isCallOwn ? 'justify-end' : 'justify-start')}>
                <div className={cn('flex items-center gap-3 px-3 py-2.5 rounded-2xl', isCallOwn ? 'rounded-br-md' : 'rounded-bl-md')}
                  style={{ backgroundColor: isCallOwn ? 'var(--app-accent)' : 'var(--app-layer-base)', border: isCallOwn ? 'none' : '1px solid var(--app-border)' }}>
                  {/* Phone icon circle */}
                  <div className="w-9 h-9 rounded-full flex items-center justify-center shrink-0"
                    style={{ backgroundColor: iconColor }}>
                    {isVideo ? <Video size={16} style={{ color: '#fff' }} /> : <Phone size={16} style={{ color: '#fff' }} />}
                  </div>
                  {/* Label + duration */}
                  <div className="flex flex-col min-w-0">
                    <span className="text-sm font-medium" style={{ color: isCallOwn ? 'var(--app-accent-text, #fff)' : 'var(--app-text-main)' }}>
                      {label}
                    </span>
                    <span className="text-xs" style={{ color: isCallOwn ? 'rgba(255,255,255,0.7)' : 'var(--app-text-secondary)' }}>
                      {callInfo.duration}
                    </span>
                  </div>
                  {/* Timestamp */}
                  <span className="text-[10px] shrink-0 self-end ml-2" style={{ color: isCallOwn ? 'rgba(255,255,255,0.6)' : 'var(--app-text-secondary)', opacity: 0.7 }}>
                    {formatTime(message.createdAt, language)}
                  </span>
                </div>
              </div>
            );
          }

          const isOwn = message.senderId === currentUser?.id;
          const memberInfo = chatMembers.find((m) => m.userId === message.senderId);
          const senderName = message.senderDisplayName || message.senderUsername
            || memberInfo?.displayName || memberInfo?.username
            || t('messenger.userFallback');
          const prev = chatMessages[index - 1];
          const showAvatar = !isOwn && (!prev || prev.senderId !== message.senderId);
          const isPrivate = chat?.type === 'private';
          const isPinned = pinnedIds.includes(message.id);
          const isSearchHit = activeSearchId === message.id;

          return (
            <MessageBubble key={message.id} language={language} message={message}
              isOwn={isOwn} senderName={senderName} showAvatar={!isPrivate && showAvatar} showName={!isPrivate && showAvatar}
              onReply={() => setReplyTo(message)} onTaskClick={handleTaskClick}
              onStartEdit={handleStartEdit} onDelete={(id) => setDeleteConfirmId(id)}
              onForward={() => setForwardMsg(message)}
              onTogglePin={() => togglePin(message.id)} isPinned={isPinned}
              isEditing={editingMessageId === message.id} editText={editText}
              onEditChange={setEditText} onEditKeyDown={handleEditKeyDown}
              onSaveEdit={handleSaveEdit} onCancelEdit={handleCancelEdit} editInputRef={editInputRef}
              isDeleteConfirm={deleteConfirmId === message.id}
              onDeleteConfirm={() => handleDeleteMessage(message.id)} onDeleteCancel={() => setDeleteConfirmId(null)}
              replyFallback={t('messenger.chat.replyFallback')}
              searchHighlight={searchQuery} isSearchHit={isSearchHit} />
          );
        })}
        <div ref={messagesEndRef} />
      </div>

      {/* ─── Typing indicator ───────────────────────────────────── */}
      {typingNames.length > 0 && (
        <div className="px-4 py-1 text-xs animate-pulse" style={{ color: 'var(--app-text-secondary)' }}>
          <span style={{ fontWeight: 500 }}>{typingNames.join(', ')}</span>
          {' '}{typingNames.length === 1 ? t('messenger.chat.typingOne') : t('messenger.chat.typingMany', { count: typingNames.length })}
        </div>
      )}

      {showScrollDown && (
        <button onClick={scrollToBottom} className="absolute bottom-20 right-8 p-2 rounded-full shadow-lg transition-all hover:scale-105"
          style={{ backgroundColor: 'var(--app-accent)', color: 'var(--app-accent-text, #fff)' }}><ArrowDown size={16} /></button>
      )}

      {/* ─── Reply bar ──────────────────────────────────────────── */}
      {replyTo && (
        <div className="flex items-center gap-2 px-4 py-2" style={{ backgroundColor: 'var(--app-layer-base)', borderTop: '1px solid var(--app-border)' }}>
          <Reply size={14} style={{ color: 'var(--app-accent)' }} />
          <span className="text-xs truncate flex-1" style={{ color: 'var(--app-text-secondary)' }}>
            {replyTo.senderDisplayName || replyTo.senderUsername || t('messenger.chat.replyFallback')}: {replyTo.text}
          </span>
          <button onClick={() => setReplyTo(null)} className="opacity-50 hover:opacity-100"><X size={14} /></button>
        </div>
      )}

      {/* ─── Input area ─────────────────────────────────────────── */}
      <div className="px-4 py-3 shrink-0 relative" style={{ borderTop: '1px solid var(--app-border)' }}>
        {mentionQuery && (Object.keys(tasks).length > 0 || workspaceMembers.length > 0) && (
          <MentionDropdown tasks={tasks} members={workspaceMembers.filter((m) => m.userId !== currentUser?.id)}
            query={mentionQuery.query} onSelectTask={handleSelectTask} onSelectUser={handleSelectUser}
            position={{ bottom: '100%' }} />
        )}
        <div className="flex items-end gap-2 rounded-xl px-3 py-2"
          style={{ backgroundColor: 'var(--app-layer-base)', border: '1px solid var(--app-border)' }}>
          <textarea ref={inputRef} value={text} onChange={handleInput} onKeyDown={handleKeyDown}
            placeholder={t('messenger.inputPlaceholder', { defaultValue: 'Write a message...' })}
            rows={1} className="flex-1 bg-transparent outline-none text-sm resize-none max-h-32"
            style={{ color: 'var(--app-text-main)' }} />
          <button onClick={handleSend} disabled={!text.trim()}
            className={cn('p-1.5 rounded-lg transition-all shrink-0', text.trim() ? 'opacity-100 hover:scale-105' : 'opacity-30')}
            style={{ backgroundColor: 'var(--app-accent)', color: 'var(--app-accent-text, #fff)' }}><Send size={16} /></button>
        </div>
      </div>

      {/* ─── Forward modal ──────────────────────────────────────── */}
      {forwardMsg && <ForwardModal chats={chats} currentChatId={activeChat} currentUser={currentUser} onForward={handleForward} onClose={() => setForwardMsg(null)} />}
    </div>
  );
};

// ═══════════════════════════════════════════════════════════════════
// MessageBubble
// ═══════════════════════════════════════════════════════════════════
const MessageBubble = ({
  message, isOwn, senderName, showAvatar, showName,
  onReply, onTaskClick, onStartEdit, onDelete, onForward, onTogglePin, isPinned,
  isEditing, editText, onEditChange, onEditKeyDown, onSaveEdit, onCancelEdit, editInputRef,
  isDeleteConfirm, onDeleteConfirm, onDeleteCancel,
  replyFallback, language, searchHighlight, isSearchHit,
}) => {
  const [hovered, setHovered] = useState(false);
  const replyMessage = message.replyTo ? { text: message.replyTo.text || '...' } : null;
  const msgText = message.text || '';
  const hasMentions = ANY_MENTION_PATTERN.test(msgText);

  return (
    <div id={`msg-${message.id}`}
      className={cn('flex gap-2 group', isOwn && 'flex-row-reverse', isSearchHit && 'ring-1 ring-yellow-400/50 rounded-lg')}
      onMouseEnter={() => setHovered(true)} onMouseLeave={() => setHovered(false)}>
      {/* Avatar — only for other people's messages */}
      {!isOwn && (
        <div className="w-8 shrink-0">
          {showAvatar && (
            <div className="w-8 h-8 rounded-full flex items-center justify-center text-xs font-semibold"
              style={{ backgroundColor: stringToColor(senderName), color: '#fff' }}>
              {getInitials(senderName)}
            </div>
          )}
        </div>
      )}

      <div className={cn('flex flex-col max-w-[70%] min-w-[60px]', isOwn && 'items-end')}>
        {showName && (
          <div className="text-[11px] font-medium mb-0.5 px-1" style={{ color: stringToColor(senderName) }}>{senderName}</div>
        )}

        {replyMessage && (
          <div className="text-[11px] px-2 py-1 mb-0.5 rounded-lg border-l-2 truncate"
            style={{ backgroundColor: 'var(--app-layer-base)', borderColor: 'var(--app-accent)', color: 'var(--app-text-secondary)' }}>
            {replyMessage.text || replyFallback}
          </div>
        )}

        {/* Pin indicator */}
        {isPinned && (
          <div className="flex items-center gap-1 mb-0.5 px-1">
            <Pin size={8} style={{ color: 'var(--app-accent)' }} />
            <span className="text-[9px]" style={{ color: 'var(--app-accent)' }}>Pinned</span>
          </div>
        )}

        {/* Bubble */}
        {message.isDeleted ? (
          <div className={cn('px-3 py-1.5 rounded-2xl text-sm italic opacity-50', isOwn ? 'rounded-br-md' : 'rounded-bl-md')}
            style={{ backgroundColor: isOwn ? 'var(--app-accent)' : 'var(--app-layer-base)', color: isOwn ? 'var(--app-accent-text, #fff)' : 'var(--app-text-secondary)' }}>
            Message deleted
          </div>
        ) : (
          <div className={cn('px-3 py-1.5 rounded-2xl text-sm relative', isOwn ? 'rounded-br-md' : 'rounded-bl-md')}
            style={{ backgroundColor: isOwn ? 'var(--app-accent)' : 'var(--app-layer-base)', color: isOwn ? 'var(--app-accent-text, #fff)' : 'var(--app-text-main)' }}>
            {isEditing ? (
              <div className="flex flex-col gap-1">
                <textarea ref={editInputRef} value={editText} onChange={(e) => onEditChange(e.target.value)} onKeyDown={onEditKeyDown}
                  rows={1} className="w-full bg-transparent outline-none text-sm resize-none max-h-32 border-b"
                  style={{ color: 'inherit', borderColor: 'rgba(255,255,255,0.3)' }} />
                <div className="flex gap-1 justify-end">
                  <button onClick={onCancelEdit} className="p-1 rounded hover:bg-black/20"><X size={12} /></button>
                  <button onClick={onSaveEdit} className="p-1 rounded hover:bg-black/20"><Check size={12} /></button>
                </div>
              </div>
            ) : (
              <>
                {hasMentions ? (
                  <RichMessageText text={msgText} onTaskClick={onTaskClick} highlight={searchHighlight} />
                ) : searchHighlight ? (
                  <div className="whitespace-pre-wrap break-words">{highlightText(msgText, searchHighlight)}</div>
                ) : (
                  <div className="whitespace-pre-wrap break-words">{renderTextWithLinks(msgText)}</div>
                )}
                <div className={cn('text-[10px] mt-0.5 text-right flex items-center justify-end gap-1', isOwn ? 'opacity-70' : 'opacity-40')}>
                  {message.isEdited && <span className="italic">edited</span>}
                  {formatTime(message.createdAt, language)}
                  {/* Read receipt — own messages */}
                  {isOwn && <CheckCheck size={12} className="ml-0.5" />}
                </div>
              </>
            )}
          </div>
        )}

        {/* Delete confirmation */}
        {isDeleteConfirm && (
          <div className="flex items-center gap-2 mt-1 px-2 py-1.5 rounded-lg text-xs"
            style={{ backgroundColor: 'rgba(220,38,38,0.15)', border: '1px solid rgba(220,38,38,0.3)' }}>
            <span style={{ color: '#fca5a5' }}>Delete this message?</span>
            <button onClick={onDeleteConfirm} className="px-2 py-0.5 rounded text-xs font-medium" style={{ backgroundColor: '#dc2626', color: '#fff' }}>Delete</button>
            <button onClick={onDeleteCancel} className="px-2 py-0.5 rounded text-xs hover:bg-white/10" style={{ color: 'var(--app-text-secondary)' }}>Cancel</button>
          </div>
        )}
      </div>

      {/* ─── Hover action buttons ─────────────────────────────── */}
      {hovered && !isEditing && !message.isDeleted && (
        <div className="flex items-start gap-0.5 shrink-0 mt-1">
          <HoverBtn onClick={onReply} title="Reply"><Reply size={12} /></HoverBtn>
          <HoverBtn onClick={onForward} title="Forward"><Forward size={12} /></HoverBtn>
          <HoverBtn onClick={onTogglePin} title={isPinned ? 'Unpin' : 'Pin'}>
            {isPinned ? <PinOff size={12} /> : <Pin size={12} />}
          </HoverBtn>
          {isOwn && <HoverBtn onClick={() => onStartEdit(message)} title="Edit"><Pencil size={12} /></HoverBtn>}
          {isOwn && <HoverBtn onClick={() => onDelete(message.id)} title="Delete" danger><Trash2 size={12} /></HoverBtn>}
        </div>
      )}
    </div>
  );
};

const HoverBtn = ({ onClick, title, children, danger }) => (
  <button onClick={onClick} title={title}
    className="p-1 rounded-md hover:opacity-80 transition-all"
    style={{ backgroundColor: 'var(--app-layer-base)', color: danger ? '#f87171' : 'var(--app-text-secondary)' }}>
    {children}
  </button>
);

// ─── Utility functions ─────────────────────────────────────────────
function getChatDisplayName(chat, currentUser, userFallback, chatFallback) {
  if (!chat) return '';
  if (chat.name) return chat.name;
  if (chat.type === 'private') {
    const other = getOtherMember(chat, currentUser);
    return other?.displayName || other?.username || userFallback;
  }
  return chat.projectName || chatFallback;
}

function getOtherMember(chat, currentUser) {
  if (chat?.type !== 'private' || !chat.members) return null;
  return chat.members.find((m) => m.userId !== currentUser?.id) || null;
}

function getInitials(name) {
  if (!name) return '?';
  return name.split(' ').map((w) => w[0]).join('').slice(0, 2).toUpperCase();
}

function stringToColor(str) {
  let hash = 0;
  for (let i = 0; i < (str || '').length; i++) hash = str.charCodeAt(i) + ((hash << 5) - hash);
  return `hsl(${Math.abs(hash % 360)}, 55%, 50%)`;
}

export default ChatWindow;
