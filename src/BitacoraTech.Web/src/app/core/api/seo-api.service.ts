import { Injectable, inject } from '@angular/core';
import { SeoAnalyzeRequest, SeoAnalyzeResponse } from '../../models/api.models';
import { ApiClient } from './api-client';

@Injectable({ providedIn: 'root' })
export class SeoApiService {
  private readonly api = inject(ApiClient);

  analyze(payload: SeoAnalyzeRequest) {
    return this.api.post<SeoAnalyzeRequest, SeoAnalyzeResponse>('/seo/analyze', payload);
  }
}
