import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { AuthStore } from '../auth/auth.store';

export const apiInterceptor: HttpInterceptorFn = (request, next) => {
  const authStore = inject(AuthStore);
  const token = authStore.accessToken();
  if (!token) {
    return next(request);
  }

  return next(
    request.clone({
      setHeaders: {
        Authorization: `Bearer ${token}`
      }
    })
  );
};
