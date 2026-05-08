import { Injectable, computed, inject, signal } from '@angular/core';
import { finalize, forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { ArticlesApiService } from '../core/api/articles-api.service';
import { KeywordsApiService } from '../core/api/keywords-api.service';
import { SeoApiService } from '../core/api/seo-api.service';
import {
  ArticleDetailResponse,
  ArticlePreviewModel,
  ArticleSummaryResponse,
  KeywordClusterResponse,
  KeywordResponse,
  PublishArticleRequest,
  ResearchKeywordRequest,
  SeoAnalyzeRequest,
  SeoAnalyzeResponse,
  WordPressPublicationMode
} from '../models/api.models';

const seedArticles: ArticlePreviewModel[] = [
  {
    id: 'art-1',
    siteId: 'site-1',
    title: 'Angular 20 para equipos editoriales: pipeline, previews y SEO',
    excerpt: 'Checklist de rendimiento, estructura y automatizacion para publicar mas rapido.',
    htmlContent:
      '<figure><img src="https://images.unsplash.com/photo-1499750310107-5fef28a66643?auto=format&fit=crop&w=1400&q=80" alt="Espacio editorial con laptop y libreta" /></figure><h2>Resumen</h2><p>Angular 20 permite construir flujos editoriales modulares con senales, vistas rapidas y una capa SEO accionable.</p><h3>Puntos clave</h3><ul><li>Preview editorial en tiempo real</li><li>Keywords accionables</li><li>Metricas claras para publicacion</li></ul>',
    status: 'Draft',
    seoScore: 84,
    primaryKeyword: 'angular 20 seo',
    updatedAt: '2026-05-08T09:00:00Z',
    wordPressPostId: null
  },
  {
    id: 'art-2',
    siteId: 'site-1',
    title: 'Guia practica de keyword clustering para blogs B2B',
    excerpt: 'Como agrupar intencion, volumen y dificultad sin perder foco comercial.',
    htmlContent:
      '<figure><img src="https://images.unsplash.com/photo-1516321318423-f06f85e504b3?auto=format&fit=crop&w=1400&q=80" alt="Equipo analizando datos de contenido" /></figure><h2>Clustering</h2><p>El cluster correcto evita canibalizacion y mejora la arquitectura tematica del sitio.</p>',
    status: 'Review',
    seoScore: 72,
    primaryKeyword: 'keyword clustering b2b',
    updatedAt: '2026-05-07T15:30:00Z',
    wordPressPostId: null
  },
  {
    id: 'art-3',
    siteId: 'site-1',
    title: 'Playbook para auditorias SEO rapidas antes de publicar',
    excerpt: 'Un framework corto para subir la calidad antes de empujar a WordPress.',
    htmlContent:
      '<figure><img src="https://images.unsplash.com/photo-1486312338219-ce68d2c6f44d?auto=format&fit=crop&w=1400&q=80" alt="Checklist editorial en pantalla" /></figure><h2>Auditoria final</h2><p>Revisa title, densidad, FAQs, enlaces internos y legibilidad antes de publicar.</p>',
    status: 'Scheduled',
    seoScore: 91,
    primaryKeyword: 'auditoria seo contenido',
    updatedAt: '2026-05-06T18:45:00Z',
    wordPressPostId: '2841'
  }
];

const seedKeywords: KeywordResponse[] = [
  {
    id: 'kw-1',
    siteId: 'site-1',
    keyword: 'angular seo dashboard',
    searchVolume: 2400,
    difficulty: 36,
    trendScore: 82,
    opportunityScore: 88,
    mentions: 14,
    source: 'research',
    cluster: 'editorial ops',
    intent: 'commercial',
    lastSeenAt: '2026-05-08T10:00:00Z'
  },
  {
    id: 'kw-2',
    siteId: 'site-1',
    keyword: 'keyword clustering software',
    searchVolume: 1600,
    difficulty: 42,
    trendScore: 75,
    opportunityScore: 79,
    mentions: 9,
    source: 'research',
    cluster: 'keyword management',
    intent: 'commercial',
    lastSeenAt: '2026-05-08T08:00:00Z'
  },
  {
    id: 'kw-3',
    siteId: 'site-1',
    keyword: 'seo article score',
    searchVolume: 880,
    difficulty: 28,
    trendScore: 69,
    opportunityScore: 83,
    mentions: 6,
    source: 'research',
    cluster: 'seo optimization',
    intent: 'informational',
    lastSeenAt: '2026-05-07T12:00:00Z'
  }
];

const seedSeoReport: SeoAnalyzeResponse = {
  score: 84,
  keywordDensity: 1.9,
  readability: 'Muy buena',
  recommendations: [
    'Agrega un CTA en el primer tercio del articulo.',
    'Aumenta menciones semanticas en el bloque final.',
    'Inserta un enlace interno hacia la guia principal del cluster.'
  ],
  semanticKeywords: ['signals angular', 'angular material dashboard', 'content workflow'],
  internalLinks: [{ anchorText: 'guia completa de senales', slug: '/blog/angular-signals', reason: 'Refuerza topical authority.' }],
  faqs: [{ question: 'Que score SEO es suficiente?', answer: 'Para publicacion rapida, apunta a 80+ con legibilidad estable.' }],
  faqSchema: '{"@type":"FAQPage"}',
  jsonLd: '{"@context":"https://schema.org"}',
  readabilityMetrics: {
    wordCount: 1240,
    sentenceCount: 73,
    paragraphCount: 22,
    readingEase: 71.5,
    gradeLevel: '8th grade'
  }
};

@Injectable({ providedIn: 'root' })
export class WorkspaceStore {
  private readonly articlesApi = inject(ArticlesApiService);
  private readonly keywordsApi = inject(KeywordsApiService);
  private readonly seoApi = inject(SeoApiService);

  private readonly loadingState = signal(false);
  private readonly seoLoadingState = signal(false);
  private readonly keywordResearchState = signal(false);
  private readonly articleLoadingState = signal(false);
  private readonly publishingState = signal(false);
  private readonly publishMessageState = signal<string | null>(null);
  private readonly articlesState = signal<ArticlePreviewModel[]>(seedArticles);
  private readonly keywordsState = signal<KeywordResponse[]>(seedKeywords);
  private readonly seoState = signal<SeoAnalyzeResponse>(seedSeoReport);
  private readonly selectedArticleIdState = signal(seedArticles[0].id);

  readonly loading = this.loadingState.asReadonly();
  readonly seoLoading = this.seoLoadingState.asReadonly();
  readonly keywordResearchLoading = this.keywordResearchState.asReadonly();
  readonly articleLoading = this.articleLoadingState.asReadonly();
  readonly publishing = this.publishingState.asReadonly();
  readonly publishMessage = this.publishMessageState.asReadonly();
  readonly articles = this.articlesState.asReadonly();
  readonly keywords = this.keywordsState.asReadonly();
  readonly seoReport = this.seoState.asReadonly();
  readonly selectedArticleId = this.selectedArticleIdState.asReadonly();

  readonly selectedArticle = computed(
    () => this.articlesState().find((article) => article.id === this.selectedArticleIdState()) ?? this.articlesState()[0]
  );

  readonly dashboardStats = computed(() => {
    const articles = this.articlesState();
    const keywords = this.keywordsState();
    const avgSeo = Math.round(articles.reduce((sum, article) => sum + article.seoScore, 0) / articles.length);

    return [
      { label: 'Articles activos', value: articles.length.toString().padStart(2, '0'), helper: 'Pipeline editorial' },
      { label: 'SEO promedio', value: `${avgSeo}`, helper: 'Calidad lista para publicar' },
      { label: 'Keywords vivas', value: keywords.length.toString().padStart(2, '0'), helper: 'Oportunidades detectadas' },
      { label: 'Top oportunidad', value: `${Math.max(...keywords.map((keyword) => keyword.opportunityScore))}`, helper: 'Cluster mas fuerte' }
    ];
  });

  readonly keywordClusters = computed(() => {
    const map = new Map<string, KeywordResponse[]>();
    for (const keyword of this.keywordsState()) {
      const cluster = map.get(keyword.cluster) ?? [];
      cluster.push(keyword);
      map.set(keyword.cluster, cluster);
    }

    return Array.from(map.entries()).map(([cluster, keywords]) => ({
      cluster,
      count: keywords.length,
      avgOpportunity: Math.round(keywords.reduce((sum, item) => sum + item.opportunityScore, 0) / keywords.length)
    }));
  });

  loadDashboard() {
    if (this.loadingState()) {
      return;
    }

    this.loadingState.set(true);
    forkJoin({
      articles: this.articlesApi.getArticles().pipe(catchError(() => of(null))),
      keywords: this.keywordsApi.getKeywords().pipe(catchError(() => of(null)))
    })
      .pipe(finalize(() => this.loadingState.set(false)))
      .subscribe(({ articles, keywords }) => {
        if (articles?.length) {
          const mappedArticles = this.mapArticleSummaries(articles);
          this.articlesState.set(mappedArticles);
          this.selectedArticleIdState.set(mappedArticles[0].id);
          this.loadArticleDetail(mappedArticles[0].id);
        }

        if (keywords?.length) {
          this.keywordsState.set(keywords);
        }
      });
  }

  setSelectedArticle(articleId: string) {
    this.selectedArticleIdState.set(articleId);
    this.publishMessageState.set(null);
    this.loadArticleDetail(articleId);
  }

  analyzeSeo(request: SeoAnalyzeRequest) {
    this.seoLoadingState.set(true);
    this.seoApi
      .analyze(request)
      .pipe(
        catchError(() => of(seedSeoReport)),
        finalize(() => this.seoLoadingState.set(false))
      )
      .subscribe((response) => this.seoState.set(response));
  }

  researchKeywords(payload: ResearchKeywordRequest) {
    this.keywordResearchState.set(true);
    this.keywordsApi
      .research(payload)
      .pipe(
        catchError(() => of(this.mapSeedResearch(payload.seedKeyword))),
        finalize(() => this.keywordResearchState.set(false))
      )
      .subscribe((clusters) => {
        const flat = clusters.flatMap((cluster) => cluster.keywords);
        if (flat.length) {
          this.keywordsState.set(flat);
        }
      });
  }

  publishSelectedArticle() {
    const article = this.selectedArticle();
    if (!article) {
      return;
    }

    this.publishingState.set(true);
    this.publishMessageState.set(null);

    const payload: PublishArticleRequest = {
      siteId: article.siteId,
      mode: WordPressPublicationMode.Publish,
      excerpt: article.excerpt
    };

    this.articlesApi
      .publishArticle(article.id, payload)
      .pipe(
        catchError(() => {
          this.publishMessageState.set('No se pudo publicar en WordPress.');
          return of(null);
        }),
        finalize(() => this.publishingState.set(false))
      )
      .subscribe((response) => {
        if (!response) {
          return;
        }

        this.publishMessageState.set('Publicacion enviada a WordPress.');
        this.patchArticle(article.id, {
          status: 'Publishing',
          wordPressPostId: response.jobId
        });
      });
  }

  private loadArticleDetail(articleId: string) {
    if (!this.looksLikeGuid(articleId)) {
      return;
    }

    this.articleLoadingState.set(true);
    this.articlesApi
      .getArticle(articleId)
      .pipe(
        catchError(() => of(null)),
        finalize(() => this.articleLoadingState.set(false))
      )
      .subscribe((article) => {
        if (!article) {
          return;
        }

        this.patchArticle(articleId, this.mapArticleDetail(article));
      });
  }

  private mapArticleSummaries(articles: ArticleSummaryResponse[]): ArticlePreviewModel[] {
    return articles.map((article, index) => ({
      id: article.id,
      siteId: article.siteId,
      title: article.title,
      excerpt: 'Contenido sincronizado desde el backend para revision editorial rapida.',
      htmlContent: `<figure><img src="https://picsum.photos/seed/${article.id}/1400/780" alt="${article.title}" /></figure><h2>${article.title}</h2><p>Preview generado desde el resumen del articulo.</p>`,
      status: article.status,
      seoScore: article.seoScore ?? 0,
      primaryKeyword: seedKeywords[index % seedKeywords.length].keyword,
      updatedAt: new Date().toISOString(),
      wordPressPostId: null
    }));
  }

  private mapArticleDetail(article: ArticleDetailResponse): Partial<ArticlePreviewModel> {
    return {
      siteId: article.siteId,
      title: article.title,
      htmlContent: article.htmlContent,
      status: article.status,
      seoScore: article.seoScore ?? 0,
      wordPressPostId: article.wordPressPostId,
      excerpt: this.extractExcerpt(article.htmlContent)
    };
  }

  private mapSeedResearch(seedKeyword: string): KeywordClusterResponse[] {
    const enriched = seedKeywords.map((keyword, index) => ({
      ...keyword,
      id: `${keyword.id}-${index}`,
      keyword: index === 0 ? seedKeyword : `${seedKeyword} ${keyword.cluster}`
    }));

    return [
      {
        name: 'generated',
        keywordCount: enriched.length,
        averageOpportunityScore: enriched.reduce((sum, keyword) => sum + keyword.opportunityScore, 0) / enriched.length,
        keywords: enriched
      }
    ];
  }

  private patchArticle(articleId: string, patch: Partial<ArticlePreviewModel>) {
    this.articlesState.update((articles) =>
      articles.map((article) => (article.id === articleId ? { ...article, ...patch } : article))
    );
  }

  private extractExcerpt(htmlContent: string) {
    const text = htmlContent
      .replace(/<(script|style)[^>]*>.*?<\/\1>/gis, ' ')
      .replace(/<[^>]+>/g, ' ')
      .replace(/\s+/g, ' ')
      .trim();

    if (!text) {
      return 'Preview editorial listo para revision.';
    }

    return text.length > 170 ? `${text.slice(0, 167).trimEnd()}...` : text;
  }

  private looksLikeGuid(value: string) {
    return /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(value);
  }
}
