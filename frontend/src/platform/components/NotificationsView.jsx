import React, { useMemo, useState } from 'react';
import { Bell, Inbox, MessageSquare, UserPlus, ClipboardList, CheckCircle, XCircle } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { cn } from '../../shared/utils/cn';
import { usePlatform } from '../context/PlatformContext';

const TYPE_CONFIG = {
  TaskAssigned: { icon: ClipboardList, color: 'text-blue-500', bg: 'bg-blue-500/10' },
  WorkspaceInvite: { icon: UserPlus, color: 'text-green-500', bg: 'bg-green-500/10' },
  CommentAdded: { icon: MessageSquare, color: 'text-purple-500', bg: 'bg-purple-500/10' },
  TaskUpdated: { icon: ClipboardList, color: 'text-amber-500', bg: 'bg-amber-500/10' },
  Mention: { icon: Bell, color: 'text-pink-500', bg: 'bg-pink-500/10' },
};

const timeAgo = (dateStr, t) => {
  const diff = Date.now() - new Date(dateStr).getTime();
  const mins = Math.floor(diff / 60000);

  if (mins < 1) return t('notifications.time.justNow');
  if (mins < 60) return t('notifications.time.minutesAgo', { count: mins });

  const hours = Math.floor(mins / 60);
  if (hours < 24) return t('notifications.time.hoursAgo', { count: hours });

  const days = Math.floor(hours / 24);
  return t('notifications.time.daysAgo', { count: days });
};

const NotificationsView = () => {
  const { t } = useTranslation('platform');
  const {
    notifications,
    unreadCount,
    markNotificationRead,
    markAllNotificationsRead,
    handleNotificationAction,
  } = usePlatform();

  const [filter, setFilter] = useState('all');

  const filtered = useMemo(
    () => (filter === 'unread' ? notifications.filter((item) => !item.isRead) : notifications),
    [filter, notifications],
  );

  return (
    <div className="h-full flex flex-col">
      <div className="px-6 pt-5 pb-0">
        <h1 className="text-2xl font-bold flex items-center gap-2" style={{ color: 'var(--app-text-main)' }}>
          <Bell size={22} style={{ color: 'var(--app-accent)' }} />
          {t('notifications.title')}
        </h1>
        <p className="text-sm mt-1 mb-4" style={{ color: 'var(--app-text-secondary)' }}>
          {t('notifications.subtitle')}
        </p>
      </div>

      <div className="flex items-center justify-between px-6 py-4 border-b" style={{ borderColor: 'var(--app-border)' }}>
        <div className="flex items-center gap-2">
          <h2 className="text-base font-semibold" style={{ color: 'var(--app-text-main)' }}>
            {t('notifications.inbox')}
          </h2>
          {unreadCount > 0 && (
            <span
              className="ml-1 px-2 py-0.5 rounded-full text-xs font-bold"
              style={{ backgroundColor: 'var(--app-accent)', color: 'var(--app-accent-text)' }}
            >
              {unreadCount}
            </span>
          )}
        </div>

        <div className="flex items-center gap-2">
          {unreadCount > 0 && (
            <button
              onClick={markAllNotificationsRead}
              className="text-xs font-medium hover:underline mr-2"
              style={{ color: 'var(--app-accent)' }}
            >
              {t('notifications.markAllRead')}
            </button>
          )}
          <div className="flex items-center gap-1 p-0.5 rounded-lg" style={{ backgroundColor: 'var(--app-layer-base)' }}>
            {[
              ['all', t('notifications.filters.all')],
              ['unread', t('notifications.filters.unread')],
            ].map(([id, label]) => (
              <button
                key={id}
                onClick={() => setFilter(id)}
                className={cn(
                  'px-3 py-1 text-xs font-medium rounded-md transition-all',
                  filter === id ? 'shadow-sm' : 'hover:opacity-80',
                )}
                style={
                  filter === id
                    ? { backgroundColor: 'var(--app-layer-surface)', color: 'var(--app-text-main)' }
                    : { color: 'var(--app-text-secondary)' }
                }
              >
                {label}
              </button>
            ))}
          </div>
        </div>
      </div>

      <div className="flex-1 overflow-y-auto px-6 py-4">
        {filtered.length === 0 ? (
          <div className="flex flex-col items-center justify-center h-full text-center py-20">
            <div
              className="w-16 h-16 rounded-2xl flex items-center justify-center mb-4"
              style={{ backgroundColor: 'var(--app-layer-surface)' }}
            >
              <Inbox size={28} style={{ color: 'var(--app-text-secondary)' }} />
            </div>
            <h3 className="text-base font-semibold mb-1" style={{ color: 'var(--app-text-main)' }}>
              {t('notifications.emptyTitle')}
            </h3>
            <p className="text-sm" style={{ color: 'var(--app-text-secondary)' }}>
              {t('notifications.emptyDescription')}
            </p>
          </div>
        ) : (
          <div className="space-y-2">
            {filtered.map((notification) => {
              const cfg = TYPE_CONFIG[notification.type] || TYPE_CONFIG.TaskUpdated;
              const Icon = cfg.icon;

              return (
                <div
                  key={notification.id}
                  onClick={() => !notification.isRead && markNotificationRead(notification.id)}
                  className={cn('flex items-start gap-3 p-3 rounded-xl transition-colors cursor-pointer', !notification.isRead && 'ring-1')}
                  style={{
                    backgroundColor: notification.isRead ? 'transparent' : 'var(--app-layer-surface)',
                    '--tw-ring-color': !notification.isRead ? 'var(--app-accent)' : 'transparent',
                  }}
                >
                  <div className={`w-9 h-9 rounded-lg flex items-center justify-center shrink-0 mt-0.5 ${cfg.bg}`}>
                    <Icon size={16} className={cfg.color} />
                  </div>
                  <div className="flex-1 min-w-0">
                    <p className="text-sm font-medium" style={{ color: 'var(--app-text-main)' }}>
                      {notification.title}
                    </p>
                    <p className="text-xs mt-0.5" style={{ color: 'var(--app-text-secondary)' }}>
                      {notification.message}
                    </p>

                    {notification.type === 'WorkspaceInvite' && !notification.actionStatus && (
                      <div className="flex gap-2 mt-2">
                        <button
                          onClick={(event) => {
                            event.stopPropagation();
                            handleNotificationAction(notification.id, 'accepted');
                          }}
                          className="flex items-center gap-1 text-xs px-3 py-1 rounded-lg bg-green-500/15 text-green-500 hover:bg-green-500/25 transition-colors font-medium"
                        >
                          <CheckCircle size={13} /> {t('notifications.accept')}
                        </button>
                        <button
                          onClick={(event) => {
                            event.stopPropagation();
                            handleNotificationAction(notification.id, 'declined');
                          }}
                          className="flex items-center gap-1 text-xs px-3 py-1 rounded-lg bg-red-500/15 text-red-500 hover:bg-red-500/25 transition-colors font-medium"
                        >
                          <XCircle size={13} /> {t('notifications.decline')}
                        </button>
                      </div>
                    )}

                    {notification.actionStatus === 'accepted' && (
                      <span className="inline-flex items-center gap-1 text-xs text-green-500 mt-1">
                        <CheckCircle size={12} /> {t('notifications.accepted')}
                      </span>
                    )}
                    {notification.actionStatus === 'declined' && (
                      <span className="inline-flex items-center gap-1 text-xs text-red-500 mt-1">
                        <XCircle size={12} /> {t('notifications.declined')}
                      </span>
                    )}

                    <p className="text-xs mt-1" style={{ color: 'var(--app-text-secondary)', opacity: 0.6 }}>
                      {timeAgo(notification.createdAt, t)}
                    </p>
                  </div>
                  {!notification.isRead && (
                    <span className="w-2 h-2 rounded-full shrink-0 mt-2" style={{ backgroundColor: 'var(--app-accent)' }} />
                  )}
                </div>
              );
            })}
          </div>
        )}
      </div>
    </div>
  );
};

export default NotificationsView;
