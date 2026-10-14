import { describe, expect, it } from 'vitest';

import { getEquippedAvatarFrame } from './avatarFrames';

describe('avatar frame resolution', () => {
  it('returns the fire frame only when it is equipped in frame inventory', () => {
    const frame = getEquippedAvatarFrame([
      {
        itemId: 'a0000001-0000-0000-0000-000000000003',
        category: 'frames',
        isEquipped: true,
      },
    ]);

    expect(frame?.key).toBe('fireFrame');
  });

  it('ignores owned but unequipped avatar frames', () => {
    const frame = getEquippedAvatarFrame([
      {
        itemId: 'a0000001-0000-0000-0000-000000000003',
        category: 'frames',
        isEquipped: false,
      },
    ]);

    expect(frame).toBeNull();
  });
});
