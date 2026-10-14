export const buildFallbackAvatar = (seed = 'collaby-user') =>
  `https://api.dicebear.com/7.x/avataaars/svg?seed=${encodeURIComponent(seed || 'collaby-user')}`;

export const getAvatarSeed = ({
  seed,
  userId,
  displayName,
  username,
  email,
  fallback = 'collaby-user',
} = {}) => seed || displayName || username || email || userId || fallback;

export const resolveUserAvatar = ({
  avatarUrl,
  seed,
  userId,
  displayName,
  username,
  email,
  fallback = 'collaby-user',
} = {}) => {
  if (avatarUrl) {
    return avatarUrl;
  }

  return buildFallbackAvatar(getAvatarSeed({
    seed,
    userId,
    displayName,
    username,
    email,
    fallback,
  }));
};
