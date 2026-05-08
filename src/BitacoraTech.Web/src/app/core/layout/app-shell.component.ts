import { BreakpointObserver } from '@angular/cdk/layout';
import { CommonModule } from '@angular/common';
import { Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatMenuModule } from '@angular/material/menu';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatToolbarModule } from '@angular/material/toolbar';
import { map, startWith } from 'rxjs/operators';
import { AuthStore } from '../auth/auth.store';
import { ThemeStore } from '../theme/theme.store';

@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [
    CommonModule,
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    MatButtonModule,
    MatIconModule,
    MatListModule,
    MatMenuModule,
    MatSidenavModule,
    MatToolbarModule
  ],
  template: `
    <mat-sidenav-container class="shell">
      <mat-sidenav #drawer [mode]="isMobile() ? 'over' : 'side'" [opened]="!isMobile()" class="shell-nav">
        <div class="brand">
          <div class="brand__logo">BT</div>
          <div>
            <p class="brand__eyebrow">Content OS</p>
            <h1>BitacoraTech</h1>
          </div>
        </div>

        <mat-nav-list>
          @for (item of navItems; track item.path) {
            <a mat-list-item [routerLink]="item.path" routerLinkActive="active-link" #active="routerLinkActive" [activated]="active.isActive">
              <mat-icon matListItemIcon>{{ item.icon }}</mat-icon>
              <span matListItemTitle>{{ item.label }}</span>
            </a>
          }
        </mat-nav-list>

        <div class="nav-footer">
          <p>Workspace inteligente para artículos, keywords y SEO.</p>
        </div>
      </mat-sidenav>

      <mat-sidenav-content>
        <mat-toolbar class="shell-toolbar">
          @if (isMobile()) {
            <button mat-icon-button (click)="drawer.toggle()" aria-label="Abrir menú">
              <mat-icon>menu</mat-icon>
            </button>
          }

          <div class="toolbar-copy">
            <p class="toolbar-copy__eyebrow">Editorial control center</p>
            <strong>{{ currentSection() }}</strong>
          </div>

          <span class="spacer"></span>

          <button mat-icon-button (click)="themeStore.toggle()" [attr.aria-label]="themeStore.isDark() ? 'Modo claro' : 'Modo oscuro'">
            <mat-icon>{{ themeStore.isDark() ? 'light_mode' : 'dark_mode' }}</mat-icon>
          </button>

          <button mat-button [matMenuTriggerFor]="profileMenu">
            {{ authStore.email() }}
          </button>
          <mat-menu #profileMenu="matMenu">
            <button mat-menu-item (click)="logout()">
              <mat-icon>logout</mat-icon>
              <span>Cerrar sesión</span>
            </button>
          </mat-menu>
        </mat-toolbar>

        <main class="shell-content">
          <router-outlet />
        </main>
      </mat-sidenav-content>
    </mat-sidenav-container>
  `,
  styles: [`
    .shell { min-height: 100vh; background: var(--app-bg); }
    .shell-nav {
      width: 280px;
      padding: 1rem;
      border-right: 1px solid var(--app-border);
      background:
        radial-gradient(circle at top, color-mix(in srgb, var(--app-primary) 18%, transparent), transparent 35%),
        var(--app-surface);
    }
    .brand { display: flex; gap: 1rem; align-items: center; padding: 1rem 0.5rem 1.5rem; }
    .brand__logo {
      display: grid;
      place-items: center;
      width: 3rem;
      height: 3rem;
      border-radius: 1rem;
      background: linear-gradient(135deg, var(--app-primary), var(--app-accent));
      color: white;
      font-weight: 800;
    }
    .brand__eyebrow, .toolbar-copy__eyebrow {
      margin: 0;
      font-size: 0.72rem;
      text-transform: uppercase;
      letter-spacing: 0.12em;
      color: var(--app-muted);
    }
    .brand h1, .toolbar-copy strong { margin: 0; }
    .nav-footer {
      margin-top: auto;
      padding: 1rem 0.75rem;
      color: var(--app-muted);
      font-size: 0.9rem;
    }
    .shell-toolbar {
      position: sticky;
      top: 0;
      z-index: 2;
      gap: 0.75rem;
      border-bottom: 1px solid var(--app-border);
      background: color-mix(in srgb, var(--app-surface) 88%, transparent);
      backdrop-filter: blur(18px);
    }
    .toolbar-copy { display: flex; flex-direction: column; }
    .spacer { flex: 1; }
    .shell-content {
      padding: 1rem;
    }
    .active-link {
      border-radius: 0.9rem;
      background: color-mix(in srgb, var(--app-primary) 12%, var(--app-surface));
      color: var(--app-primary);
    }
    @media (min-width: 768px) {
      .shell-content { padding: 1.5rem; }
    }
  `]
})
export class AppShellComponent {
  protected readonly authStore = inject(AuthStore);
  protected readonly themeStore = inject(ThemeStore);
  private readonly router = inject(Router);
  private readonly breakpointObserver = inject(BreakpointObserver);

  protected readonly navItems = [
    { label: 'Dashboard', path: '/dashboard', icon: 'space_dashboard' },
    { label: 'Article Preview', path: '/articles', icon: 'article' },
    { label: 'Keywords', path: '/keywords', icon: 'track_changes' },
    { label: 'SEO Score', path: '/seo', icon: 'query_stats' }
  ];

  private readonly isMobileState = toSignal(
    this.breakpointObserver.observe('(max-width: 767px)').pipe(map((state) => state.matches)),
    { initialValue: false }
  );
  private readonly currentUrl = toSignal(this.router.events.pipe(startWith(null), map(() => this.router.url)), {
    initialValue: this.router.url
  });

  protected readonly isMobile = computed(() => this.isMobileState());

  protected readonly currentSection = computed(() => {
    const path = this.currentUrl().split('?')[0];
    return this.navItems.find((item) => path.startsWith(item.path))?.label ?? 'Dashboard';
  });

  protected logout() {
    this.authStore.clear();
    this.router.navigateByUrl('/login');
  }
}
