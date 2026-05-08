import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { AppShellComponent } from './core/layout/app-shell.component';

export const routes: Routes = [
  {
    path: 'login',
    loadComponent: () => import('./features/login/login.component').then((m) => m.LoginComponent)
  },
  {
    path: '',
    component: AppShellComponent,
    canActivate: [authGuard],
    children: [
      {
        path: '',
        redirectTo: 'dashboard',
        pathMatch: 'full'
      },
      {
        path: 'dashboard',
        loadChildren: () => import('./features/dashboard/dashboard.routes').then((m) => m.dashboardRoutes)
      },
      {
        path: 'articles',
        loadChildren: () => import('./features/articles/articles.routes').then((m) => m.articlesRoutes)
      },
      {
        path: 'keywords',
        loadChildren: () => import('./features/keywords/keywords.routes').then((m) => m.keywordsRoutes)
      },
      {
        path: 'seo',
        loadChildren: () => import('./features/seo/seo.routes').then((m) => m.seoRoutes)
      }
    ]
  },
  {
    path: '**',
    redirectTo: ''
  }
];
