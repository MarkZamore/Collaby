import React from 'react';
import { cn } from '../utils/cn';

const AvatarWithFrame = ({
  src,
  alt,
  frame = null,
  sizeClassName = 'h-10 w-10',
  wrapperClassName = '',
  noFrameShellClassName = '',
  noFrameInnerClassName = '',
  imageClassName = '',
  imageTestId,
  onError,
}) => {
  const hasFrame = Boolean(frame);

  return (
    <div
      className={cn('relative shrink-0 rounded-full', sizeClassName, wrapperClassName)}
      data-avatar-frame={frame?.key || 'none'}
      title={frame?.label}
    >
      {hasFrame && (
        <>
          <span
            aria-hidden="true"
            className="absolute -inset-2 rounded-full opacity-75 blur-md"
            style={{ background: frame.aura }}
          />
          <span
            aria-hidden="true"
            className="absolute -inset-1 rounded-full"
            style={{
              background: frame.ring,
              boxShadow: frame.glow,
            }}
          />
        </>
      )}

      <div
        className={cn(
          'relative z-10 h-full w-full rounded-full',
          hasFrame ? 'p-[3px]' : noFrameShellClassName,
        )}
        style={hasFrame ? { background: frame.ring } : undefined}
      >
        <div
          className={cn(
            'h-full w-full overflow-hidden rounded-full',
            hasFrame ? 'p-[2px]' : noFrameInnerClassName,
          )}
          style={hasFrame ? { backgroundColor: 'var(--app-layer-surface)' } : undefined}
        >
          <img
            src={src}
            alt={alt}
            className={cn('h-full w-full rounded-full object-cover', imageClassName)}
            data-testid={imageTestId}
            onError={onError}
          />
        </div>
      </div>
    </div>
  );
};

export default AvatarWithFrame;
