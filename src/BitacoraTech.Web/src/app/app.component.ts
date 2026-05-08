import { Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { ThemeStore } from './core/theme/theme.store';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet],
  template: '<router-outlet />'
})
export class AppComponent {
  private readonly themeStore = inject(ThemeStore);

  constructor() {
    this.themeStore.init();
  }
}
