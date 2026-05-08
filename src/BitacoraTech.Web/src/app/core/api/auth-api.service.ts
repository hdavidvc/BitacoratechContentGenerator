import { Injectable, inject } from '@angular/core';
import { AuthResponse, LoginRequest } from '../../models/api.models';
import { ApiClient } from './api-client';

@Injectable({ providedIn: 'root' })
export class AuthApiService {
  private readonly api = inject(ApiClient);

  login(payload: LoginRequest) {
    return this.api.post<LoginRequest, AuthResponse>('/auth/login', payload);
  }
}
