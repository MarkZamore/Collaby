import { useEffect, useRef, useCallback, useState } from 'react';
import * as signalR from '@microsoft/signalr';
import { logRealtimeIssue, shouldIgnoreRealtimeError } from '../utils/realtimeErrors';

/**
 * SignalR hook for real-time gamification events.
 *
 * @param {string|null} accessToken JWT for authentication
 * @param {function} onReward called with reward payload { xp, coins, level, reason }
 */
export default function useGamificationSignalR(accessToken, onReward) {
  const connectionRef = useRef(null);
  const onRewardRef = useRef(onReward);
  const isStoppingRef = useRef(false);
  onRewardRef.current = onReward;

  const apiBase = import.meta.env.VITE_API_BASE_URL || '/api';
  const [connectionState, setConnectionState] = useState(signalR.HubConnectionState.Disconnected);
  const markDegraded = useCallback((errorCode, error, { silent = false } = {}) => {
    logRealtimeIssue('Gamification SignalR', errorCode, error, {
      isShuttingDown: isStoppingRef.current,
      silent,
    });
  }, []);

  const clearRealtimeError = useCallback(() => {}, []);

  useEffect(() => {
    if (!accessToken) {
      isStoppingRef.current = true;
      setConnectionState(signalR.HubConnectionState.Disconnected);
      clearRealtimeError();
      return undefined;
    }

    isStoppingRef.current = false;

    const connection = new signalR.HubConnectionBuilder()
      .withUrl(`${apiBase}/gamification/hubs/gamification`, { accessTokenFactory: () => accessToken })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(signalR.LogLevel.None)
      .build();

    connectionRef.current = connection;
    setConnectionState(connection.state);

    connection.on('RewardReceived', (payload) => {
      if (onRewardRef.current) {
        onRewardRef.current(payload);
      }
    });

    connection.onreconnecting((error) => {
      setConnectionState(signalR.HubConnectionState.Reconnecting);
      markDegraded('gamification-realtime-reconnecting', error, { silent: true });
    });

    connection.onreconnected(() => {
      setConnectionState(signalR.HubConnectionState.Connected);
      clearRealtimeError();
    });

    connection.onclose((error) => {
      setConnectionState(signalR.HubConnectionState.Disconnected);
      if (error && !shouldIgnoreRealtimeError(error, { isShuttingDown: isStoppingRef.current })) {
        markDegraded('gamification-realtime-closed', error);
      }
    });

    connection.start()
      .then(() => {
        setConnectionState(signalR.HubConnectionState.Connected);
        clearRealtimeError();
      })
      .catch((error) => {
        setConnectionState(signalR.HubConnectionState.Disconnected);
        markDegraded('gamification-realtime-connect-failed', error);
      });

    return () => {
      isStoppingRef.current = true;
      void connection.stop().catch((error) => {
        logRealtimeIssue('Gamification SignalR', 'gamification-realtime-stop-failed', error, {
          isShuttingDown: true,
        });
      });

      connectionRef.current = null;
      setConnectionState(signalR.HubConnectionState.Disconnected);
    };
  }, [accessToken, apiBase, clearRealtimeError, markDegraded]);

  return {
    connectionState,
  };
}
