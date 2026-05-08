import { Routes } from '@angular/router';

export const routes: Routes = [
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
  },
  {
    path: 'settings',
    loadChildren: () => import('./features/settings/settings.routes').then((m) => m.settingsRoutes)
  },
  {
    path: 'wordpress',
    loadChildren: () => import('./features/wordpress/wordpress.routes').then((m) => m.wordpressRoutes)
  }
];

