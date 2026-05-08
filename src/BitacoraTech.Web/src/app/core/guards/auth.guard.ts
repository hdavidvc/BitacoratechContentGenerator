import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { hasAnyRole, isAuthenticated } from '../auth/auth.store';

export const authGuard: CanActivateFn = () => {
  const router = inject(Router);
  return isAuthenticated() ? true : router.createUrlTree(['/login']);
};

export const roleGuard = (roles: string[]): CanActivateFn => () => {
  const router = inject(Router);
  return isAuthenticated() && hasAnyRole(roles) ? true : router.createUrlTree(['/dashboard']);
};
