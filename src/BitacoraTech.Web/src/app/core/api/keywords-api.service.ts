import { Injectable, inject } from '@angular/core';
import { KeywordClusterResponse, KeywordResponse, ResearchKeywordRequest } from '../../models/api.models';
import { ApiClient } from './api-client';

@Injectable({ providedIn: 'root' })
export class KeywordsApiService {
  private readonly api = inject(ApiClient);

  getKeywords() {
    return this.api.get<KeywordResponse[]>('/keywords');
  }

  research(payload: ResearchKeywordRequest) {
    return this.api.post<ResearchKeywordRequest, KeywordClusterResponse[]>('/keywords/research', payload);
  }
}
