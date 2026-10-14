import React from 'react';
import { render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import VerifyEmailSent from './VerifyEmailSent';

const authApiMocks = vi.hoisted(() => ({
  resendVerificationEmail: vi.fn(),
}));

vi.mock('../../shared/utils/authApi', () => authApiMocks);

vi.mock('../../platform/context/PreferencesContext', () => ({
  usePreferences: () => ({
    resolvedTheme: 'light',
  }),
}));

vi.mock('react-hot-toast', () => ({
  default: {
    success: vi.fn(),
    error: vi.fn(),
  },
  Toaster: () => null,
}));

describe('VerifyEmailSent page', () => {
  beforeEach(() => {
    sessionStorage.clear();
    authApiMocks.resendVerificationEmail.mockReset();
  });

  const renderPage = (initialEntry = '/verify-email-sent') => render(
    <MemoryRouter initialEntries={[initialEntry]}>
      <Routes>
        <Route path="/verify-email-sent" element={<VerifyEmailSent />} />
        <Route path="/auth" element={<div>Auth page</div>} />
      </Routes>
    </MemoryRouter>,
  );

  it('renders a standalone status page even without preloaded session email', () => {
    renderPage('/verify-email-sent');

    expect(screen.getAllByText(/Verification email sent|Письмо для подтверждения отправлено/i).length).toBeGreaterThan(0);
    expect(screen.getByRole('button', { name: /Request a new email|Запросить новое письмо/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Go to sign in|Перейти ко входу/i })).toBeInTheDocument();
  });

  it('shows the email from the query string when it is available', () => {
    renderPage('/verify-email-sent?email=qa-standalone@example.invalid');

    expect(screen.getByText('qa-standalone@example.invalid')).toBeInTheDocument();
  });
});
