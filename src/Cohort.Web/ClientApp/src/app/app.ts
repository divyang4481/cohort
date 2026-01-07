import { Component, computed, effect, signal } from '@angular/core';
import { Router, RouterLink, RouterOutlet, NavigationEnd, ActivatedRoute } from '@angular/router';
import { filter } from 'rxjs';
import { ApiService } from './services/api.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App {
  protected readonly title = signal('Cohort');
  readonly me = computed(() => this.api.me());
  readonly navbarClass = signal<string>('navbar-dark bg-primary');

  constructor(
    private readonly api: ApiService,
    private readonly router: Router,
    private readonly activatedRoute: ActivatedRoute
  ) {
    void this.api.loadUiConfig();
    void this.api.tryLoadMe();

    effect(() => {
      const cfg = this.api.uiConfig();
      if (!cfg) {
        return;
      }
      this.applyThemeFromRoute();
    });

    this.router.events.pipe(filter((e) => e instanceof NavigationEnd)).subscribe(() => {
      this.applyThemeFromRoute();
    });
  }

  private applyThemeFromRoute(): void {
    const cfg = this.api.uiConfig();
    const area = this.getDeepestRouteData('area') ?? 'default';
    const theme =
      (area === 'admin' && cfg?.themes.admin) ||
      (area === 'host' && cfg?.themes.host) ||
      (area === 'participant' && cfg?.themes.participant) ||
      cfg?.themes.defaultTheme;

    if (theme?.navbarClass) {
      this.navbarClass.set(theme.navbarClass);
    }
  }

  private getDeepestRouteData(key: string): any {
    let route: ActivatedRoute | null = this.activatedRoute;
    while (route?.firstChild) {
      route = route.firstChild;
    }
    return route?.snapshot.data?.[key];
  }

  async signOut(): Promise<void> {
    const me = this.api.me();
    if (me?.authSource === 'oidc') {
      window.location.href = '/auth/logout';
      return;
    }

    await this.api.logoutLocalCookie();
    await this.router.navigateByUrl('/participant/signin');
  }

  encode(value: string): string {
    return encodeURIComponent(value);
  }
}
