import React, { useRef, useEffect, useState, useMemo, useCallback } from 'react';
import { Mic, MicOff, Video, VideoOff, PhoneOff, Maximize2, Users } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';

import { useMessenger } from '../../context/MessengerContext';
import { usePlatform } from '../../context/PlatformContext';

const PIP_WIDTH = 320;
const PIP_HEIGHT = 200;

const PictureInPicture = () => {
  const { t } = useTranslation('platform');
  const navigate = useNavigate();
  const {
    activeCallSession,
    localStream,
    remoteStreams,
    callParticipants,
    toggleAudio,
    toggleVideo,
    endCall,
    leaveCall,
    setIsPiP,
    selectChat,
  } = useMessenger();
  const { currentUser, users, setActivePage } = usePlatform();

  const [position, setPosition] = useState({ x: window.innerWidth - PIP_WIDTH - 20, y: window.innerHeight - PIP_HEIGHT - 40 });
  const [dragging, setDragging] = useState(false);
  const dragOffset = useRef({ x: 0, y: 0 });
  const mainVideoRef = useRef(null);
  const localVideoRef = useRef(null);

  const localHasVideo = localStream?.getVideoTracks().some((track) => track.enabled);
  const localHasAudio = localStream?.getAudioTracks().some((track) => track.enabled);
  const isGroupCall = Boolean(activeCallSession?.isGroup);

  // All remote participants
  const remoteParticipants = useMemo(() => {
    return callParticipants.filter((p) => p.userId !== currentUser?.id);
  }, [callParticipants, currentUser?.id]);

  // All remote streams for audio playback
  const allRemoteStreams = useMemo(() => {
    return remoteParticipants
      .map((p) => ({ userId: p.userId, stream: getStreamByUserId(remoteStreams, p.userId) }))
      .filter((entry) => entry.stream != null);
  }, [remoteParticipants, remoteStreams]);

  // Pick the "active speaker" or first remote with video to display
  const displayParticipant = useMemo(() => {
    // Prefer participant with live video
    const withVideo = remoteParticipants.find((p) => {
      const stream = getStreamByUserId(remoteStreams, p.userId);
      return stream?.getVideoTracks().some((track) => track.readyState === 'live' && track.enabled !== false);
    });
    return withVideo || remoteParticipants[0] || null;
  }, [remoteParticipants, remoteStreams]);

  const displayStream = displayParticipant ? getStreamByUserId(remoteStreams, displayParticipant.userId) : null;
  const displayHasLiveVideo = displayStream?.getVideoTracks().some((track) => track.readyState === 'live' && track.enabled !== false) ?? false;

  const displayUser = displayParticipant ? users?.find((user) => isSameId(user.id, displayParticipant.userId)) : null;
  const displayName =
    displayParticipant?.displayName
    || displayParticipant?.username
    || displayUser?.name
    || t('messenger.call.unknownParticipant');

  // Attach main display video
  useEffect(() => {
    if (mainVideoRef.current) {
      mainVideoRef.current.srcObject = displayStream || null;
    }
  }, [displayStream]);

  // Attach local video
  useEffect(() => {
    if (localVideoRef.current && localStream) {
      localVideoRef.current.srcObject = localStream;
    }
  }, [localStream]);

  // Drag handlers
  const onMouseDown = (event) => {
    if (event.target.closest('button')) return;
    setDragging(true);
    dragOffset.current = { x: event.clientX - position.x, y: event.clientY - position.y };
  };

  useEffect(() => {
    if (!dragging) return undefined;

    const onMove = (event) => {
      setPosition({
        x: Math.max(0, Math.min(window.innerWidth - PIP_WIDTH, event.clientX - dragOffset.current.x)),
        y: Math.max(0, Math.min(window.innerHeight - PIP_HEIGHT, event.clientY - dragOffset.current.y)),
      });
    };

    const onUp = () => setDragging(false);
    window.addEventListener('mousemove', onMove);
    window.addEventListener('mouseup', onUp);
    return () => {
      window.removeEventListener('mousemove', onMove);
      window.removeEventListener('mouseup', onUp);
    };
  }, [dragging]);

  const handleExpand = useCallback(() => {
    selectChat(null);
    setIsPiP(false);
    setActivePage('messenger');
    navigate('/messenger');
  }, [selectChat, setIsPiP, setActivePage, navigate]);

  return (
    <div
      onMouseDown={onMouseDown}
      className="fixed z-[998] rounded-2xl overflow-hidden shadow-2xl cursor-move select-none"
      style={{
        left: position.x,
        top: position.y,
        width: PIP_WIDTH,
        height: PIP_HEIGHT,
        backgroundColor: '#111',
        border: '2px solid rgba(255,255,255,0.1)',
      }}
    >
      {/* Audio elements for ALL remote participants */}
      {allRemoteStreams.map((entry) => (
        <RemoteAudio key={entry.userId} stream={entry.stream} />
      ))}

      {/* Main video display */}
      {displayHasLiveVideo && displayStream ? (
        <video ref={mainVideoRef} autoPlay playsInline muted className="w-full h-full object-cover" />
      ) : (
        <div className="w-full h-full flex items-center justify-center">
          <div className="text-center">
            <div
              className="w-12 h-12 rounded-full flex items-center justify-center text-sm font-bold mx-auto"
              style={{ backgroundColor: stringToColor(displayName), color: '#fff' }}
            >
              {getInitials(displayName)}
            </div>
            <span className="text-white/60 text-xs mt-1 block">{displayName}</span>
          </div>
        </div>
      )}

      {/* Participant count badge for group calls */}
      {isGroupCall && remoteParticipants.length > 1 && (
        <div className="absolute top-2 left-2 flex items-center gap-1 bg-black/50 text-white/80 text-[10px] px-1.5 py-0.5 rounded backdrop-blur-sm">
          <Users size={10} />
          {callParticipants.length}
        </div>
      )}

      {/* Local camera preview */}
      {localHasVideo && localStream && (
        <div className="absolute top-2 right-2 w-16 h-12 rounded-lg overflow-hidden border border-white/20">
          <video ref={localVideoRef} autoPlay playsInline muted className="w-full h-full object-cover" />
        </div>
      )}

      {/* Controls */}
      <div
        className="absolute bottom-0 left-0 right-0 flex items-center justify-center gap-2 py-1.5"
        style={{ backgroundColor: 'rgba(0,0,0,0.6)', backdropFilter: 'blur(4px)' }}
      >
        <button
          onClick={toggleAudio}
          className="p-1.5 rounded-full transition-all text-white/80 hover:text-white hover:bg-white/10"
          title={localHasAudio ? t('messenger.call.toggleMicOff') : t('messenger.call.toggleMicOn')}
        >
          {localHasAudio ? <Mic size={14} /> : <MicOff size={14} />}
        </button>
        <button
          onClick={toggleVideo}
          className="p-1.5 rounded-full transition-all text-white/80 hover:text-white hover:bg-white/10"
          title={localHasVideo ? t('messenger.call.toggleCameraOff') : t('messenger.call.toggleCameraOn')}
        >
          {localHasVideo ? <Video size={14} /> : <VideoOff size={14} />}
        </button>
        <button
          onClick={handleExpand}
          className="p-1.5 rounded-full transition-all text-white/80 hover:text-white hover:bg-white/10"
          title={t('messenger.call.expand')}
        >
          <Maximize2 size={14} />
        </button>
        <button
          onClick={isGroupCall ? leaveCall : endCall}
          className="p-1.5 rounded-full bg-red-500 hover:bg-red-600 text-white transition-all"
          title={isGroupCall ? t('messenger.call.leaveCall') : t('messenger.call.endCall')}
        >
          <PhoneOff size={14} />
        </button>
      </div>
    </div>
  );
};

/** Renders a hidden <audio> element for one remote stream */
const RemoteAudio = ({ stream }) => {
  const audioRef = useRef(null);

  useEffect(() => {
    if (audioRef.current) {
      audioRef.current.srcObject = stream || null;
    }
  }, [stream]);

  return <audio ref={audioRef} autoPlay playsInline className="hidden" />;
};

function isSameId(a, b) {
  if (a == null || b == null) return false;
  return String(a).toLowerCase() === String(b).toLowerCase();
}

function getStreamByUserId(streams, userId) {
  if (!streams || userId == null) return null;
  const direct = streams[userId];
  if (direct) return direct;
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

export default PictureInPicture;
