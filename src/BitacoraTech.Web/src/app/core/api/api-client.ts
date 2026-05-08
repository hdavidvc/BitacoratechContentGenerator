import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';

export const apiBaseUrl = '/api/v1';

@Injectable({ providedIn: 'root' })
export class ApiClient {
  private readonly http = inject(HttpClient);

  get<T>(path: string) {
    return this.http.get<T>(`${apiBaseUrl}${path}`);
  }

  post<TRequest, TResponse>(path: string, body: TRequest) {
    return this.http.post<TResponse>(`${apiBaseUrl}${path}`, body);
  }
}
