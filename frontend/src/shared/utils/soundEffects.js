const SOUND_PATHS = {
  ringtone: '/sounds/ringtone.mp3',
  callEnded: '/sounds/call-ended.mp3',
  micMuted: '/sounds/mic-muted.mp3',
  micUnmuted: '/sounds/mic-unmuted.mp3',
  cameraOn: '/sounds/camera-on.mp3',
  cameraOff: '/sounds/camera-off.mp3',
};

const clampVolume = (volume) => {
  if (typeof volume !== 'number' || Number.isNaN(volume)) {
    return 1;
  }

  return Math.max(0, Math.min(1, volume));
};

const createAudio = (name, { loop = false, volume = 1 } = {}) => {
  const src = SOUND_PATHS[name];
  if (!src || typeof window === 'undefined') {
    return null;
  }

  const audio = new Audio(src);
  audio.preload = 'auto';
  audio.loop = loop;
  audio.volume = clampVolume(volume);
  return audio;
};

export const playSoundEffect = async (name, options = {}) => {
  const audio = createAudio(name, options);
  if (!audio) {
    return false;
  }

  try {
    audio.currentTime = 0;
    await audio.play();
    return true;
  } catch {
    return false;
  }
};

export const startLoopSound = async (name, options = {}) => {
  const audio = createAudio(name, { ...options, loop: true });
  if (!audio) {
    return null;
  }

  try {
    audio.currentTime = 0;
    await audio.play();
    return audio;
  } catch {
    return null;
  }
};

export const stopLoopSound = (audio) => {
  if (!audio) {
    return;
  }

  audio.pause();
  audio.currentTime = 0;
};
