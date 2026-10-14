import React from 'react';
import { Bell, MessageCircle, UserPlus, ClipboardList, CheckCircle, XCircle } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { usePlatform } from '../context/PlatformContext';

const TYPE_CONFIG = {
  TaskAssigned: { icon: ClipboardList, color: 'text-blue-500', bg: 'bg-blue-500/10' },
  WorkspaceInvite: { icon: UserPlus, color: 'text-green-500', bg: 'bg-green-500/10' },
  CommentAdded: { icon: MessageCircle, color: 'text-purple-500', bg: 'bg-purple-500/10' },
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

const NotificationPanel = ({ isOpen }) => {
  const { t } = useTranslation('platform');
  const {
    notifications,
    markNotificationRead,
    markAllNotificationsRead,
    handleNotificationAction,
  } = usePlatform();

  if (!isOpen) return null;

  return (
    <div
      style={{
        backgroundColor: 'var(--app-layer-overlay)',
        borderColor: 'var(--app-layer-border)',
        color: 'var(--app-text-main)',
      }}
      className="absolute right-4 top-full mt-2 w-80 sm:w-96 rounded-xl shadow-2xl border overflow-hidden z-50"
    >
      <div
        className="p-4 border-b flex items-center justify-between"
        style={{ borderColor: 'var(--app-layer-border)', backgroundColor: 'var(--app-layer-surface)' }}
      >
        <h3 className="font-bold flex items-center gap-2">
          <Bell size={16} className="text-blue-500" />
          {t('notifications.title')}
        </h3>
        <button
          onClick={markAllNotificationsRead}
          className="text-xs font-medium text-blue-500 hover:underline"
        >
          {t('notifications.markAllRead')}
        </button>
      </div>

      <div className="max-h-[400px] overflow-y-auto">
        {notifications.length === 0 ? (
          <div className="p-8 text-center" style={{ color: 'var(--app-text-secondary)' }}>
            <Bell size={32} className="mx-auto mb-3 opacity-20" />
            <p className="text-sm">{t('notifications.emptyNew')}</p>
          </div>
        ) : (
          <div className="divide-y" style={{ borderColor: 'var(--app-layer-border)' }}>
            {notifications.map((notification) => {
              const cfg = TYPE_CONFIG[notification.type] || TYPE_CONFIG.TaskUpdated;
              const Icon = cfg.icon;

              return (
                <div
                  key={notification.id}
                  onClick={() => !notification.isRead && markNotificationRead(notification.id)}
                  className={`p-4 transition-colors cursor-pointer hover:opacity-80 ${!notification.isRead ? 'bg-blue-50/10' : ''}`}
                  style={{ borderColor: 'var(--app-layer-border)' }}
                >
                  <div className="flex gap-3">
                    <div className={`w-8 h-8 rounded-full flex items-center justify-center flex-shrink-0 ${cfg.bg}`}>
                      <Icon size={16} className={cfg.color} />
                    </div>
                    <div className="flex-1 min-w-0">
                      <p className="text-sm font-medium leading-snug mb-0.5" style={{ color: 'var(--app-text-main)' }}>
                        {notification.title}
                      </p>
                      <p className="text-xs mb-1" style={{ color: 'var(--app-text-secondary)' }}>
                        {notification.message}
                      </p>

                      {notification.type === 'WorkspaceInvite' && !notification.actionStatus && (
                        <div className="flex gap-2 mt-1.5">
                          <button
                            onClick={(event) => {
                              event.stopPropagation();
                              handleNotificationAction(notification.id, 'accepted');
                            }}
                            className="flex items-center gap-1 text-xs px-2 py-0.5 rounded bg-green-500/15 text-green-500 hover:bg-green-500/25 transition-colors"
                          >
                            <CheckCircle size={12} /> {t('notifications.accept')}
                          </button>
                          <button
                            onClick={(event) => {
                              event.stopPropagation();
                              handleNotificationAction(notification.id, 'declined');
                            }}
                            className="flex items-center gap-1 text-xs px-2 py-0.5 rounded bg-red-500/15 text-red-500 hover:bg-red-500/25 transition-colors"
                          >
                            <XCircle size={12} /> {t('notifications.decline')}
                          </button>
                        </div>
                      )}

                      {notification.actionStatus === 'accepted' && (
                        <span className="text-xs text-green-500">{t('notifications.accepted')}</span>
                      )}
                      {notification.actionStatus === 'declined' && (
                        <span className="text-xs text-red-500">{t('notifications.declined')}</span>
                      )}

                      <span className="text-xs block mt-1" style={{ color: 'var(--app-text-secondary)', opacity: 0.6 }}>
                        {timeAgo(notification.createdAt, t)}
                      </span>
                    </div>
                    {!notification.isRead && (
                      <div className="w-2 h-2 rounded-full bg-blue-500 mt-1.5 flex-shrink-0" />
                    )}
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </div>
    </div>
  );
};

export default NotificationPanel;
