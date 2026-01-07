import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { ApiService } from '../services/api.service';

@Component({
  selector: 'app-participant-signin-page',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="container py-3" style="max-width: 520px;">
      <h2 class="mb-3">Participant</h2>

      <div class="card">
        <div class="card-body">
          <label class="form-label" for="name">Name</label>
          <input id="name" class="form-control" [(ngModel)]="name" />

          @if (error()) {
            <div class="alert alert-danger mt-3">{{ error() }}</div>
          }

          <div class="d-grid gap-2 mt-3">
            <button class="btn btn-success" (click)="signInAnonymous()">Continue anonymously</button>
            <a class="btn btn-outline-secondary" [href]="oidcLoginUrl()">Sign in with OIDC</a>
          </div>
        </div>
      </div>
    </div>
  `,
})
export class ParticipantSignInPage {
  name = '';
  readonly error = signal<string | null>(null);

  constructor(
    private readonly api: ApiService,
    private readonly router: Router,
    private readonly route: ActivatedRoute
  ) {}

  oidcLoginUrl(): string {
    const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl') ?? '/participant';
    return `/auth/login?returnUrl=${encodeURIComponent(returnUrl)}`;
  }

  async signInAnonymous(): Promise<void> {
    this.error.set(null);
    const trimmed = this.name.trim();
    if (!trimmed) {
      this.error.set('Name is required.');
      return;
    }

    try {
      await this.api.anonymousParticipantSignIn(trimmed);
      const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl') ?? '/participant';
      await this.router.navigateByUrl(returnUrl);
    } catch {
      this.error.set('Sign-in failed.');
    }
  }
}
