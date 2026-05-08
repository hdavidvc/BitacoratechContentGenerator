import { CommonModule, DatePipe } from '@angular/common';
import { Component, inject } from '@angular/core';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { RouterLink } from '@angular/router';
import { WorkspaceStore } from '../../state/workspace.store';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, DatePipe, RouterLink, MatCardModule, MatChipsModule, MatIconModule, MatProgressBarModule],
  template: `
    <section class="page-grid">
      <div class="hero card-surface">
        <div>
          <p class="eyebrow">Dashboard</p>
          <h2>Visión rápida del rendimiento editorial.</h2>
          <p class="muted">Monitorea artículos, oportunidades y calidad SEO sin salir del flujo de trabajo.</p>
        </div>
        <div class="hero-actions">
          <a routerLink="/articles" class="pill-action">Abrir article preview</a>
          <a routerLink="/seo" class="pill-action pill-action--secondary">Ir a SEO score</a>
        </div>
      </div>

      <div class="stats-grid">
        @for (stat of workspaceStore.dashboardStats(); track stat.label) {
          <mat-card class="metric-card">
            <p class="metric-card__label">{{ stat.label }}</p>
            <h3>{{ stat.value }}</h3>
            <span>{{ stat.helper }}</span>
          </mat-card>
        }
      </div>

      <div class="content-grid">
        <mat-card class="card-surface">
          <div class="section-header">
            <div>
              <p class="eyebrow">Pipeline</p>
              <h3>Artículos recientes</h3>
            </div>
          </div>

          @for (article of workspaceStore.articles(); track article.id) {
            <article class="list-row">
              <div>
                <strong>{{ article.title }}</strong>
                <p>{{ article.excerpt }}</p>
                <span>{{ article.updatedAt | date:'mediumDate' }}</span>
              </div>
              <div class="list-row__meta">
                <mat-chip>{{ article.status }}</mat-chip>
                <div class="score-pill">{{ article.seoScore }}</div>
              </div>
            </article>
          }
        </mat-card>

        <mat-card class="card-surface">
          <p class="eyebrow">SEO snapshot</p>
          <h3>{{ workspaceStore.seoReport().score }}/100</h3>
          <mat-progress-bar mode="determinate" [value]="workspaceStore.seoReport().score"></mat-progress-bar>
          <div class="mini-grid">
            <div>
              <span>Legibilidad</span>
              <strong>{{ workspaceStore.seoReport().readability }}</strong>
            </div>
            <div>
              <span>Densidad</span>
              <strong>{{ workspaceStore.seoReport().keywordDensity }}%</strong>
            </div>
          </div>

          <div class="recommendations">
            @for (item of workspaceStore.seoReport().recommendations; track item) {
              <p><mat-icon>check_circle</mat-icon>{{ item }}</p>
            }
          </div>
        </mat-card>
      </div>
    </section>
  `,
  styles: [`
    .page-grid { display: grid; gap: 1rem; }
    .hero, .stats-grid, .content-grid { display: grid; gap: 1rem; }
    .hero { align-items: end; }
    .hero h2, .metric-card h3 { margin: 0; }
    .hero-actions { display: flex; gap: 0.75rem; flex-wrap: wrap; }
    .pill-action {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      min-height: 2.8rem;
      padding: 0 1rem;
      border-radius: 999px;
      background: var(--app-primary);
      color: white;
      text-decoration: none;
      font-weight: 700;
    }
    .pill-action--secondary {
      background: color-mix(in srgb, var(--app-surface) 70%, white);
      color: var(--app-text);
      border: 1px solid var(--app-border);
    }
    .stats-grid { grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); }
    .metric-card { border-radius: 1.25rem; }
    .metric-card__label, .eyebrow {
      margin: 0 0 0.35rem;
      color: var(--app-muted);
      text-transform: uppercase;
      letter-spacing: 0.12em;
      font-size: 0.72rem;
    }
    .content-grid { grid-template-columns: repeat(auto-fit, minmax(290px, 1fr)); }
    .card-surface {
      padding: 1rem;
      border-radius: 1.5rem;
      background: var(--app-surface);
      border: 1px solid var(--app-border);
    }
    .list-row {
      display: flex;
      justify-content: space-between;
      gap: 1rem;
      padding: 1rem 0;
      border-bottom: 1px solid var(--app-border);
    }
    .list-row:last-child { border-bottom: 0; }
    .list-row p, .muted { color: var(--app-muted); }
    .list-row span { font-size: 0.85rem; color: var(--app-muted); }
    .list-row__meta { display: flex; gap: 0.75rem; align-items: center; }
    .score-pill {
      display: grid;
      place-items: center;
      width: 3rem;
      height: 3rem;
      border-radius: 999px;
      background: color-mix(in srgb, var(--app-primary) 18%, transparent);
      color: var(--app-primary);
      font-weight: 800;
    }
    .mini-grid {
      display: grid;
      grid-template-columns: repeat(2, 1fr);
      gap: 1rem;
      margin: 1rem 0;
    }
    .mini-grid span { display: block; color: var(--app-muted); }
    .recommendations { display: grid; gap: 0.75rem; margin-top: 1rem; }
    .recommendations p {
      display: flex;
      gap: 0.5rem;
      align-items: center;
      margin: 0;
    }
  `]
})
export class DashboardComponent {
  protected readonly workspaceStore = inject(WorkspaceStore);

  constructor() {
    this.workspaceStore.loadDashboard();
  }
}
