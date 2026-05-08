import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthStore } from '../auth/auth.store';

export const authGuard: CanActivateFn = () => {
  const router = inject(Router);
  const authStore = inject(AuthStore);
  return authStore.isAuthenticated() ? true : router.createUrlTree(['/login']);
};

export const roleGuard = (roles: string[]): CanActivateFn => () => {
  const router = inject(Router);
  const authStore = inject(AuthStore);
  return authStore.isAuthenticated() && authStore.hasAnyRole(roles) ? true : router.createUrlTree(['/dashboard']);
};
