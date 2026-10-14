import React from 'react';

/**
 * Skeleton placeholder for loading states.
 * Usage:
 *   <Skeleton width="100%" height={20} />
 *   <Skeleton circle size={40} />
 *   <Skeleton.Card />
 *   <Skeleton.Chart />
 *   <Skeleton.List rows={4} />
 */

const pulseStyle = {
  animation: 'skeleton-pulse 1.8s ease-in-out infinite',
  borderRadius: 8,
};

const Skeleton = ({ width = '100%', height = 16, circle = false, size, style = {}, className = '' }) => {
  const dim = circle ? (size || height) : undefined;
  return (
    <div
      className={`skeleton-bone ${className}`}
      style={{
        width: circle ? dim : width,
        height: circle ? dim : height,
        borderRadius: circle ? '50%' : 8,
        backgroundColor: 'var(--app-border, rgba(0,0,0,0.07))',
        ...pulseStyle,
        ...style,
      }}
    />
  );
};

Skeleton.Card = ({ height = 250 }) => (
  <div style={{ padding: 24, display: 'flex', flexDirection: 'column', gap: 16 }}>
    <Skeleton width="40%" height={18} />
    <Skeleton width="60%" height={14} />
    <Skeleton width="100%" height={height - 80} style={{ borderRadius: 12 }} />
  </div>
);

Skeleton.Chart = () => (
  <div style={{ display: 'flex', flexDirection: 'column', gap: 12, height: 250, justifyContent: 'flex-end', padding: '0 8px' }}>
    <div style={{ display: 'flex', gap: 6, alignItems: 'flex-end', flex: 1 }}>
      {[60, 80, 45, 90, 70, 55, 85].map((h, i) => (
        <Skeleton key={i} width="100%" height={`${h}%`} style={{ borderRadius: 4, animationDelay: `${i * 0.1}s` }} />
      ))}
    </div>
    <Skeleton width="100%" height={1} style={{ borderRadius: 0 }} />
  </div>
);

Skeleton.List = ({ rows = 4 }) => (
  <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
    {Array.from({ length: rows }).map((_, i) => (
      <div key={i} style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
        <Skeleton circle size={32} style={{ animationDelay: `${i * 0.1}s` }} />
        <div style={{ flex: 1, display: 'flex', flexDirection: 'column', gap: 6 }}>
          <Skeleton width={`${70 - i * 10}%`} height={14} style={{ animationDelay: `${i * 0.1}s` }} />
          <Skeleton width={`${40 + i * 5}%`} height={10} style={{ animationDelay: `${i * 0.15}s` }} />
        </div>
      </div>
    ))}
  </div>
);

Skeleton.TaskRow = ({ rows = 3 }) => (
  <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
    {Array.from({ length: rows }).map((_, i) => (
      <div
        key={i}
        style={{
          display: 'flex', alignItems: 'center', gap: 12, padding: '12px 16px',
          borderRadius: 12, backgroundColor: 'var(--app-layer-surface, #f5f5f5)',
        }}
      >
        <Skeleton width={16} height={16} style={{ borderRadius: 4, flexShrink: 0, animationDelay: `${i * 0.1}s` }} />
        <Skeleton width={`${65 - i * 8}%`} height={14} style={{ animationDelay: `${i * 0.1}s` }} />
        <div style={{ marginLeft: 'auto', display: 'flex', gap: 8 }}>
          <Skeleton width={60} height={22} style={{ borderRadius: 999, animationDelay: `${i * 0.15}s` }} />
          <Skeleton width={40} height={22} style={{ borderRadius: 999, animationDelay: `${i * 0.2}s` }} />
        </div>
      </div>
    ))}
  </div>
);

export default Skeleton;
