import React from 'react';

import AppLoadingScreen from './AppLoadingScreen';
import {
  isDynamicImportError,
  rememberChunkRecoveryAttempt,
  shouldTriggerChunkRecovery,
} from '../utils/chunkRecovery';

class ChunkRecoveryBoundary extends React.Component {
  constructor(props) {
    super(props);
    this.state = {
      error: null,
    };
  }

  static getDerivedStateFromError(error) {
    return { error };
  }

  componentDidCatch(error) {
    if (!isDynamicImportError(error)) {
      return;
    }

    const locationKey = `${window.location.pathname}${window.location.search}`;
    if (!shouldTriggerChunkRecovery(window.sessionStorage, locationKey)) {
      return;
    }

    rememberChunkRecoveryAttempt(window.sessionStorage, locationKey);
    if (typeof this.props.onRecover === 'function') {
      this.props.onRecover();
      return;
    }

    window.location.reload();
  }

  render() {
    if (this.state.error) {
      if (isDynamicImportError(this.state.error)) {
        return this.props.fallback ?? <AppLoadingScreen />;
      }

      throw this.state.error;
    }

    return this.props.children;
  }
}

export default ChunkRecoveryBoundary;
