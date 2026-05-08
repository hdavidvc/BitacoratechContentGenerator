import { CommonModule, DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { WorkspaceStore } from '../../state/workspace.store';

@Component({
  selector: 'app-article-preview',
  standalone: true,
  imports: [
    CommonModule,
    DatePipe,
    MatButtonModule,
    MatButtonToggleModule,
    MatCardModule,
    MatChipsModule,
    MatIconModule,
    MatListModule,
    MatProgressBarModule
  ],
  template: `
    <section class="preview-shell">
      <mat-card class="article-list">
        <p class="eyebrow">WordPress preview</p>
        <div class="list-head">
          <div>
            <h2>Articulos</h2>
            <p>Selecciona una pieza para revisar antes de publicar.</p>
          </div>
          @if (workspaceStore.loading()) {
            <span class="mini-state">Sync...</span>
          }
        </div>

        <mat-nav-list>
          @for (article of workspaceStore.articles(); track article.id) {
            <a mat-list-item (click)="workspaceStore.setSelectedArticle(article.id)" [class.active]="workspaceStore.selectedArticleId() === article.id">
              <div matListItemTitle>{{ article.title }}</div>
              <div matListItemLine>{{ article.excerpt }}</div>
              <div matListItemMeta class="list-score">{{ article.seoScore }}</div>
            </a>
          }
        </mat-nav-list>
      </mat-card>

      <div class="preview-column">
        <mat-card class="control-bar">
          <div>
            <p class="eyebrow">Preview canvas</p>
            <h2>{{ selectedArticle().title }}</h2>
            <p class="meta-line">
              <span>{{ selectedArticle().updatedAt | date:'medium' }}</span>
              <span>{{ estimatedReadingTime() }}</span>
            </p>
          </div>

          <div class="control-actions">
            <mat-button-toggle-group [value]="viewport()" (change)="viewport.set($event.value)" aria-label="Cambiar vista">
              <mat-button-toggle value="desktop">
                <mat-icon>desktop_windows</mat-icon>
                Desktop
              </mat-button-toggle>
              <mat-button-toggle value="mobile">
                <mat-icon>smartphone</mat-icon>
                Mobile
              </mat-button-toggle>
            </mat-button-toggle-group>

            <button mat-flat-button class="publish-btn" (click)="workspaceStore.publishSelectedArticle()" [disabled]="workspaceStore.publishing()">
              <mat-icon>{{ workspaceStore.publishing() ? 'sync' : 'publish' }}</mat-icon>
              {{ workspaceStore.publishing() ? 'Publicando...' : 'Publicar' }}
            </button>
          </div>
        </mat-card>

        <section class="insights-grid">
          <mat-card class="insight-card seo-card">
            <div class="score-top">
              <div>
                <p class="eyebrow">SEO score</p>
                <h3>{{ selectedArticle().seoScore }}/100</h3>
              </div>
              <div class="score-chip" [class.good]="selectedArticle().seoScore >= 80" [class.warn]="selectedArticle().seoScore < 80">
                {{ seoLabel() }}
              </div>
            </div>
            <mat-progress-bar mode="determinate" [value]="selectedArticle().seoScore"></mat-progress-bar>
          </mat-card>

          <mat-card class="insight-card">
            <p class="eyebrow">Contenido</p>
            <div class="stat-pair">
              <strong>{{ contentMetrics().words }}</strong>
              <span>palabras</span>
            </div>
            <div class="stat-pair">
              <strong>{{ contentMetrics().images }}</strong>
              <span>imagenes</span>
            </div>
          </mat-card>

          <mat-card class="insight-card">
            <p class="eyebrow">Keyword</p>
            <mat-chip>{{ selectedArticle().primaryKeyword }}</mat-chip>
            <p class="status-line">Estado: <strong>{{ selectedArticle().status }}</strong></p>
          </mat-card>
        </section>

        @if (workspaceStore.publishMessage()) {
          <mat-card class="publish-state">
            <mat-icon>info</mat-icon>
            <span>{{ workspaceStore.publishMessage() }}</span>
          </mat-card>
        }

        <div class="device-stage">
          <div class="browser-chrome" [class.browser-chrome--mobile]="viewport() === 'mobile'">
            <div class="chrome-dots">
              <span></span>
              <span></span>
              <span></span>
            </div>
            <div class="browser-url">https://preview.bitacoratech.local/{{ slug() }}</div>
          </div>

          <div class="device-frame" [class.device-frame--mobile]="viewport() === 'mobile'">
            <article class="wordpress-preview">
              @if (heroImage()) {
                <img class="hero-image" [src]="heroImage()" [alt]="selectedArticle().title" />
              }

              <div class="post-header">
                <mat-chip>{{ selectedArticle().status }}</mat-chip>
                <h1>{{ selectedArticle().title }}</h1>
                <p>{{ selectedArticle().excerpt }}</p>
              </div>

              @if (workspaceStore.articleLoading()) {
                <div class="loading-copy">Cargando HTML real...</div>
              }

              <div class="post-body" [innerHTML]="selectedArticle().htmlContent"></div>
            </article>
          </div>
        </div>
      </div>
    </section>
  `,
  styles: [`
    :host { display: block; }
    .preview-shell {
      display: grid;
      gap: 1rem;
      align-items: start;
    }
    .article-list,
    .control-bar,
    .insight-card,
    .publish-state {
      padding: 1rem;
      border-radius: 1.5rem;
      border: 1px solid var(--app-border);
      background: var(--app-surface);
      box-shadow: 0 20px 40px rgba(15, 23, 42, 0.08);
    }
    .preview-column {
      display: grid;
      gap: 1rem;
    }
    .eyebrow {
      margin: 0;
      color: var(--app-muted);
      text-transform: uppercase;
      letter-spacing: 0.12em;
      font-size: 0.72rem;
    }
    .list-head,
    .control-bar,
    .score-top {
      display: flex;
      justify-content: space-between;
      gap: 1rem;
      align-items: start;
    }
    .list-head h2,
    .control-bar h2,
    .score-top h3 {
      margin: 0.35rem 0 0;
    }
    .list-head p,
    .meta-line,
    .status-line,
    .control-bar p:not(.eyebrow) {
      margin: 0.35rem 0 0;
      color: var(--app-muted);
    }
    .meta-line {
      display: flex;
      gap: 0.75rem;
      flex-wrap: wrap;
    }
    .mini-state {
      padding: 0.45rem 0.7rem;
      border-radius: 999px;
      background: color-mix(in srgb, var(--app-primary) 12%, transparent);
      color: var(--app-primary);
      font-size: 0.82rem;
      font-weight: 700;
    }
    mat-nav-list a {
      border-radius: 1rem;
      margin-bottom: 0.5rem;
    }
    mat-nav-list a.active {
      background: color-mix(in srgb, var(--app-primary) 12%, transparent);
    }
    .list-score {
      display: grid;
      place-items: center;
      width: 2.5rem;
      height: 2.5rem;
      border-radius: 999px;
      background: color-mix(in srgb, var(--app-primary) 12%, transparent);
      color: var(--app-primary);
      font-weight: 800;
    }
    .control-actions {
      display: grid;
      gap: 0.75rem;
      justify-items: end;
    }
    .publish-btn {
      --mdc-filled-button-container-color: #1f7a4f;
      --mdc-filled-button-label-text-color: #fff;
    }
    .insights-grid {
      display: grid;
      gap: 1rem;
    }
    .seo-card {
      background:
        linear-gradient(135deg, color-mix(in srgb, var(--app-primary) 10%, white), transparent 65%),
        var(--app-surface);
    }
    .score-chip {
      padding: 0.65rem 0.85rem;
      border-radius: 999px;
      font-weight: 700;
      background: color-mix(in srgb, #f59e0b 18%, transparent);
      color: #b45309;
    }
    .score-chip.good {
      background: color-mix(in srgb, #22c55e 18%, transparent);
      color: #15803d;
    }
    .score-chip.warn {
      background: color-mix(in srgb, #f59e0b 18%, transparent);
      color: #b45309;
    }
    .stat-pair {
      display: flex;
      align-items: baseline;
      gap: 0.5rem;
      margin-top: 0.75rem;
    }
    .stat-pair strong {
      font-size: 1.8rem;
      line-height: 1;
    }
    .publish-state {
      display: flex;
      gap: 0.75rem;
      align-items: center;
    }
    .device-stage {
      padding: 1rem;
      border-radius: 1.75rem;
      background:
        radial-gradient(circle at top left, color-mix(in srgb, var(--app-primary) 18%, transparent), transparent 35%),
        linear-gradient(180deg, #f8fafc, #eef2ff);
      border: 1px solid var(--app-border);
    }
    .browser-chrome {
      display: flex;
      align-items: center;
      gap: 1rem;
      padding: 0.85rem 1rem;
      border-radius: 1.1rem 1.1rem 0 0;
      background: #dbe4f3;
      border: 1px solid #c7d2e3;
      border-bottom: none;
    }
    .browser-chrome--mobile {
      max-width: 430px;
      margin-inline: auto;
    }
    .chrome-dots {
      display: flex;
      gap: 0.4rem;
    }
    .chrome-dots span {
      width: 0.7rem;
      height: 0.7rem;
      border-radius: 999px;
      background: #94a3b8;
    }
    .browser-url {
      min-width: 0;
      flex: 1;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
      padding: 0.65rem 0.9rem;
      border-radius: 999px;
      background: rgba(255, 255, 255, 0.85);
      color: #475569;
      font-size: 0.9rem;
    }
    .device-frame {
      overflow: hidden;
      border-radius: 0 0 1.4rem 1.4rem;
      border: 1px solid #c7d2e3;
      background: #fff;
      min-height: 720px;
    }
    .device-frame--mobile {
      max-width: 430px;
      min-height: 780px;
      margin-inline: auto;
      border-radius: 0 0 2rem 2rem;
    }
    .wordpress-preview {
      color: #0f172a;
      background: #fff;
    }
    .hero-image {
      display: block;
      width: 100%;
      aspect-ratio: 16 / 7;
      object-fit: cover;
      background: #e2e8f0;
    }
    .post-header {
      max-width: 760px;
      margin: 0 auto;
      padding: 2rem 1.25rem 1rem;
    }
    .post-header h1 {
      margin: 1rem 0 0.75rem;
      font-size: clamp(2rem, 4vw, 3.5rem);
      line-height: 1.05;
      font-weight: 800;
    }
    .post-header p {
      margin: 0;
      color: #475569;
      font-size: 1.05rem;
      line-height: 1.7;
    }
    .loading-copy {
      max-width: 760px;
      margin: 0 auto;
      padding: 0 1.25rem 1rem;
      color: #64748b;
    }
    .post-body {
      max-width: 760px;
      margin: 0 auto;
      padding: 0 1.25rem 3rem;
      color: #1e293b;
      line-height: 1.85;
      font-size: 1.02rem;
    }
    .post-body :is(h2, h3, h4) {
      margin: 2rem 0 0.85rem;
      line-height: 1.2;
      color: #0f172a;
    }
    .post-body :is(p, ul, ol, blockquote, figure) {
      margin: 0 0 1.2rem;
    }
    .post-body :is(ul, ol) {
      padding-left: 1.25rem;
    }
    .post-body img {
      display: block;
      width: 100%;
      height: auto;
      border-radius: 1rem;
    }
    .post-body blockquote {
      margin-left: 0;
      padding: 1rem 1.25rem;
      border-left: 4px solid #94a3b8;
      background: #f8fafc;
    }
    @media (min-width: 1100px) {
      .preview-shell {
        grid-template-columns: 340px minmax(0, 1fr);
      }
      .insights-grid {
        grid-template-columns: 1.2fr 0.8fr 0.8fr;
      }
    }
    @media (max-width: 768px) {
      .control-bar,
      .list-head,
      .score-top {
        flex-direction: column;
      }
      .control-actions {
        width: 100%;
        justify-items: stretch;
      }
      .device-stage {
        padding: 0.75rem;
      }
      .browser-chrome {
        gap: 0.75rem;
      }
      .post-header {
        padding-top: 1.5rem;
      }
    }
  `]
})
export class ArticlePreviewComponent implements OnInit {
  protected readonly workspaceStore = inject(WorkspaceStore);
  protected readonly viewport = signal<'desktop' | 'mobile'>('desktop');
  protected readonly selectedArticle = this.workspaceStore.selectedArticle;

  protected readonly contentMetrics = computed(() => {
    const html = this.selectedArticle()?.htmlContent ?? '';
    const text = html.replace(/<[^>]+>/g, ' ').replace(/\s+/g, ' ').trim();
    const words = text ? text.split(' ').length : 0;
    const images = (html.match(/<img\b/gi) ?? []).length;
    return { words, images };
  });

  ngOnInit() {
    this.workspaceStore.loadDashboard();
  }

  protected seoLabel() {
    const score = this.selectedArticle().seoScore;
    if (score >= 85) {
      return 'Excelente';
    }
    if (score >= 70) {
      return 'Bueno';
    }
    return 'Mejorable';
  }

  protected estimatedReadingTime() {
    const words = this.contentMetrics().words;
    return `${Math.max(1, Math.ceil(words / 220))} min read`;
  }

  protected slug() {
    return this.selectedArticle()
      .title.toLowerCase()
      .replace(/[^a-z0-9\s-]/g, '')
      .trim()
      .replace(/\s+/g, '-');
  }

  protected heroImage() {
    const html = this.selectedArticle().htmlContent;
    const match = html.match(/<img[^>]+src=["']([^"']+)["']/i);
    return match?.[1] ?? null;
  }
}
