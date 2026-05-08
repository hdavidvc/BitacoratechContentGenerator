import { HttpInterceptorFn } from '@angular/common/http';
import { accessToken } from '../auth/auth.store';

export const apiInterceptor: HttpInterceptorFn = (request, next) => {
  const token = accessToken();
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
