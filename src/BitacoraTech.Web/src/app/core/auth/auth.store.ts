import { computed, Injectable, signal } from '@angular/core';
import { AuthResponse } from '../../models/api.models';

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

@Injectable({ providedIn: 'root' })
export class AuthStore {
  private readonly sessionState = signal<AuthSession | null>(this.readSession());
  readonly session = this.sessionState.asReadonly();
  readonly accessToken = computed(() => this.sessionState()?.accessToken ?? null);
  readonly email = computed(() => this.sessionState()?.email ?? 'guest@bitacora.tech');
  readonly roles = computed(() => this.sessionState()?.roles ?? []);
  readonly isAuthenticated = computed(() => {
    const session = this.sessionState();
    return !!session?.accessToken && new Date(session.accessTokenExpiresAt).getTime() > Date.now();
  });

  setSession(response: AuthResponse) {
    const session: AuthSession = {
      accessToken: response.accessToken,
      refreshToken: response.refreshToken,
      accessTokenExpiresAt: response.accessTokenExpiresAt,
      refreshTokenExpiresAt: response.refreshTokenExpiresAt,
      userId: response.userId,
      tenantId: response.tenantId,
      email: response.email,
      roles: response.roles
    };

    localStorage.setItem(storageKey, JSON.stringify(session));
    this.sessionState.set(session);
  }

  clear() {
    localStorage.removeItem(storageKey);
    this.sessionState.set(null);
  }

  hasAnyRole(roles: string[]) {
    const sessionRoles = this.roles();
    return roles.length === 0 || roles.some((role) => sessionRoles.includes(role));
  }

  private readSession(): AuthSession | null {
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
  }
}
