import React, { useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Check, Search, Upload, X } from 'lucide-react';
import { useAuth } from '../../shared/context/AuthContext';
import { searchBackgroundImages } from '../../shared/utils/workspaceApi';
import { usePlatform } from '../context/PlatformContext';

const PRESET_BACKGROUNDS = [
  { id: 'img-1', url: 'https://images.unsplash.com/photo-1519681393784-d120267933ba?w=1920&q=80', label: 'Mountains' },
  { id: 'img-2', url: 'https://images.unsplash.com/photo-1507400492013-162706c8c05e?w=1920&q=80', label: 'Ocean' },
  { id: 'img-3', url: 'https://images.unsplash.com/photo-1477346611705-65d1883cee1e?w=1920&q=80', label: 'Forest' },
  { id: 'img-4', url: 'https://images.unsplash.com/photo-1534088568595-a066f410bcda?w=1920&q=80', label: 'Sunset' },
  { id: 'img-5', url: 'https://images.unsplash.com/photo-1451187580459-43490279c0fa?w=1920&q=80', label: 'Space' },
  { id: 'img-6', url: 'https://images.unsplash.com/photo-1557682250-33bd709cbe85?w=1920&q=80', label: 'Gradient Purple' },
  { id: 'img-7', url: 'https://images.unsplash.com/photo-1558591710-4b4a1ae0f04d?w=1920&q=80', label: 'Abstract' },
  { id: 'img-8', url: 'https://images.unsplash.com/photo-1497436072909-60f360e1d4b1?w=1920&q=80', label: 'Valley' },
  { id: 'img-9', url: 'https://images.unsplash.com/photo-1475924156734-496f6cac6ec1?w=1920&q=80', label: 'Aurora' },
  { id: 'img-10', url: 'https://images.unsplash.com/photo-1488866022916-f7f2a032cd24?w=1920&q=80', label: 'Clouds' },
  { id: 'img-11', url: 'https://images.unsplash.com/photo-1579546929518-9e396f3cc809?w=1920&q=80', label: 'Gradient' },
  { id: 'img-12', url: 'https://images.unsplash.com/photo-1506744038136-46273834b3fb?w=1920&q=80', label: 'Lake' },
];

const PRESET_COLORS = [
  '#1a472a',
  '#0e3d4a',
  '#4a1a1a',
  '#2d2d2d',
  '#1a1a3a',
  '#3a2a1a',
  '#1a3a2a',
  '#2a1a3a',
  '#0f172a',
  '#1e293b',
  '#18181b',
  '#1c1917',
  '#1a2744',
  '#2d1b69',
  '#134e4a',
  '#713f12',
];

const SEARCH_TAGS = ['gradient', 'clouds', 'landscape', 'ocean', 'autumn', 'abstract', 'city', 'dark', 'minimal'];

const BackgroundPicker = ({ showForAllCheckbox = true }) => {
  const { t } = useTranslation('platform');
  const { accessToken } = useAuth();
  const {
    boardBackground,
    defaultBoardBackground,
    projectBoardBackgroundOverride,
    setBoardBackground,
    clearProjectBoardBackground,
    clearDefaultBoardBackground,
  } = usePlatform();
  const fileInputRef = useRef(null);
  const perPage = 12;

  const [searchQuery, setSearchQuery] = useState('');
  const [searchResults, setSearchResults] = useState([]);
  const [searchPage, setSearchPage] = useState(1);
  const [lastSearchQuery, setLastSearchQuery] = useState('');
  const [hasMoreResults, setHasMoreResults] = useState(false);
  const [searchError, setSearchError] = useState(null);
  const [forAll, setForAll] = useState(false);
  const [isSearching, setIsSearching] = useState(false);
  const [isLoadingMore, setIsLoadingMore] = useState(false);
  const selectionMode = showForAllCheckbox ? (forAll ? 'global' : 'project') : 'global';
  const activeBackground = selectionMode === 'global'
    ? defaultBoardBackground
    : projectBoardBackgroundOverride || boardBackground;
  const canResetActiveBackground = selectionMode === 'global'
    ? Boolean(defaultBoardBackground)
    : Boolean(projectBoardBackgroundOverride);

  const handleSelectBackground = (background) => {
    setBoardBackground(background, { forAll: selectionMode === 'global' });
  };

  const handleReset = () => {
    if (selectionMode === 'global') {
      clearDefaultBoardBackground();
      return;
    }

    clearProjectBoardBackground();
  };

  const handleSearch = async (query) => {
    const trimmed = (query || '').trim();

    if (!trimmed) {
      setSearchResults([]);
      setSearchError(null);
      setSearchPage(1);
      setLastSearchQuery('');
      setHasMoreResults(false);
      return;
    }

    setIsSearching(true);
    setSearchError(null);
    setSearchPage(1);
    setLastSearchQuery(trimmed);

    try {
      const data = await searchBackgroundImages(accessToken, trimmed, perPage, 1);
      const list = Array.isArray(data?.results) ? data.results : [];
      setSearchResults(list);
      setHasMoreResults(list.length >= perPage);
    } catch (error) {
      setSearchResults([]);
      setHasMoreResults(false);
      setSearchError(error?.status === 403 ? t('backgroundPicker.searchUnavailable') : error?.message || t('backgroundPicker.searchFailed'));
    } finally {
      setIsSearching(false);
    }
  };

  const handleLoadMore = async () => {
    if (!lastSearchQuery || isLoadingMore || !hasMoreResults) {
      return;
    }

    const nextPage = searchPage + 1;
    setIsLoadingMore(true);
    setSearchError(null);

    try {
      const data = await searchBackgroundImages(accessToken, lastSearchQuery, perPage, nextPage);
      const list = Array.isArray(data?.results) ? data.results : [];
      setSearchResults((prev) => [...prev, ...list]);
      setSearchPage(nextPage);
      setHasMoreResults(list.length >= perPage);
    } catch (error) {
      setSearchError(error?.message || t('backgroundPicker.loadMoreFailed'));
    } finally {
      setIsLoadingMore(false);
    }
  };

  const handleFileUpload = (event) => {
    const file = event.target.files?.[0];

    if (!file) {
      return;
    }

    const reader = new FileReader();
    reader.onload = (loadEvent) => {
      handleSelectBackground({ type: 'upload', value: loadEvent.target.result });
    };
    reader.readAsDataURL(file);
  };

  const isSelected = (type, value) => activeBackground?.type === type && activeBackground?.value === value;

  return (
    <div className="space-y-5">
      <div className="flex items-center justify-between">
        <h3 className="text-sm font-semibold" style={{ color: 'var(--app-text-main)' }}>
          {t('backgroundPicker.title')}
        </h3>
        {canResetActiveBackground && (
          <button
            onClick={handleReset}
            className="text-xs flex items-center gap-1 px-2 py-1 rounded-md hover:bg-red-500/10 transition-colors"
            style={{ color: '#EF4444' }}
          >
            <X size={12} />
            {t('backgroundPicker.reset')}
          </button>
        )}
      </div>

      {showForAllCheckbox && (
        <label className="flex items-center gap-3 cursor-pointer" onClick={() => setForAll((value) => !value)}>
          <div
            className="w-[18px] h-[18px] rounded shrink-0 flex items-center justify-center transition-colors"
            style={{
              backgroundColor: forAll ? '#3B82F6' : 'transparent',
              border: forAll ? 'none' : '2px solid var(--app-border)',
            }}
          >
            {forAll && (
              <svg width="12" height="12" viewBox="0 0 12 12" fill="none">
                <path d="M2.5 6L5 8.5L9.5 3.5" stroke="white" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
              </svg>
            )}
          </div>
          <span className="text-sm" style={{ color: 'var(--app-text-main)' }}>
            {t('backgroundPicker.applyForAll')}
          </span>
        </label>
      )}

      <button
        onClick={() => fileInputRef.current?.click()}
        className="text-sm font-medium flex items-center gap-1.5 px-3 py-2 rounded-lg transition-colors hover:bg-white/5"
        style={{ color: 'var(--app-accent)', border: '1px dashed var(--app-border)' }}
      >
        <Upload size={14} />
        {t('backgroundPicker.upload')}
      </button>

      <input ref={fileInputRef} type="file" accept="image/*" onChange={handleFileUpload} className="hidden" />

      <form
        onSubmit={(event) => {
          event.preventDefault();
          handleSearch(searchQuery);
        }}
        className="flex items-center gap-2"
      >
        <div className="relative flex-1">
          <Search size={14} className="absolute left-3 top-1/2 -translate-y-1/2" style={{ color: 'var(--app-text-secondary)' }} />
          <input
            type="text"
            placeholder={t('backgroundPicker.searchPlaceholder')}
            className="w-full pl-9 pr-4 py-2 rounded-lg text-sm outline-none"
            style={{ backgroundColor: 'var(--app-layer-base)', border: '1px solid var(--app-border)', color: 'var(--app-text-main)' }}
            value={searchQuery}
            onChange={(event) => setSearchQuery(event.target.value)}
          />
        </div>
        <button type="submit" className="px-4 py-2 rounded-lg text-sm font-medium" style={{ backgroundColor: 'var(--app-accent)', color: 'white' }}>
          {t('backgroundPicker.search')}
        </button>
      </form>

      <div className="flex items-center gap-2 flex-wrap">
        <span className="text-xs" style={{ color: 'var(--app-text-secondary)' }}>
          {t('backgroundPicker.examples')}
        </span>
        {SEARCH_TAGS.map((tag) => (
          <span
            key={tag}
            className="text-xs px-2 py-0.5 rounded-full cursor-pointer hover:opacity-80 transition-opacity"
            style={{ color: 'var(--app-accent)', backgroundColor: 'color-mix(in srgb, var(--app-accent) 10%, transparent)' }}
            onClick={() => {
              setSearchQuery(tag);
              handleSearch(tag);
            }}
          >
            {tag}
          </span>
        ))}
      </div>

      {searchError && (
        <p className="text-xs" style={{ color: '#EF4444' }}>
          {searchError}
        </p>
      )}

      {searchResults.length > 0 && (
        <div>
          <p className="text-xs font-medium mb-2" style={{ color: 'var(--app-text-secondary)' }}>
            {t('backgroundPicker.searchResults')}
          </p>
          <div className="grid grid-cols-4 gap-2">
            {searchResults.map((item) => (
              <div
                key={item.id}
                onClick={() => handleSelectBackground({ type: 'image', value: item.url })}
                className="aspect-[4/3] rounded-lg cursor-pointer transition-all hover:scale-[1.03] overflow-hidden relative group"
                style={{ border: isSelected('image', item.url) ? '3px solid var(--app-accent)' : '2px solid transparent' }}
              >
                <img src={item.url} alt="" className="w-full h-full object-cover" loading="lazy" />
                {isSelected('image', item.url) && (
                  <div className="absolute inset-0 bg-blue-500/20 flex items-center justify-center">
                    <Check size={20} className="text-white drop-shadow-lg" />
                  </div>
                )}
                <div className="absolute inset-0 bg-black/0 group-hover:bg-black/20 transition-colors" />
              </div>
            ))}
          </div>
          {hasMoreResults && (
            <button
              type="button"
              onClick={handleLoadMore}
              disabled={isLoadingMore}
              className="w-full mt-3 py-2 rounded-lg text-sm font-medium transition-colors disabled:opacity-50"
              style={{ backgroundColor: 'var(--app-layer-base)', color: 'var(--app-accent)', border: '1px solid var(--app-border)' }}
            >
              {isLoadingMore ? t('common:status.loading') : t('backgroundPicker.loadMore')}
            </button>
          )}
          <p className="text-[10px] mt-1.5" style={{ color: 'var(--app-text-secondary)' }}>
            {t('backgroundPicker.fromUnsplash')}{' '}
            <a href="https://unsplash.com" target="_blank" rel="noopener noreferrer" className="underline hover:opacity-80" style={{ color: 'var(--app-text-secondary)' }}>
              Unsplash
            </a>
          </p>
        </div>
      )}

      {isSearching && (
        <div className="text-center py-4">
          <span className="text-sm animate-pulse" style={{ color: 'var(--app-text-secondary)' }}>
            {t('backgroundPicker.searching')}
          </span>
        </div>
      )}

      <div>
        <p className="text-xs font-medium mb-2" style={{ color: 'var(--app-text-secondary)' }}>
          {t('backgroundPicker.solidColors')}
        </p>
        <div className="grid grid-cols-8 gap-2">
          {PRESET_COLORS.map((color) => (
            <div
              key={color}
              onClick={() => handleSelectBackground({ type: 'color', value: color })}
              className="aspect-square rounded-lg cursor-pointer transition-all hover:scale-[1.1] relative"
              style={{
                backgroundColor: color,
                border: isSelected('color', color) ? '3px solid var(--app-accent)' : '2px solid rgba(255,255,255,0.1)',
              }}
            >
              {isSelected('color', color) && (
                <div className="absolute inset-0 flex items-center justify-center">
                  <Check size={14} className="text-white drop-shadow-lg" />
                </div>
              )}
            </div>
          ))}
        </div>
      </div>

      <div>
        <p className="text-xs font-medium mb-2" style={{ color: 'var(--app-text-secondary)' }}>
          {t('backgroundPicker.images')}
        </p>
        <div className="grid grid-cols-4 gap-2">
          {PRESET_BACKGROUNDS.map((background) => (
            <div
              key={background.id}
              onClick={() => handleSelectBackground({ type: 'image', value: background.url })}
              className="aspect-[4/3] rounded-lg cursor-pointer transition-all hover:scale-[1.03] overflow-hidden relative group"
              style={{ border: isSelected('image', background.url) ? '3px solid var(--app-accent)' : '2px solid transparent' }}
            >
              <img src={background.url} alt={background.label} className="w-full h-full object-cover" loading="lazy" />
              {isSelected('image', background.url) && (
                <div className="absolute inset-0 bg-blue-500/20 flex items-center justify-center">
                  <Check size={20} className="text-white drop-shadow-lg" />
                </div>
              )}
              <div className="absolute inset-0 bg-black/0 group-hover:bg-black/20 transition-colors" />
              <div className="absolute bottom-0 left-0 right-0 px-2 py-1 bg-gradient-to-t from-black/50 to-transparent opacity-0 group-hover:opacity-100 transition-opacity">
                <span className="text-[10px] text-white font-medium">{background.label}</span>
              </div>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
};

export default BackgroundPicker;
