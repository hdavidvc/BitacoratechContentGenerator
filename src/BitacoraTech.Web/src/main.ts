import { bootstrapApplication } from '@angular/platform-browser';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { AppComponent } from './app/app.component';
import { routes } from './app/app.routes';
import { apiInterceptor } from './app/core/interceptors/api.interceptor';

bootstrapApplication(AppComponent, {
  providers: [provideRouter(routes), provideHttpClient(withInterceptors([apiInterceptor])), provideAnimationsAsync()]
}).catch((error) => console.error(error));
