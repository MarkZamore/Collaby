import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Toaster } from 'react-hot-toast';
import { useNavigate } from 'react-router-dom';
import Sidebar from './Sidebar';
import TopBar from './TopBar';
import CreateTaskModal from './CreateTaskModal';
import IncomingCallModal from './messenger/IncomingCallModal';
import PictureInPicture from './messenger/PictureInPicture';
import { PlatformProvider, usePlatform } from '../context/PlatformContext';
import { MessengerProvider, useMessenger } from '../context/MessengerContext';
import { useFirstWorkspaceModal } from '../context/FirstWorkspaceModalContext';
import { GamificationProvider } from '../../shared/context/GamificationContext';

const useIsMobile = (breakpoint = 768) => {
  const [isMobile, setIsMobile] = useState(() => typeof window !== 'undefined' && window.innerWidth < breakpoint);

  useEffect(() => {
    const mq = window.matchMedia(`(max-width: ${breakpoint - 1}px)`);
    const handler = (e) => setIsMobile(e.matches);
    mq.addEventListener('change', handler);
    setIsMobile(mq.matches);
    return () => mq.removeEventListener('change', handler);
  }, [breakpoint]);

  return isMobile;
};

const DashboardLayoutContent = ({ children }) => {
  const navigate = useNavigate();
  const isMobile = useIsMobile();
  const [isSidebarOpen, setIsSidebarOpen] = useState(!isMobile);
  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);
  const [isDocumentHidden, setIsDocumentHidden] = useState(() => (typeof document !== 'undefined' ? document.hidden : false));
  const autoPiPRequestedRef = useRef(false);
  const tabHiddenPiPRef = useRef(false);
  const browserPiPRequestedRef = useRef(false);
  const pipVideoRef = useRef(null);
  const { isOpen: isModalOpen } = useFirstWorkspaceModal();
  const { activePage, currentUser, setActivePage } = usePlatform();
  const messengerCtx = useMessenger();
  const incomingCall = messengerCtx?.incomingCall;
  const activeCallSession = messengerCtx?.activeCallSession;
  const isPiP = messengerCtx?.isPiP;
  const setIsPiP = messengerCtx?.setIsPiP;
  const localStream = messengerCtx?.localStream;
  const remoteStreams = messengerCtx?.remoteStreams;
  const callParticipants = messengerCtx?.callParticipants || [];

  const preferredPiPStream = useMemo(() => {
    const findRemoteStream = () => {
      const remoteParticipant = callParticipants.find((participant) => !isSameId(participant.userId, currentUser?.id));
      if (!remoteParticipant || !remoteStreams) {
        return null;
      }

      const direct = remoteStreams[remoteParticipant.userId];
      if (direct) {
        return direct;
      }

      const matchedKey = Object.keys(remoteStreams).find((key) => isSameId(key, remoteParticipant.userId));
      return matchedKey ? remoteStreams[matchedKey] : null;
    };

    return findRemoteStream() || localStream || null;
  }, [callParticipants, currentUser?.id, localStream, remoteStreams]);

  const pipUsesLocalStreamOnly = preferredPiPStream != null && preferredPiPStream === localStream;

  const canUseNativePiP = typeof document !== 'undefined'
    && typeof HTMLVideoElement !== 'undefined'
    && Boolean(document.pictureInPictureEnabled);

  useEffect(() => {
    if (!pipVideoRef.current) {
      return;
    }

    pipVideoRef.current.srcObject = preferredPiPStream || null;
  }, [preferredPiPStream]);

  useEffect(() => {
    if (typeof document === 'undefined') {
      return undefined;
    }

    const onVisibilityChange = () => {
      setIsDocumentHidden(document.hidden);
    };

    document.addEventListener('visibilitychange', onVisibilityChange);
    return () => document.removeEventListener('visibilitychange', onVisibilityChange);
  }, []);

  useEffect(() => {
    if (!activeCallSession) {
      autoPiPRequestedRef.current = false;
      tabHiddenPiPRef.current = false;
      browserPiPRequestedRef.current = false;
      return;
    }

    if (activePage !== 'messenger' && !isPiP) {
      setIsPiP?.(true);
      autoPiPRequestedRef.current = true;
      return;
    }

    if (activePage === 'messenger' && autoPiPRequestedRef.current && isPiP && !isDocumentHidden) {
      setIsPiP?.(false);
      autoPiPRequestedRef.current = false;
    }
  }, [activeCallSession, activePage, isDocumentHidden, isPiP, setIsPiP]);

  useEffect(() => {
    const videoEl = pipVideoRef.current;
    if (!videoEl) {
      return undefined;
    }

    const handleLeavePiP = () => {
      browserPiPRequestedRef.current = false;
      if (activeCallSession && autoPiPRequestedRef.current) {
        autoPiPRequestedRef.current = false;
        setIsPiP?.(false);
        setActivePage('messenger');
        navigate('/messenger');
      }
    };

    videoEl.addEventListener('leavepictureinpicture', handleLeavePiP);
    return () => videoEl.removeEventListener('leavepictureinpicture', handleLeavePiP);
  }, [activeCallSession, navigate, setActivePage, setIsPiP]);

  useEffect(() => {
    const tryEnterNativePiP = async () => {
      if (!canUseNativePiP || !pipVideoRef.current || !preferredPiPStream) {
        return;
      }

      if (document.pictureInPictureElement) {
        browserPiPRequestedRef.current = true;
        return;
      }

      try {
        await pipVideoRef.current.play();
        await pipVideoRef.current.requestPictureInPicture();
        browserPiPRequestedRef.current = true;
      } catch {
        browserPiPRequestedRef.current = false;
      }
    };

    const tryExitNativePiP = async () => {
      if (!canUseNativePiP) {
        browserPiPRequestedRef.current = false;
        return;
      }

      if (!document.pictureInPictureElement) {
        browserPiPRequestedRef.current = false;
        return;
      }

      try {
        await document.exitPictureInPicture();
      } catch {
        // no-op
      } finally {
        browserPiPRequestedRef.current = false;
      }
    };

    if (!activeCallSession) {
      void tryExitNativePiP();
      return;
    }

    if (isDocumentHidden) {
      if (!isPiP) {
        setIsPiP?.(true);
      }
      tabHiddenPiPRef.current = true;
      void tryEnterNativePiP();
      return;
    }

    if (!tabHiddenPiPRef.current) {
      return;
    }

    tabHiddenPiPRef.current = false;
    setIsPiP?.(false);
    void tryExitNativePiP();
    setActivePage('messenger');
    navigate('/messenger');
  }, [
    activeCallSession,
    canUseNativePiP,
    isDocumentHidden,
    isPiP,
    navigate,
    preferredPiPStream,
    setActivePage,
    setIsPiP,
  ]);

  // Auto-close sidebar when switching to mobile
  useEffect(() => {
    if (isMobile) setIsSidebarOpen(false);
    else setIsSidebarOpen(true);
  }, [isMobile]);

  const toggleSidebar = useCallback(() => setIsSidebarOpen((prev) => !prev), []);

  // Close sidebar on navigation when mobile
  const handleMobileNavClose = useCallback(() => {
    if (isMobile) setIsSidebarOpen(false);
  }, [isMobile]);

  const blurActive = isModalOpen && activePage === 'dashboard';
  const blurStyle = {
    filter: blurActive ? 'blur(8px)' : 'none',
    transition: 'filter 0.3s ease-in-out',
    pointerEvents: blurActive ? 'none' : 'auto',
  };

  return (
    <div
      className="platform-layout flex h-screen w-screen overflow-x-hidden overflow-y-auto transition-colors duration-500"
      style={{
        backgroundColor: 'var(--app-bg, #FFFFFF)',
        color: 'var(--app-text-main, #1A202C)',
      }}
    >
      {/* Mobile overlay backdrop */}
      {isMobile && isSidebarOpen && (
        <div
          className="fixed inset-0 z-30 bg-black/40 backdrop-blur-sm"
          onClick={() => setIsSidebarOpen(false)}
        />
      )}

      {/* Sidebar */}
      <div
        className={isMobile ? `fixed inset-y-0 left-0 z-40 transition-transform duration-300 ${isSidebarOpen ? 'translate-x-0' : '-translate-x-full'}` : ''}
        style={isMobile ? {} : blurStyle}
      >
        <Sidebar
          isOpen={isMobile ? true : isSidebarOpen}
          isMobile={isMobile}
          toggleSidebar={toggleSidebar}
          onNavClick={handleMobileNavClose}
        />
      </div>

      {/* Main content */}
      <div
        className="flex-1 flex flex-col min-w-0 relative z-10"
        style={blurStyle}
      >
        {activePage === 'dashboard' && (
          <TopBar
            onCreateClick={() => setIsCreateModalOpen(true)}
            isMobile={isMobile}
            onMenuClick={toggleSidebar}
          />
        )}

        {/* Mobile header for non-dashboard pages */}
        {activePage !== 'dashboard' && isMobile && (
          <div
            className="flex h-12 shrink-0 items-center gap-3 border-b px-4"
            style={{ borderBottomColor: 'var(--app-border)', backgroundColor: 'var(--app-layer-surface)' }}
          >
            <button
              type="button"
              onClick={toggleSidebar}
              className="flex h-8 w-8 items-center justify-center rounded-lg"
              style={{ color: 'var(--app-text-main)' }}
            >
              <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <line x1="3" y1="6" x2="21" y2="6" /><line x1="3" y1="12" x2="21" y2="12" /><line x1="3" y1="18" x2="21" y2="18" />
              </svg>
            </button>
          </div>
        )}

        {children}
        <video
          ref={pipVideoRef}
          autoPlay
          playsInline
          muted={pipUsesLocalStreamOnly}
          className="fixed -left-[9999px] -top-[9999px] w-1 h-1 opacity-0 pointer-events-none"
        />
      </div>

      {isCreateModalOpen && (
        <CreateTaskModal onClose={() => setIsCreateModalOpen(false)} />
      )}

      {incomingCall && <IncomingCallModal />}
      {activeCallSession && isPiP && <PictureInPicture />}

      <Toaster
        position="top-right"
        toastOptions={{
          duration: 4500,
          style: {
            background: 'var(--app-layer-overlay)',
            color: 'var(--app-text-main)',
            border: '1px solid var(--app-border)',
          },
        }}
      />
    </div>
  );
};

function isSameId(a, b) {
  if (a == null || b == null) {
    return false;
  }

  return String(a).toLowerCase() === String(b).toLowerCase();
}

const DashboardLayout = ({ children }) => (
  <PlatformProvider>
    <GamificationProvider>
      <MessengerProvider>
        <DashboardLayoutContent>{children}</DashboardLayoutContent>
      </MessengerProvider>
    </GamificationProvider>
  </PlatformProvider>
);

export default DashboardLayout;
