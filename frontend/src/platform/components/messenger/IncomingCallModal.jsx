import React, { useCallback, useEffect, useRef } from 'react';
import { Phone, PhoneOff, Video } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';

import { useMessenger } from '../../context/MessengerContext';
import { usePlatform } from '../../context/PlatformContext';
import { startLoopSound, stopLoopSound } from '../../../shared/utils/soundEffects';

const IncomingCallModal = () => {
  const { t } = useTranslation('platform');
  const navigate = useNavigate();
  const { incomingCall, answerCall, declineCall } = useMessenger();
  const { users, setActivePage } = usePlatform();
  const ringtoneAudioRef = useRef(null);
  const audioContextRef = useRef(null);
  const ringIntervalRef = useRef(null);

  const stopRingtone = useCallback(() => {
    stopLoopSound(ringtoneAudioRef.current);
    ringtoneAudioRef.current = null;

    if (ringIntervalRef.current) {
      clearInterval(ringIntervalRef.current);
      ringIntervalRef.current = null;
    }

    const context = audioContextRef.current;
    if (context && context.state !== 'closed') {
      context.close().catch(() => {});
    }
    audioContextRef.current = null;
  }, []);

  const playTone = useCallback((context, frequency, startAt) => {
    const oscillator = context.createOscillator();
    const gainNode = context.createGain();

    oscillator.type = 'sine';
    oscillator.frequency.setValueAtTime(frequency, startAt);

    gainNode.gain.setValueAtTime(0.0001, startAt);
    gainNode.gain.exponentialRampToValueAtTime(0.16, startAt + 0.02);
    gainNode.gain.exponentialRampToValueAtTime(0.0001, startAt + 0.22);

    oscillator.connect(gainNode);
    gainNode.connect(context.destination);
    oscillator.start(startAt);
    oscillator.stop(startAt + 0.24);
  }, []);

  const startRingtone = useCallback(async () => {
    stopRingtone();

    const ringtoneAudio = await startLoopSound('ringtone', { volume: 0.9 });
    if (ringtoneAudio) {
      ringtoneAudioRef.current = ringtoneAudio;
      return;
    }

    const AudioContextCtor = window.AudioContext || window.webkitAudioContext;
    if (!AudioContextCtor) {
      return;
    }

    const context = new AudioContextCtor();
    audioContextRef.current = context;

    try {
      await context.resume();
    } catch {
      // Браузер может заблокировать автозапуск звука до пользовательского действия.
      return;
    }

    const ringOnce = () => {
      const startAt = context.currentTime + 0.02;
      playTone(context, 880, startAt);
      playTone(context, 660, startAt + 0.28);
    };

    ringOnce();
    ringIntervalRef.current = setInterval(ringOnce, 1400);
  }, [playTone, stopRingtone]);

  useEffect(() => {
    if (!incomingCall) {
      stopRingtone();
      return undefined;
    }

    void startRingtone();

    return () => {
      stopRingtone();
    };
  }, [incomingCall, startRingtone, stopRingtone]);

  if (!incomingCall) {
    return null;
  }

  const caller = users?.find((user) => isSameId(user.id, incomingCall.initiatorId));
  const callerParticipant = incomingCall.participants?.find((participant) =>
    isSameId(participant.userId, incomingCall.initiatorId),
  );
  const callerName =
    callerParticipant?.displayName
    || callerParticipant?.username
    || caller?.name
    || t('messenger.incomingCall.unknownCaller');
  const isVideo = incomingCall.type === 'video';

  const handleDecline = () => {
    stopRingtone();
    declineCall(incomingCall.id);
  };

  const handleAnswer = async (hasVideo) => {
    stopRingtone();
    const answered = await answerCall(incomingCall.id, hasVideo);
    if (answered) {
      setActivePage('messenger');
      navigate('/messenger');
    }
  };

  return (
    <div
      className="fixed inset-0 z-[1000] flex items-center justify-center"
      style={{ backgroundColor: 'rgba(0,0,0,0.7)', backdropFilter: 'blur(8px)' }}
    >
      <div
        className="flex flex-col items-center gap-6 p-8 rounded-2xl shadow-2xl"
        style={{ backgroundColor: 'var(--app-layer-overlay, #222)', border: '1px solid var(--app-border)' }}
      >
        <div
          className="w-20 h-20 rounded-full flex items-center justify-center text-2xl font-bold animate-pulse"
          style={{ backgroundColor: stringToColor(callerName), color: '#fff' }}
        >
          {getInitials(callerName)}
        </div>

        <div className="text-center">
          <div className="text-lg font-semibold" style={{ color: 'var(--app-text-main)' }}>
            {callerName}
          </div>
          <div className="text-sm mt-1" style={{ color: 'var(--app-text-secondary)' }}>
            {isVideo ? t('messenger.incomingCall.video') : t('messenger.incomingCall.audio')}
          </div>
        </div>

        <div className="flex items-center gap-6">
          <button
            onClick={handleDecline}
            className="p-4 rounded-full transition-all hover:opacity-85"
            style={{ backgroundColor: '#ef4444', color: '#fff', boxShadow: '0 2px 10px rgba(239,68,68,0.4)' }}
            title={t('messenger.incomingCall.decline')}
          >
            <PhoneOff size={24} />
          </button>

          <button
            onClick={() => handleAnswer(false)}
            className="p-4 rounded-full transition-all hover:opacity-85"
            style={{ backgroundColor: '#22c55e', color: '#fff', boxShadow: '0 2px 10px rgba(34,197,94,0.4)' }}
            title={t('messenger.incomingCall.answerAudio')}
          >
            <Phone size={24} />
          </button>

          {isVideo && (
            <button
              onClick={() => handleAnswer(true)}
              className="p-4 rounded-full transition-all hover:opacity-85"
              style={{ backgroundColor: '#3b82f6', color: '#fff', boxShadow: '0 2px 10px rgba(59,130,246,0.4)' }}
              title={t('messenger.incomingCall.answerVideo')}
            >
              <Video size={24} />
            </button>
          )}
        </div>
      </div>
    </div>
  );
};

function isSameId(a, b) {
  if (a == null || b == null) {
    return false;
  }

  return String(a).toLowerCase() === String(b).toLowerCase();
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

export default IncomingCallModal;
