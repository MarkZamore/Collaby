import React from 'react';
import ImagePlaceholder from './ImagePlaceholder';

const LandingMedia = ({
  asset,
  fallbackType = 'dashboard',
  width = '100%',
  height = '400px',
  imageClassName = '',
  captionClassName = '',
}) => {
  if (!asset?.imageUrl) {
    return <ImagePlaceholder type={fallbackType} width={width} height={height} />;
  }

  return (
    <div className="landing-media">
      <img
        src={asset.imageUrl}
        alt={asset.imageAlt || ''}
        className={imageClassName}
        style={{ width, height }}
      />
      {asset.caption ? <div className={captionClassName}>{asset.caption}</div> : null}
    </div>
  );
};

export default LandingMedia;
