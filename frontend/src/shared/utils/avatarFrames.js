export const AVATAR_FRAME_ITEM_IDS = {
  fireFrame: 'a0000001-0000-0000-0000-000000000003',
};

export const AVATAR_FRAMES = {
  fireFrame: {
    id: AVATAR_FRAME_ITEM_IDS.fireFrame,
    key: 'fireFrame',
    label: 'Fire Frame',
    ring: 'conic-gradient(from 210deg, #ff1744, #ff6b35, #ffd166, #ff8c42, #ff1744)',
    aura: 'radial-gradient(circle, rgba(255, 107, 53, 0.42) 0%, rgba(255, 23, 68, 0.18) 48%, transparent 72%)',
    glow: '0 0 0 1px rgba(255, 255, 255, 0.35), 0 0 18px rgba(255, 107, 53, 0.55), 0 0 32px rgba(255, 23, 68, 0.32)',
  },
};

export const getEquippedAvatarFrame = (inventory = []) => {
  if (!Array.isArray(inventory)) {
    return null;
  }

  const equippedFrame = inventory.find((item) => {
    const itemId = String(item?.itemId || item?.id || '').toLowerCase();
    const category = String(item?.category || '').toLowerCase();
    return item?.isEquipped && category === 'frames' && itemId === AVATAR_FRAME_ITEM_IDS.fireFrame;
  });

  return equippedFrame ? AVATAR_FRAMES.fireFrame : null;
};
