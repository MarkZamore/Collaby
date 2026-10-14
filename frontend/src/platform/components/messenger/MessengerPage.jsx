// messenger call: PiP on chat open instead of split view
import React, { useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';

import { useMessenger } from '../../context/MessengerContext';
import ChatList from './ChatList';
import ChatWindow from './ChatWindow';
import CreateChatModal from './CreateChatModal';
import CallView from './CallView';

const SIDEBAR_WIDTH_KEY = 'collaby_messenger_sidebar_width';
const SIDEBAR_MIN_WIDTH = 260;
const SIDEBAR_MAX_WIDTH = 520;

function clamp(value, min, max) {
  return Math.max(min, Math.min(max, value));
}

function getInitialSidebarWidth() {
  if (typeof window === 'undefined') return 320;
  const stored = Number(window.localStorage.getItem(SIDEBAR_WIDTH_KEY));
  if (!Number.isFinite(stored)) return 320;
  return clamp(stored, SIDEBAR_MIN_WIDTH, SIDEBAR_MAX_WIDTH);
}

const MessengerPage = () => {
  const { t } = useTranslation('platform');
  const { activeChat, activeCallSession, isPiP, setIsPiP } = useMessenger();
  const [showCreateChat, setShowCreateChat] = useState(false);
  const [sidebarWidth, setSidebarWidth] = useState(getInitialSidebarWidth);
  const [isResizingSidebar, setIsResizingSidebar] = useState(false);
  const [isNarrowViewport, setIsNarrowViewport] = useState(() => (typeof window !== 'undefined' ? window.innerWidth < 1024 : false));
  const containerRef = useRef(null);

  const showCall = Boolean(activeCallSession && !isPiP);

  useEffect(() => {
    if (typeof window === 'undefined') return undefined;
    const mediaQuery = window.matchMedia('(max-width: 1023px)');
    const handleMediaChange = (event) => setIsNarrowViewport(event.matches);
    mediaQuery.addEventListener('change', handleMediaChange);
    setIsNarrowViewport(mediaQuery.matches);
    return () => mediaQuery.removeEventListener('change', handleMediaChange);
  }, []);

  // Sidebar resize
  useEffect(() => {
    if (!isResizingSidebar) return undefined;

    const handleMouseMove = (event) => {
      const rect = containerRef.current?.getBoundingClientRect();
      if (!rect) return;
      const maxAllowedByLayout = Math.max(SIDEBAR_MIN_WIDTH, Math.min(SIDEBAR_MAX_WIDTH, rect.width - 340));
      const nextWidth = clamp(event.clientX - rect.left, SIDEBAR_MIN_WIDTH, maxAllowedByLayout);
      setSidebarWidth(nextWidth);
    };

    const handleMouseUp = () => {
      setIsResizingSidebar(false);
      window.localStorage.setItem(SIDEBAR_WIDTH_KEY, String(Math.round(sidebarWidth)));
    };

    window.addEventListener('mousemove', handleMouseMove);
    window.addEventListener('mouseup', handleMouseUp);
    return () => {
      window.removeEventListener('mousemove', handleMouseMove);
      window.removeEventListener('mouseup', handleMouseUp);
    };
  }, [isResizingSidebar, sidebarWidth]);

  const sidebarWidthStyle = useMemo(() => {
    if (isNarrowViewport) return 'clamp(220px, 42vw, 300px)';
    return `${Math.round(sidebarWidth)}px`;
  }, [isNarrowViewport, sidebarWidth]);

  const isResizing = isResizingSidebar;
  const prevActiveChatRef = useRef(activeChat);

  // When user selects a chat while in a call, activate PiP so the call
  // becomes a small floating window and the chat takes the main area.
  useEffect(() => {
    const chatJustSelected = activeChat && activeChat !== prevActiveChatRef.current;
    prevActiveChatRef.current = activeChat;

    if (activeCallSession && chatJustSelected && !isPiP) {
      setIsPiP(true);
    }
  }, [activeChat, activeCallSession, isPiP, setIsPiP]);

  const renderContent = () => {
    // During an active call (not PiP): show CallView only, no chat panel
    if (showCall) {
      return <CallView />;
    }

    // Chat view (shown when no call, or when call is in PiP mode)
    if (activeChat) {
      return <ChatWindow />;
    }

    return <EmptyState />;
  };

  return (
    <div
      ref={containerRef}
      className="flex-1 flex overflow-hidden mx-4 mt-2 mb-2 rounded-xl relative"
      style={{
        backgroundColor: 'var(--app-layer-surface)',
        border: '1px solid var(--app-border)',
        userSelect: isResizing ? 'none' : undefined,
      }}
    >
      <ChatList
        onNewChat={() => setShowCreateChat(true)}
        width={sidebarWidthStyle}
      />

      {!isNarrowViewport && (
        <div
          onMouseDown={(event) => {
            event.preventDefault();
            setIsResizingSidebar(true);
          }}
          className="relative shrink-0 w-1 cursor-col-resize transition-colors"
          style={{ backgroundColor: isResizingSidebar ? 'var(--app-accent)' : 'var(--app-border)' }}
          aria-label={t('messenger.title')}
          role="separator"
          aria-orientation="vertical"
        >
          <div
            className="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 h-12 w-1 rounded-full"
            style={{ backgroundColor: 'rgba(255,255,255,0.35)' }}
          />
        </div>
      )}

      <div className="flex-1 flex flex-col min-w-0 min-h-0">
        {renderContent()}
      </div>

      {showCreateChat && <CreateChatModal onClose={() => setShowCreateChat(false)} />}
    </div>
  );
};

const EmptyState = () => {
  const { t } = useTranslation('platform');

  return (
    <div className="flex-1 flex items-center justify-center" style={{ color: 'var(--app-text-secondary)' }}>
      <div className="text-center">
        <div className="text-5xl mb-4 opacity-30">💬</div>
        <p className="text-lg font-medium">{t('messenger.emptyState.title')}</p>
        <p className="text-sm mt-1 opacity-70">{t('messenger.emptyState.description')}</p>
      </div>
    </div>
  );
};

export default MessengerPage;
