import { Injectable, inject } from '@angular/core';
import { ArticleDetailResponse, ArticleSummaryResponse, JobResponse, PublishArticleRequest } from '../../models/api.models';
import { ApiClient } from './api-client';

@Injectable({ providedIn: 'root' })
export class ArticlesApiService {
  private readonly api = inject(ApiClient);

  getArticles() {
    return this.api.get<ArticleSummaryResponse[]>('/articles');
  }

  getArticle(articleId: string) {
    return this.api.get<ArticleDetailResponse>(`/articles/${articleId}`);
  }

  publishArticle(articleId: string, payload: PublishArticleRequest) {
    return this.api.post<PublishArticleRequest, JobResponse>(`/articles/${articleId}/publish`, payload);
  }
}
