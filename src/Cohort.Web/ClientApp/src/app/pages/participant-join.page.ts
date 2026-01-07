import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { ApiService } from '../services/api.service';
import type { ParticipantQuizPublicDto } from '../services/api.types';

@Component({
  selector: 'app-participant-join-page',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="container py-3">
      @if (loading()) {
        <div>Loading…</div>
      } @else if (error()) {
        <div class="alert alert-danger">{{ error() }}</div>
      } @else if (quiz()) {
        <h2 class="mb-2">Join Quiz</h2>
        <div class="card">
          <div class="card-body">
            <div><strong>Title:</strong> {{ quiz()?.title }}</div>
            <div><strong>Join Code:</strong> {{ quiz()?.joinCode }}</div>

            <div class="d-grid gap-2 mt-3" style="max-width: 360px;">
              <button class="btn btn-success" (click)="goRoom()">Enter room</button>
              <a class="btn btn-outline-secondary" [href]="oidcLoginUrl()">Sign in with OIDC</a>
              <a class="btn btn-outline-secondary" [href]="anonymousLoginUrl()">Anonymous name entry</a>
            </div>
          </div>
        </div>
      }
    </div>
  `,
})
export class ParticipantJoinPage {
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly quiz = signal<ParticipantQuizPublicDto | null>(null);

  constructor(
    private readonly api: ApiService,
    private readonly route: ActivatedRoute,
    private readonly router: Router
  ) {
    void this.load();
  }

  private get code(): string {
    return (this.route.snapshot.paramMap.get('code') ?? '').trim().toUpperCase();
  }

  oidcLoginUrl(): string {
    return `/auth/login?returnUrl=${encodeURIComponent(`/participant/room/${this.code}`)}`;
  }

  anonymousLoginUrl(): string {
    return `/participant/signin?returnUrl=${encodeURIComponent(`/participant/room/${this.code}`)}`;
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);

    try {
      this.quiz.set(await this.api.participantGetQuiz(this.code));
    } catch {
      this.error.set('Quiz not found.');
    } finally {
      this.loading.set(false);
    }
  }

  async goRoom(): Promise<void> {
    await this.router.navigateByUrl(`/participant/room/${this.code}`);
  }
}
