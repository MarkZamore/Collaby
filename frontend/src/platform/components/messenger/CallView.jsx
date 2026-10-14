import React, { useRef, useEffect, useState, useCallback } from 'react';
import {
  Mic,
  MicOff,
  Video,
  VideoOff,
  PhoneOff,
  Monitor,
  MonitorOff,
  Minimize2,
  Users,
} from 'lucide-react';
import { useTranslation } from 'react-i18next';

import { useMessenger } from '../../context/MessengerContext';
import { usePlatform } from '../../context/PlatformContext';
import { cn } from '../../../shared/utils/cn';

const CallView = () => {
  const { t } = useTranslation('platform');
  const {
    activeCallSession,
    callParticipants,
    localStream,
    remoteStreams,
    isScreenSharing,
    toggleVideo,
    toggleAudio,
    toggleScreenShare,
    endCall,
    leaveCall,
    setIsPiP,
  } = useMessenger();
  const { currentUser } = usePlatform();

  const localHasVideo = localStream?.getVideoTracks().some((track) => track.enabled);
  const localHasAudio = localStream?.getAudioTracks().some((track) => track.enabled);
  const isGroupCall = Boolean(activeCallSession?.isGroup);
  const remoteParticipants = callParticipants.filter((participant) => participant.userId !== currentUser?.id);
  const totalTiles = 1 + remoteParticipants.length;

  const gridRef = useRef(null);
  const [isNarrowGrid, setIsNarrowGrid] = useState(false);

  useEffect(() => {
    const element = gridRef.current;
    if (!element) return undefined;

    const observer = new ResizeObserver((entries) => {
      for (const entry of entries) {
        setIsNarrowGrid(entry.contentRect.width < 500);
      }
    });

    observer.observe(element);
    return () => observer.disconnect();
  }, []);

  return (
    <div className="flex-1 flex flex-col min-h-0 h-full" style={{ backgroundColor: 'var(--app-layer-base)' }}>
      {/* Header - always visible */}
      <div className="flex items-center justify-between px-2 sm:px-4 py-1.5 sm:py-2 shrink-0" style={{ backgroundColor: 'var(--app-layer-surface)', borderBottom: '1px solid var(--app-border)' }}>
        <div className="flex items-center gap-1.5 sm:gap-2">
          <Users size={16} style={{ color: 'var(--app-text-secondary)' }} />
          <span className="text-xs sm:text-sm font-medium" style={{ color: 'var(--app-text-main)' }}>
            {t('messenger.call.header', { count: callParticipants.length })}
          </span>
        </div>
        <button
          onClick={() => setIsPiP(true)}
          className="p-1.5 rounded-lg transition-all"
          style={{ color: 'var(--app-text-secondary)' }}
          title={t('messenger.call.expand')}
        >
          <Minimize2 size={16} />
        </button>
      </div>

      {/* Video grid - fills remaining space, never pushes controls off */}
      <div ref={gridRef} className={cn('flex-1 min-h-0 p-2 gap-2', getGridClass(totalTiles, isNarrowGrid))}>
        <VideoTile
          stream={localStream}
          hasVideo={localHasVideo}
          hasAudio={localHasAudio}
          name={currentUser?.name || t('messenger.call.selfName')}
          isLocal
          isMuted
          isScreenSharing={isScreenSharing}
          selfLabel={t('messenger.call.selfName')}
        />

        {remoteParticipants.map((participant) => {
          const fullParticipant = callParticipants.find((item) => item.userId === participant.userId);
          const participantStream = getStreamByUserId(remoteStreams, participant.userId);
          const displayName =
            fullParticipant?.displayName ||
            fullParticipant?.username ||
            t('messenger.call.unknownParticipant');

          return (
            <VideoTile
              key={participant.userId}
              stream={participantStream}
              hasVideo={participant.hasVideo}
              hasAudio={participant.hasAudio}
              name={displayName}
              isLocal={false}
              isScreenSharing={participant.isScreenSharing}
              selfLabel={t('messenger.call.selfName')}
            />
          );
        })}
      </div>

      {/* Controls - always visible at bottom */}
      <div className="flex items-center justify-center gap-2 sm:gap-3 py-2 sm:py-3 shrink-0" style={{ backgroundColor: 'var(--app-layer-surface)', borderTop: '1px solid var(--app-border)' }}>
        <ControlButton
          icon={localHasAudio ? Mic : MicOff}
          label={localHasAudio ? t('messenger.call.toggleMicOff') : t('messenger.call.toggleMicOn')}
          active={localHasAudio}
          onClick={toggleAudio}
        />
        <ControlButton
          icon={localHasVideo ? Video : VideoOff}
          label={localHasVideo ? t('messenger.call.toggleCameraOff') : t('messenger.call.toggleCameraOn')}
          active={localHasVideo}
          onClick={toggleVideo}
        />
        <ControlButton
          icon={isScreenSharing ? MonitorOff : Monitor}
          label={isScreenSharing ? t('messenger.call.stopShareScreen') : t('messenger.call.shareScreen')}
          active={isScreenSharing}
          onClick={toggleScreenShare}
          color={isScreenSharing ? '#22c55e' : undefined}
        />

        <button
          onClick={isGroupCall ? leaveCall : endCall}
          className="p-2.5 rounded-full transition-all hover:opacity-85"
          style={{ backgroundColor: '#ef4444', color: '#fff', boxShadow: '0 2px 8px rgba(239,68,68,0.4)' }}
          title={isGroupCall ? t('messenger.call.leaveCall') : t('messenger.call.endCall')}
        >
          <PhoneOff size={18} />
        </button>
      </div>
    </div>
  );
};

const VideoTile = ({ stream, hasVideo, hasAudio, name, isLocal, isMuted, isScreenSharing, selfLabel }) => {
  const videoRef = useRef(null);
  const audioRef = useRef(null);

  useEffect(() => {
    if (videoRef.current) {
      videoRef.current.srcObject = stream || null;
    }
  }, [stream]);

  useEffect(() => {
    if (!isLocal && audioRef.current) {
      audioRef.current.srcObject = stream || null;
    }
  }, [isLocal, stream]);

  const localHasLiveVideo = stream?.getVideoTracks().some((track) => track.readyState === 'live' && track.enabled) ?? false;
  const remoteHasLiveVideo = stream?.getVideoTracks().some((track) => track.readyState === 'live' && track.enabled !== false) ?? false;
  const actualHasVideo = isLocal ? localHasLiveVideo : remoteHasLiveVideo;
  const showVideo = actualHasVideo;

  return (
    <div
      className="relative rounded-xl overflow-hidden flex items-center justify-center min-h-0 min-w-0"
      style={{ backgroundColor: 'var(--app-layer-overlay, var(--app-layer-base))', border: '1px solid var(--app-border)' }}
    >
      <video
        ref={videoRef}
        autoPlay
        playsInline
        muted={isLocal || isMuted}
        className="w-full h-full object-contain"
        style={{ display: showVideo ? 'block' : 'none' }}
      />

      {!isLocal && <audio ref={audioRef} autoPlay playsInline className="hidden" />}

      {!showVideo && (
        <div className="flex flex-col items-center gap-2">
          <div
            className="w-14 h-14 rounded-full flex items-center justify-center text-lg font-bold"
            style={{ backgroundColor: stringToColor(name), color: '#fff' }}
          >
            {getInitials(name)}
          </div>
          <span className="text-xs" style={{ color: 'var(--app-text-secondary)' }}>{name}</span>
        </div>
      )}

      <div className="absolute bottom-1.5 left-1.5 right-1.5 flex items-center justify-between">
        <span className="text-[10px] px-1.5 py-0.5 rounded backdrop-blur-sm truncate max-w-[70%]"
          style={{ backgroundColor: 'var(--app-layer-surface)', color: 'var(--app-text-main)', opacity: 0.85 }}>
          {isLocal ? `${name} (${selfLabel})` : name}
          {isScreenSharing && ' 🖥'}
        </span>
        {hasAudio === false && (
          <span className="bg-red-500/80 p-0.5 rounded">
            <MicOff size={10} className="text-white" />
          </span>
        )}
      </div>
    </div>
  );
};

const ControlButton = ({ icon: Icon, label, active, onClick, color }) => (
  <button
    onClick={onClick}
    title={label}
    className="p-2.5 rounded-full transition-all hover:opacity-85"
    style={color
      ? { backgroundColor: `${color}33`, color }
      : {
          backgroundColor: active ? 'var(--app-accent)' : 'var(--app-layer-surface)',
          color: active ? 'var(--app-accent-text, #fff)' : 'var(--app-text-secondary)',
          border: '2px solid var(--app-border)',
          boxShadow: '0 1px 3px rgba(0,0,0,0.1)',
        }
    }
  >
    <Icon size={18} />
  </button>
);

function getGridClass(count, narrow) {
  if (count <= 1) return 'grid grid-cols-1 grid-rows-1';
  if (count <= 2) return narrow ? 'grid grid-cols-1 grid-rows-2' : 'grid grid-cols-2 grid-rows-1';
  if (count <= 4) return 'grid grid-cols-2 grid-rows-2';
  if (count <= 6) return narrow ? 'grid grid-cols-2 grid-rows-3' : 'grid grid-cols-3 grid-rows-2';
  return narrow ? 'grid grid-cols-2 grid-rows-auto' : 'grid grid-cols-3 grid-rows-3';
}

function isSameId(a, b) {
  if (a == null || b == null) {
    return false;
  }

  return String(a).toLowerCase() === String(b).toLowerCase();
}

function getStreamByUserId(streams, userId) {
  if (!streams || userId == null) {
    return null;
  }

  const direct = streams[userId];
  if (direct) {
    return direct;
  }

  const matchedKey = Object.keys(streams).find((key) => isSameId(key, userId));
  return matchedKey ? streams[matchedKey] : null;
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

  return `hsl(${Math.abs(hash % 360)}, 55%, 50%)`;
}

export default CallView;
