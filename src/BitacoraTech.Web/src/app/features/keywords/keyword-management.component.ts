import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { WorkspaceStore } from '../../state/workspace.store';

@Component({
  selector: 'app-keyword-management',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatButtonModule,
    MatCardModule,
    MatChipsModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressSpinnerModule
  ],
  template: `
    <section class="keywords-grid">
      <mat-card class="panel">
        <p class="eyebrow">Keyword management</p>
        <h2>Investiga y prioriza términos.</h2>

        <form [formGroup]="form" (ngSubmit)="research()" class="form-grid">
          <mat-form-field appearance="outline">
            <mat-label>Seed keyword</mat-label>
            <input matInput formControlName="seedKeyword" />
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Site Id</mat-label>
            <input matInput formControlName="siteId" />
          </mat-form-field>

          <button mat-flat-button type="submit" [disabled]="form.invalid || workspaceStore.keywordResearchLoading()">
            @if (workspaceStore.keywordResearchLoading()) {
              <mat-spinner diameter="18"></mat-spinner>
            } @else {
              Investigar keywords
            }
          </button>
        </form>

        <div class="cluster-row">
          @for (cluster of workspaceStore.keywordClusters(); track cluster.cluster) {
            <div class="cluster-pill">
              <strong>{{ cluster.cluster }}</strong>
              <span>{{ cluster.count }} keywords • {{ cluster.avgOpportunity }}/100</span>
            </div>
          }
        </div>
      </mat-card>

      <mat-card class="panel">
        <div class="table-header">
          <h3>Opportunity board</h3>
          <span>{{ workspaceStore.keywords().length }} resultados</span>
        </div>

        <div class="keyword-list">
          @for (keyword of workspaceStore.keywords(); track keyword.id) {
            <article class="keyword-row">
              <div>
                <strong>{{ keyword.keyword }}</strong>
                <p>{{ keyword.intent }} • {{ keyword.cluster }}</p>
              </div>
              <div class="keyword-metrics">
                <mat-chip>Vol {{ keyword.searchVolume }}</mat-chip>
                <mat-chip>Diff {{ keyword.difficulty }}</mat-chip>
                <div class="opportunity">{{ keyword.opportunityScore }}</div>
              </div>
            </article>
          }
        </div>
      </mat-card>
    </section>
  `,
  styles: [`
    .keywords-grid { display: grid; gap: 1rem; }
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
    .form-grid { display: grid; gap: 1rem; margin-top: 1rem; }
    .cluster-row {
      display: flex;
      gap: 0.75rem;
      flex-wrap: wrap;
      margin-top: 1rem;
    }
    .cluster-pill {
      padding: 0.85rem 1rem;
      border-radius: 1rem;
      background: color-mix(in srgb, var(--app-primary) 10%, transparent);
    }
    .cluster-pill span { display: block; color: var(--app-muted); font-size: 0.9rem; }
    .table-header, .keyword-row {
      display: flex;
      justify-content: space-between;
      gap: 1rem;
      align-items: center;
    }
    .keyword-list { display: grid; gap: 0.75rem; }
    .keyword-row {
      padding: 1rem;
      border-radius: 1rem;
      border: 1px solid var(--app-border);
    }
    .keyword-row p { margin: 0.3rem 0 0; color: var(--app-muted); }
    .keyword-metrics {
      display: flex;
      gap: 0.5rem;
      align-items: center;
      flex-wrap: wrap;
      justify-content: end;
    }
    .opportunity {
      display: grid;
      place-items: center;
      width: 3rem;
      height: 3rem;
      border-radius: 999px;
      background: color-mix(in srgb, var(--app-accent) 15%, transparent);
      font-weight: 800;
    }
    @media (min-width: 1100px) {
      .keywords-grid { grid-template-columns: 420px minmax(0, 1fr); }
    }
  `]
})
export class KeywordManagementComponent {
  private readonly formBuilder = inject(FormBuilder);
  protected readonly workspaceStore = inject(WorkspaceStore);

  protected readonly form = this.formBuilder.nonNullable.group({
    seedKeyword: ['angular content workflow', Validators.required],
    siteId: ['site-1', Validators.required]
  });

  protected research() {
    if (this.form.invalid) {
      return;
    }

    this.workspaceStore.researchKeywords(this.form.getRawValue());
  }
}
