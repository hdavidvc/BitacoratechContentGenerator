import { DOCUMENT } from '@angular/common';
import { Injectable, inject, signal } from '@angular/core';

const storageKey = 'bitacoratech.theme';

@Injectable({ providedIn: 'root' })
export class ThemeStore {
  private readonly document = inject(DOCUMENT);
  private readonly mode = signal<'light' | 'dark'>('light');
  readonly isDark = () => this.mode() === 'dark';

  init() {
    const saved = localStorage.getItem(storageKey) as 'light' | 'dark' | null;
    const preferred = window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
    this.setMode(saved ?? preferred);
  }

  toggle() {
    this.setMode(this.mode() === 'dark' ? 'light' : 'dark');
  }

  private setMode(mode: 'light' | 'dark') {
    this.mode.set(mode);
    localStorage.setItem(storageKey, mode);
    this.document.documentElement.classList.toggle('dark', mode === 'dark');
  }
}
