import { computed, signal } from '@angular/core';

const storageKey = 'bitacoratech.auth';

export interface AuthSession {
  accessToken: string;
  refreshToken: string;
  accessTokenExpiresAt: string;
  refreshTokenExpiresAt: string;
  userId: string;
  tenantId: string;
  email: string;
  roles: string[];
}

const readSession = (): AuthSession | null => {
  const raw = localStorage.getItem(storageKey);
  if (!raw) {
    return null;
  }

  try {
    return JSON.parse(raw) as AuthSession;
  } catch {
    localStorage.removeItem(storageKey);
    return null;
  }
};

export const authSession = signal<AuthSession | null>(readSession());
export const accessToken = computed(() => authSession()?.accessToken ?? null);
export const isAuthenticated = computed(() => {
  const session = authSession();
  return !!session?.accessToken && new Date(session.accessTokenExpiresAt).getTime() > Date.now();
});

export const setAuthSession = (session: AuthSession) => {
  localStorage.setItem(storageKey, JSON.stringify(session));
  authSession.set(session);
};

export const clearAuthSession = () => {
  localStorage.removeItem(storageKey);
  authSession.set(null);
};

export const hasAnyRole = (roles: string[]) => {
  const sessionRoles = authSession()?.roles ?? [];
  return roles.length === 0 || roles.some((role) => sessionRoles.includes(role));
};
