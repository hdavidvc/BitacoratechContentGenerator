import { CommonModule } from '@angular/common';
import { Component, computed, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { WorkspaceStore } from '../../state/workspace.store';

@Component({
  selector: 'app-seo-score',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatButtonModule,
    MatCardModule,
    MatChipsModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule
  ],
  template: `
    <section class="seo-grid">
      <mat-card class="panel">
        <p class="eyebrow">SEO score</p>
        <h2>Analiza tu contenido antes de publicar.</h2>

        <form [formGroup]="form" (ngSubmit)="analyze()" class="form-grid">
          <mat-form-field appearance="outline">
            <mat-label>Título</mat-label>
            <input matInput formControlName="title" />
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Primary keyword</mat-label>
            <input matInput formControlName="primaryKeyword" />
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>HTML content</mat-label>
            <textarea matInput rows="10" formControlName="htmlContent"></textarea>
          </mat-form-field>

          <button mat-flat-button type="submit" [disabled]="form.invalid || workspaceStore.seoLoading()">
            {{ workspaceStore.seoLoading() ? 'Analizando...' : 'Analizar SEO' }}
          </button>
        </form>
      </mat-card>

      <mat-card class="panel">
        <div class="score-head">
          <div>
            <p class="eyebrow">Resultado</p>
            <h3>{{ workspaceStore.seoReport().score }}/100</h3>
          </div>
          <div class="score-badge" [class.score-badge--good]="grade() >= 80">
            {{ gradeLabel() }}
          </div>
        </div>

        <mat-progress-bar mode="determinate" [value]="workspaceStore.seoReport().score"></mat-progress-bar>

        <div class="metric-grid">
          <div>
            <span>Densidad keyword</span>
            <strong>{{ workspaceStore.seoReport().keywordDensity }}%</strong>
          </div>
          <div>
            <span>Legibilidad</span>
            <strong>{{ workspaceStore.seoReport().readability }}</strong>
          </div>
          <div>
            <span>Palabras</span>
            <strong>{{ workspaceStore.seoReport().readabilityMetrics.wordCount }}</strong>
          </div>
          <div>
            <span>Reading ease</span>
            <strong>{{ workspaceStore.seoReport().readabilityMetrics.readingEase }}</strong>
          </div>
        </div>

        <div class="result-block">
          <h4>Recomendaciones</h4>
          @for (item of workspaceStore.seoReport().recommendations; track item) {
            <p><mat-icon>north_east</mat-icon>{{ item }}</p>
          }
        </div>

        <div class="result-block">
          <h4>Semantic keywords</h4>
          <div class="chips">
            @for (item of workspaceStore.seoReport().semanticKeywords; track item) {
              <mat-chip>{{ item }}</mat-chip>
            }
          </div>
        </div>
      </mat-card>
    </section>
  `,
  styles: [`
    .seo-grid { display: grid; gap: 1rem; }
    .panel {
      padding: 1rem;
      border-radius: 1.5rem;
      background: var(--app-surface);
      border: 1px solid var(--app-border);
    }
    .eyebrow {
      margin: 0;
      color: var(--app-muted);
      text-transform: uppercase;
      letter-spacing: 0.12em;
      font-size: 0.72rem;
    }
    .form-grid { display: grid; gap: 1rem; }
    .score-head {
      display: flex;
      justify-content: space-between;
      gap: 1rem;
      align-items: center;
      margin-bottom: 1rem;
    }
    .score-head h3 { margin: 0.25rem 0 0; font-size: 2.8rem; }
    .score-badge {
      padding: 0.75rem 1rem;
      border-radius: 999px;
      background: color-mix(in srgb, #f59e0b 15%, transparent);
      color: #b45309;
      font-weight: 700;
    }
    .score-badge--good {
      background: color-mix(in srgb, #22c55e 16%, transparent);
      color: #15803d;
    }
    .metric-grid {
      display: grid;
      grid-template-columns: repeat(2, 1fr);
      gap: 1rem;
      margin: 1rem 0;
    }
    .metric-grid span { display: block; color: var(--app-muted); }
    .result-block { margin-top: 1.5rem; }
    .result-block p {
      display: flex;
      gap: 0.5rem;
      align-items: center;
      margin: 0 0 0.75rem;
    }
    .chips { display: flex; gap: 0.5rem; flex-wrap: wrap; }
    @media (min-width: 1100px) {
      .seo-grid { grid-template-columns: minmax(0, 1fr) 420px; }
    }
  `]
})
export class SeoScoreComponent {
  private readonly formBuilder = inject(FormBuilder);
  protected readonly workspaceStore = inject(WorkspaceStore);
  protected readonly form = this.formBuilder.nonNullable.group({
    articleId: [''],
    title: ['Angular 20 para equipos editoriales: pipeline, previews y SEO', Validators.required],
    primaryKeyword: ['angular 20 seo', Validators.required],
    htmlContent: [
      '<h1>Angular 20 para equipos editoriales</h1><p>Señales, dashboard y calidad SEO en un mismo frontend.</p>',
      Validators.required
    ]
  });

  protected readonly grade = computed(() => this.workspaceStore.seoReport().score);
  protected readonly gradeLabel = computed(() => {
    const score = this.grade();
    if (score >= 80) {
      return 'Listo para publicar';
    }
    if (score >= 65) {
      return 'Necesita ajustes';
    }
    return 'Riesgo alto';
  });

  protected analyze() {
    if (this.form.invalid) {
      return;
    }

    const payload = this.form.getRawValue();
    this.workspaceStore.analyzeSeo({
      articleId: payload.articleId || undefined,
      title: payload.title,
      primaryKeyword: payload.primaryKeyword,
      htmlContent: payload.htmlContent
    });
  }
}
