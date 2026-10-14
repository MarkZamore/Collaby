import React, { useState, useMemo, useRef, useEffect, useCallback } from 'react';
import {
  Search,
  Plus,
  MessageSquare,
  Users,
  Hash,
  Eraser,
  MoreVertical,
  LogOut,
  Archive,
  Trash2,
  ChevronDown,
  ChevronUp,
} from 'lucide-react';
import { useTranslation } from 'react-i18next';

import { useMessenger } from '../../context/MessengerContext';
import { usePlatform } from '../../context/PlatformContext';
import { usePreferences } from '../../context/PreferencesContext';
import { cn } from '../../../shared/utils/cn';
import { formatDate, formatTime } from '../../../shared/utils/locale';
import { resolveUserAvatar } from '../../../shared/utils/avatar';

const ARCHIVE_KEY = 'collaby_archived_chats';

// Format call messages for chat list preview
const CALL_START_RE = /(?:📞\s*)?Начат\s+(аудио|видео)звонок/i;
const CALL_END_RE = /(?:📞\s*)?Звонок завершён\s*\((\d+:\d+)\)/i;
function formatLastMessagePreview(text) {
  if (!text) return text;
  const startMatch = text.match(CALL_START_RE);
  if (startMatch) return `📞 ${startMatch[1] === 'видео' ? 'Видеозвонок' : 'Аудиозвонок'}`;
  const endMatch = text.match(CALL_END_RE);
  if (endMatch) return `📞 Звонок (${endMatch[1]})`;
  return text;
}

const getArchivedIds = () => {
  try {
    return new Set(JSON.parse(localStorage.getItem(ARCHIVE_KEY) || '[]'));
  } catch {
    return new Set();
  }
};

const CHAT_TYPE_ICONS = {
  private: MessageSquare,
  project: Users,
  channel: Hash,
};

const ChatList = ({ onNewChat, width = 320 }) => {
  const { t } = useTranslation('platform');
  const { language } = usePreferences();
  const { chats, activeChat, selectChat, onlineUsers, unreadTotal, leaveChat, clearChat, deleteChat } = useMessenger();
  const { currentUser, users } = usePlatform();
  const [search, setSearch] = useState('');
  const [filter, setFilter] = useState('all');
  const [archivedIds, setArchivedIds] = useState(getArchivedIds);
  const [archiveOpen, setArchiveOpen] = useState(false);

  const archiveChat = useCallback((chatId) => {
    setArchivedIds((prev) => {
      const next = new Set(prev);
      next.add(chatId);
      localStorage.setItem(ARCHIVE_KEY, JSON.stringify([...next]));
      return next;
    });
  }, []);

  const unarchiveChat = useCallback((chatId) => {
    setArchivedIds((prev) => {
      const next = new Set(prev);
      next.delete(chatId);
      localStorage.setItem(ARCHIVE_KEY, JSON.stringify([...next]));
      return next;
    });
  }, []);

  const filtered = useMemo(() => {
    let list = (chats || []).filter((chat) => !archivedIds.has(chat.id));

    if (filter !== 'all') {
      list = list.filter((chat) => chat.type === filter);
    }

    if (search.trim()) {
      const query = search.toLowerCase();
      list = list.filter((chat) => {
        const name = getChatDisplayName(
          chat,
          currentUser,
          t('messenger.userFallback'),
          t('messenger.chatFallback'),
        );

        return name?.toLowerCase().includes(query);
      });
    }

    return [...list].sort((left, right) => {
      if ((left.unreadCount || 0) > 0 && !(right.unreadCount > 0)) return -1;
      if ((right.unreadCount || 0) > 0 && !(left.unreadCount > 0)) return 1;

      const leftTime = left.lastMessage?.sentAt || left.updatedAt || left.createdAt || '';
      const rightTime = right.lastMessage?.sentAt || right.updatedAt || right.createdAt || '';

      return rightTime.localeCompare(leftTime);
    });
  }, [archivedIds, chats, currentUser, filter, search, t]);

  const archivedChats = useMemo(
    () => (chats || []).filter((chat) => archivedIds.has(chat.id)),
    [archivedIds, chats],
  );

  const filters = [
    { id: 'all', label: t('messenger.filters.all') },
    { id: 'private', label: t('messenger.filters.private') },
    { id: 'project', label: t('messenger.filters.project') },
    { id: 'channel', label: t('messenger.filters.channel') },
  ];

  return (
    <div
      className="flex flex-col shrink-0"
      style={{
        width,
        borderRight: '1px solid var(--app-border)',
        backgroundColor: 'var(--app-layer-surface)',
      }}
    >
      <div
        className="flex items-center justify-between px-4 py-3"
        style={{ borderBottom: '1px solid var(--app-border)' }}
      >
        <h2 className="text-base font-semibold" style={{ color: 'var(--app-text-main)' }}>
          {t('messenger.title')}
          {unreadTotal > 0 && (
            <span
              className="ml-2 px-1.5 py-0.5 text-xs rounded-full font-medium"
              style={{ backgroundColor: 'var(--app-accent)', color: 'var(--app-accent-text, #fff)' }}
            >
              {unreadTotal}
            </span>
          )}
        </h2>

        <button
          onClick={onNewChat}
          className="p-1.5 rounded-lg hover:opacity-80 transition-all"
          style={{ backgroundColor: 'var(--app-accent)', color: 'var(--app-accent-text, #fff)' }}
          aria-label={t('messenger.newChat')}
        >
          <Plus size={16} />
        </button>
      </div>

      <div className="px-3 py-2">
        <div className="relative">
          <Search
            size={14}
            className="absolute left-3 top-1/2 -translate-y-1/2 opacity-50"
            style={{ color: 'var(--app-text-secondary)' }}
          />
          <input
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            placeholder={t('messenger.searchPlaceholder')}
            className="w-full pl-8 pr-3 py-1.5 rounded-lg text-sm outline-none transition-all"
            style={{
              backgroundColor: 'var(--app-layer-base)',
              border: '1px solid var(--app-border)',
              color: 'var(--app-text-main)',
            }}
          />
        </div>
      </div>

      <div className="flex gap-1 px-3 pb-2">
        {filters.map((item) => (
          <button
            key={item.id}
            onClick={() => setFilter(item.id)}
            className={cn(
              'px-2.5 py-1 rounded-lg text-xs font-medium transition-all',
              filter === item.id ? 'opacity-100' : 'opacity-60 hover:opacity-80',
            )}
            style={{
              backgroundColor: filter === item.id ? 'var(--app-accent)' : 'var(--app-layer-base)',
              color:
                filter === item.id
                  ? 'var(--app-accent-text, #fff)'
                  : 'var(--app-text-secondary)',
            }}
          >
            {item.label}
          </button>
        ))}
      </div>

      <div className="flex-1 overflow-y-auto custom-scrollbar">
        {filtered.length === 0 ? (
          <div className="text-center py-8 text-sm opacity-50" style={{ color: 'var(--app-text-secondary)' }}>
            {search ? t('messenger.emptySearch') : t('messenger.emptyChats')}
          </div>
        ) : (
          filtered.map((chat) => (
            <ChatListItem
              key={chat.id}
              chat={chat}
              isActive={activeChat === chat.id}
              onClick={() => selectChat(chat.id)}
              onArchive={() => archiveChat(chat.id)}
              onLeave={() => leaveChat(chat.id)}
              onClear={() => clearChat?.(chat.id)}
              onDelete={() => deleteChat?.(chat.id)}
              currentUser={currentUser}
              users={users}
              onlineUsers={onlineUsers}
              language={language}
            />
          ))
        )}

        {archivedChats.length > 0 && (
          <div style={{ borderTop: '1px solid var(--app-border)' }}>
            <button
              onClick={() => setArchiveOpen((value) => !value)}
              className="w-full flex items-center justify-between px-4 py-2.5 text-xs font-medium hover:opacity-80 transition-opacity"
              style={{ color: 'var(--app-text-secondary)' }}
            >
              <div className="flex items-center gap-2">
                <Archive size={13} />
                <span>{t('messenger.archive', { count: archivedChats.length })}</span>
              </div>
              {archiveOpen ? <ChevronUp size={13} /> : <ChevronDown size={13} />}
            </button>

            {archiveOpen &&
              archivedChats.map((chat) => (
                <ChatListItem
                  key={chat.id}
                  chat={chat}
                  isActive={activeChat === chat.id}
                  onClick={() => selectChat(chat.id)}
                  onArchive={() => unarchiveChat(chat.id)}
                  onLeave={() => leaveChat(chat.id)}
                  onClear={() => clearChat?.(chat.id)}
                  onDelete={() => deleteChat?.(chat.id)}
                  isArchived
                  currentUser={currentUser}
                  users={users}
                  onlineUsers={onlineUsers}
                  language={language}
                />
              ))}
          </div>
        )}
      </div>
    </div>
  );
};

const ChatListItem = ({
  chat,
  isActive,
  onClick,
  onArchive,
  onLeave,
  onClear,
  onDelete,
  currentUser,
  users,
  onlineUsers,
  isArchived = false,
  language,
}) => {
  const { t } = useTranslation('platform');
  const displayName = getChatDisplayName(
    chat,
    currentUser,
    t('messenger.userFallback'),
    t('messenger.chatFallback'),
  );
  const otherMember = getOtherMember(chat, currentUser);
  const isOnline = chat.type === 'private' && otherMember && onlineUsers[otherMember.userId];
  const privateAvatarSrc = getPrivateChatAvatar(chat, currentUser, users, displayName);
  const currentMember = chat?.members?.find((member) => isSameId(member.userId, currentUser?.id));
  const isAdminMember = String(currentMember?.role || '').toLowerCase() === 'admin';
  const isPrivateChat = chat.type === 'private';
  const canDeleteForEveryone = isPrivateChat
    ? isSameId(chat.createdBy, currentUser?.id)
    : isAdminMember;
  const canClearChat = isPrivateChat || isAdminMember;
  const TypeIcon = CHAT_TYPE_ICONS[chat.type] || MessageSquare;
  const lastMessage = chat.lastMessage;
  const [menuOpen, setMenuOpen] = useState(false);
  const menuRef = useRef(null);

  useEffect(() => {
    if (!menuOpen) {
      return undefined;
    }

    const handler = (event) => {
      if (menuRef.current && !menuRef.current.contains(event.target)) {
        setMenuOpen(false);
      }
    };

    document.addEventListener('mousedown', handler);
    return () => document.removeEventListener('mousedown', handler);
  }, [menuOpen]);

  return (
    <div
      className={cn(
        'relative w-full flex items-center gap-3 px-4 py-3 transition-all group',
        isActive ? 'opacity-100' : 'opacity-80 hover:opacity-100',
      )}
      style={{
        backgroundColor: isActive ? 'var(--app-sidebar-item-active-bg, rgba(0,0,0,0.06))' : 'transparent',
        cursor: 'pointer',
      }}
      onClick={onClick}
    >
      <div className="relative shrink-0">
        <div
          className="w-10 h-10 rounded-full flex items-center justify-center text-sm font-semibold"
          style={{
            backgroundColor: chat.type === 'private' ? stringToColor(displayName) : 'var(--app-accent)',
            color: '#fff',
          }}
        >
          {chat.type === 'private' ? (
            <img
              src={privateAvatarSrc}
              alt={displayName}
              className="w-10 h-10 rounded-full object-cover"
            />
          ) : <TypeIcon size={18} />}
        </div>
        {isOnline && (
          <div
            className="absolute -bottom-0.5 -right-0.5 w-3.5 h-3.5 rounded-full border-2"
            style={{ backgroundColor: '#22c55e', borderColor: 'var(--app-layer-surface)' }}
          />
        )}
      </div>

      <div className="flex-1 min-w-0">
        <div className="flex items-center justify-between">
          <span className="text-sm font-medium truncate" style={{ color: 'var(--app-text-main)' }}>
            {displayName}
          </span>
          {lastMessage?.sentAt && !menuOpen && (
            <span
              className="text-[10px] shrink-0 ml-1 opacity-50 group-hover:hidden"
              style={{ color: 'var(--app-text-secondary)' }}
            >
              {formatChatListTimestamp(lastMessage.sentAt, language, t)}
            </span>
          )}
        </div>
        <div className="flex items-center justify-between mt-0.5">
          <span className="text-xs truncate opacity-60" style={{ color: 'var(--app-text-secondary)' }}>
            {formatLastMessagePreview(lastMessage?.text) || t('messenger.noMessages')}
          </span>
          {(chat.unreadCount || 0) > 0 && (
            <span
              className="ml-1 shrink-0 w-5 h-5 rounded-full flex items-center justify-center text-[10px] font-bold"
              style={{ backgroundColor: 'var(--app-accent)', color: 'var(--app-accent-text, #fff)' }}
            >
              {chat.unreadCount > 99 ? '99+' : chat.unreadCount}
            </span>
          )}
        </div>
      </div>

      <div className="relative shrink-0" ref={menuRef}>
        <button
          onClick={(event) => {
            event.stopPropagation();
            setMenuOpen((value) => !value);
          }}
          className={cn('p-1 rounded-md transition-opacity', menuOpen ? 'opacity-100' : 'opacity-0 group-hover:opacity-100')}
          style={{ color: 'var(--app-text-secondary)' }}
          title={t('messenger.menuTitle')}
        >
          <MoreVertical size={14} />
        </button>

        {menuOpen && (
          <div
            className="absolute right-0 top-full mt-1 w-48 rounded-xl shadow-2xl z-50 overflow-hidden"
            style={{ backgroundColor: 'var(--app-sidebar-bg)', border: '1px solid var(--app-border)' }}
            onClick={(event) => event.stopPropagation()}
          >
            <button
              onClick={() => {
                setMenuOpen(false);
                onArchive();
              }}
              className="w-full flex items-center gap-2.5 px-3 py-2.5 text-sm hover:opacity-80 transition-opacity text-left"
              style={{ color: 'var(--app-text-secondary)' }}
            >
              <Archive size={14} />
              <span>{isArchived ? t('messenger.actions.unarchive') : t('messenger.actions.archive')}</span>
            </button>
            <div style={{ borderTop: '1px solid var(--app-border)' }} />
            <button
              onClick={() => {
                setMenuOpen(false);
                onLeave();
              }}
              className="w-full flex items-center gap-2.5 px-3 py-2.5 text-sm hover:opacity-80 transition-opacity text-left"
              style={{ color: '#f87171' }}
            >
              <LogOut size={14} />
              <span>{t('messenger.actions.removeForMe')}</span>
            </button>
            {canClearChat && (
              <button
                onClick={() => {
                  setMenuOpen(false);
                  onClear?.();
                }}
                className="w-full flex items-center gap-2.5 px-3 py-2.5 text-sm hover:opacity-80 transition-opacity text-left"
                style={{ color: '#f59e0b' }}
              >
                <Eraser size={14} />
                <span>{t('messenger.actions.clearChat')}</span>
              </button>
            )}
            {canDeleteForEveryone && (
              <button
                onClick={() => {
                  setMenuOpen(false);
                  onDelete?.();
                }}
                className="w-full flex items-center gap-2.5 px-3 py-2.5 text-sm hover:opacity-80 transition-opacity text-left"
                style={{ color: '#ef4444' }}
              >
                <Trash2 size={14} />
                <span>{t('messenger.actions.deleteForEveryone')}</span>
              </button>
            )}
          </div>
        )}
      </div>
    </div>
  );
};

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
  return chat.members.find((member) => !isSameId(member.userId, currentUser?.id)) || null;
}

function isSameId(a, b) {
  if (a == null || b == null) {
    return false;
  }

  return String(a).toLowerCase() === String(b).toLowerCase();
}

function getPrivateChatAvatar(chat, currentUser, users, displayName) {
  const otherMember = getOtherMember(chat, currentUser);
  const directoryUser = (users || []).find((user) => isSameId(user.id, otherMember?.userId));

  return resolveUserAvatar({
    avatarUrl: otherMember?.avatarUrl || directoryUser?.avatarUrl || directoryUser?.avatar,
    userId: otherMember?.userId || directoryUser?.id,
    displayName: otherMember?.displayName || directoryUser?.name || displayName,
    username: otherMember?.username || directoryUser?.username,
  });
}

function getInitials(name) {
  if (!name) return '?';
  return name
    .split(' ')
    .map((word) => word[0])
    .join('')
    .slice(0, 2)
    .toUpperCase();
}

function stringToColor(str) {
  let hash = 0;
  for (let index = 0; index < (str || '').length; index += 1) {
    hash = str.charCodeAt(index) + ((hash << 5) - hash);
  }

  const hue = Math.abs(hash % 360);
  return `hsl(${hue}, 55%, 50%)`;
}

function formatChatListTimestamp(value, language, t) {
  if (!value) return '';

  const date = new Date(value);
  const now = new Date();
  const yesterday = new Date(now);
  yesterday.setDate(yesterday.getDate() - 1);

  if (date.toDateString() === now.toDateString()) {
    return formatTime(date, language, { hour: '2-digit', minute: '2-digit' });
  }

  if (date.toDateString() === yesterday.toDateString()) {
    return t('messenger.time.yesterday');
  }

  return formatDate(date, language, { day: 'numeric', month: 'short' });
}

export default ChatList;
