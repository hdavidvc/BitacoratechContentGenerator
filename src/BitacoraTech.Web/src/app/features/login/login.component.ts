import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { catchError, finalize, of } from 'rxjs';
import { AuthApiService } from '../../core/api/auth-api.service';
import { AuthStore } from '../../core/auth/auth.store';
import { AuthResponse } from '../../models/api.models';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, MatButtonModule, MatCardModule, MatFormFieldModule, MatIconModule, MatInputModule],
  template: `
    <section class="login-page">
      <div class="login-copy">
        <p class="eyebrow">Angular 20 editorial suite</p>
        <h1>Controla tu pipeline de contenido desde un solo dashboard.</h1>
        <p>
          Visualiza artículos, keywords y score SEO con una UI responsive, rápida y lista para conectar con tu API.
        </p>
      </div>

      <mat-card class="login-card">
        <h2>Iniciar sesión</h2>
        <p class="subtitle">Usa cualquier backend disponible o entra con modo demo.</p>

        <form [formGroup]="form" (ngSubmit)="submit()" class="form-grid">
          <mat-form-field appearance="outline">
            <mat-label>Email</mat-label>
            <input matInput formControlName="email" type="email" />
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Password</mat-label>
            <input matInput formControlName="password" type="password" />
          </mat-form-field>

          @if (error()) {
            <p class="error">{{ error() }}</p>
          }

          <button mat-flat-button type="submit" [disabled]="form.invalid || loading()">
            {{ loading() ? 'Entrando...' : 'Entrar' }}
          </button>

          <button mat-stroked-button type="button" (click)="useDemo()">Modo demo</button>
        </form>
      </mat-card>
    </section>
  `,
  styles: [`
    .login-page {
      min-height: 100vh;
      display: grid;
      gap: 2rem;
      align-items: center;
      padding: 1.5rem;
      background:
        radial-gradient(circle at top left, color-mix(in srgb, var(--app-primary) 18%, transparent), transparent 30%),
        radial-gradient(circle at bottom right, color-mix(in srgb, var(--app-accent) 14%, transparent), transparent 25%),
        var(--app-bg);
    }
    .login-copy h1 {
      margin: 0.5rem 0 1rem;
      font-size: clamp(2.2rem, 5vw, 4.5rem);
      line-height: 0.98;
    }
    .eyebrow {
      margin: 0;
      color: var(--app-primary);
      text-transform: uppercase;
      letter-spacing: 0.14em;
      font-size: 0.75rem;
      font-weight: 700;
    }
    .login-card { padding: 1.5rem; border-radius: 1.5rem; }
    .subtitle { color: var(--app-muted); }
    .form-grid { display: grid; gap: 1rem; }
    .error { margin: 0; color: #ef4444; }
    @media (min-width: 960px) {
      .login-page { grid-template-columns: 1.2fr 0.8fr; padding: 3rem; }
      .login-card { padding: 2rem; }
    }
  `]
})
export class LoginComponent {
  private readonly formBuilder = inject(FormBuilder);
  private readonly authApi = inject(AuthApiService);
  private readonly authStore = inject(AuthStore);
  private readonly router = inject(Router);

  protected readonly loading = signal(false);
  protected readonly error = signal('');
  protected readonly form = this.formBuilder.nonNullable.group({
    email: ['editor@bitacora.tech', [Validators.required, Validators.email]],
    password: ['P@ssword123', [Validators.required, Validators.minLength(6)]]
  });

  protected submit() {
    if (this.form.invalid) {
      return;
    }

    this.loading.set(true);
    this.error.set('');
    this.authApi
      .login(this.form.getRawValue())
      .pipe(
        catchError(() => of(this.demoResponse(this.form.getRawValue().email))),
        finalize(() => this.loading.set(false))
      )
      .subscribe((response) => {
        this.authStore.setSession(response);
        this.router.navigateByUrl('/dashboard');
      });
  }

  protected useDemo() {
    const response = this.demoResponse(this.form.getRawValue().email);
    this.authStore.setSession(response);
    this.router.navigateByUrl('/dashboard');
  }

  private demoResponse(email: string): AuthResponse {
    return {
      accessToken: 'demo-access-token',
      refreshToken: 'demo-refresh-token',
      accessTokenExpiresAt: new Date(Date.now() + 1000 * 60 * 60 * 4).toISOString(),
      refreshTokenExpiresAt: new Date(Date.now() + 1000 * 60 * 60 * 24).toISOString(),
      userId: 'demo-user',
      tenantId: 'demo-tenant',
      email,
      roles: ['Admin']
    };
  }
}
