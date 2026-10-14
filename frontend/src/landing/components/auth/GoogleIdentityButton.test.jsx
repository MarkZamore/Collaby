import React from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import GoogleIdentityButton from './GoogleIdentityButton';
import { loadGoogleIdentityScript } from '../../../shared/utils/googleIdentity';

vi.mock('../../../shared/utils/googleIdentity', () => ({
  loadGoogleIdentityScript: vi.fn(),
}));

vi.mock('react-i18next', () => ({
  useTranslation: () => ({
    t: (_key, options) => options?.defaultValue ?? '',
  }),
}));

describe('GoogleIdentityButton', () => {
  let initialize;
  let renderButton;
  let prompt;

  beforeEach(() => {
    initialize = vi.fn();
    renderButton = vi.fn((element) => {
      element.appendChild(document.createElement('iframe'));
    });
    prompt = vi.fn((callback) => {
      callback({
        isNotDisplayed: () => false,
        isSkippedMoment: () => false,
        isDismissedMoment: () => false,
      });
    });

    vi.stubGlobal('google', {
      accounts: {
        id: {
          initialize,
          renderButton,
          prompt,
        },
      },
    });

    loadGoogleIdentityScript.mockResolvedValue(window.google);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    vi.clearAllMocks();
  });

  it('renders the custom-styled Google button over the official click target', async () => {
    render(<GoogleIdentityButton clientId="google-client-id" dark={false} onCredential={vi.fn()} />);

    const visualButton = screen.getByRole('button', { name: /continue with google/i });
    expect(visualButton).toHaveClass('google-custom-btn');
    expect(visualButton).toBeDisabled();

    const buttonContainer = await screen.findByTestId('google-identity-button');

    await waitFor(() => expect(renderButton).toHaveBeenCalledWith(
      buttonContainer,
      expect.objectContaining({
        shape: 'pill',
        size: 'large',
        text: 'continue_with',
        theme: 'outline',
        width: 400,
      }),
    ));

    await waitFor(() => expect(visualButton).toBeEnabled());
    expect(buttonContainer).toHaveClass('google-official-hitbox', 'is-ready');
    expect(buttonContainer).toHaveAttribute('aria-hidden', 'true');

    fireEvent.click(visualButton);
    expect(prompt).toHaveBeenCalledTimes(1);
  });

  it('passes Google credentials to the auth flow', async () => {
    const onCredential = vi.fn();
    render(<GoogleIdentityButton clientId="google-client-id" dark onCredential={onCredential} />);

    await waitFor(() => expect(initialize).toHaveBeenCalledTimes(1));

    const config = initialize.mock.calls[0][0];
    config.callback({ credential: 'google-id-token' });

    expect(onCredential).toHaveBeenCalledWith('google-id-token');
  });
});
