import {
  BUILD_RECOVERY_QUERY_KEY,
  BUILD_RECOVERY_STORAGE_KEY,
  buildRuntimeRefreshUrl,
  cleanupRuntimeRefreshUrl,
  navigateToPublishedBuild,
  rememberBuildRecoveryAttempt,
  shouldReloadForPublishedBuild,
  shouldTriggerBuildRecovery,
} from './runtimeVersion';

describe('runtimeVersion helpers', () => {
  it('detects when a published build differs from the current build', () => {
    expect(shouldReloadForPublishedBuild({ buildId: 'build-b' }, 'build-a')).toBe(true);
    expect(shouldReloadForPublishedBuild({ buildId: 'build-a' }, 'build-a')).toBe(false);
    expect(shouldReloadForPublishedBuild({}, 'build-a')).toBe(false);
  });

  it('throttles repeated build recovery attempts for the same target build', () => {
    const storage = window.sessionStorage;
    storage.removeItem(BUILD_RECOVERY_STORAGE_KEY);

    expect(shouldTriggerBuildRecovery(storage, 'build-b', 1_000)).toBe(true);
    rememberBuildRecoveryAttempt(storage, 'build-b', 1_000);

    expect(shouldTriggerBuildRecovery(storage, 'build-b', 1_500, 1_000)).toBe(false);
    expect(shouldTriggerBuildRecovery(storage, 'build-c', 1_500, 1_000)).toBe(true);
    expect(shouldTriggerBuildRecovery(storage, 'build-b', 2_500, 1_000)).toBe(true);
  });

  it('builds a hard refresh url with the target build id preserved in the query string', () => {
    expect(
      buildRuntimeRefreshUrl(
        'https://collaby.org/workspace/demo/project/alpha?fresh=1#task-42',
        'build-b',
      ),
    ).toBe(
      'https://collaby.org/workspace/demo/project/alpha?fresh=1&__build=build-b#task-42',
    );
  });

  it('cleans up the cache-busting query once the expected build is already loaded', () => {
    const historyLike = {
      state: { marker: 1 },
      replaceState: vi.fn(),
    };

    cleanupRuntimeRefreshUrl(
      'https://collaby.org/profile?fresh=1&__build=build-b#section',
      historyLike,
      'build-b',
    );

    expect(historyLike.replaceState).toHaveBeenCalledWith(
      historyLike.state,
      '',
      '/profile?fresh=1#section',
    );
  });

  it('navigates to the published build via location.replace instead of a soft reload', () => {
    const replace = vi.fn();
    const reload = vi.fn();
    const windowLike = {
      location: {
        origin: 'https://collaby.org',
        pathname: '/dashboard',
        search: '?fresh=1',
        hash: '#top',
        replace,
        reload,
      },
    };

    navigateToPublishedBuild({ buildId: 'build-z' }, windowLike);

    expect(replace).toHaveBeenCalledWith(
      `https://collaby.org/dashboard?fresh=1&${BUILD_RECOVERY_QUERY_KEY}=build-z#top`,
    );
    expect(reload).not.toHaveBeenCalled();
  });
});
