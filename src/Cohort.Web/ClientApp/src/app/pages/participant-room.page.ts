import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { ApiService } from '../services/api.service';
import type { ParticipantQuizRoomDto } from '../services/api.types';
import * as signalR from '@microsoft/signalr';

@Component({
  selector: 'app-participant-room-page',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="container py-3">
      @if (loading()) {
        <div>Loading…</div>
      } @else if (error()) {
        <div class="alert alert-danger">{{ error() }}</div>
      } @else if (room()) {
        <h2 class="mb-2">Waiting Room</h2>
        <div class="card">
          <div class="card-body">
            <div><strong>Quiz:</strong> {{ room()?.title }}</div>
            <div><strong>Join Code:</strong> {{ room()?.joinCode }}</div>
            <div><strong>You:</strong> {{ room()?.displayName }}</div>

            @if (room()?.isStarted) {
              <div class="alert alert-success mt-3">Quiz started.</div>
            } @else {
              <div class="alert alert-info mt-3">Waiting for host to start…</div>
            }
          </div>
        </div>
      }
    </div>
  `,
})
export class ParticipantRoomPage {
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly room = signal<ParticipantQuizRoomDto | null>(null);

  private hub: signalR.HubConnection | null = null;

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

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);

    try {
      await this.api.tryLoadMe();
      const me = this.api.me();
      const isParticipant = me?.appRole === 'participant';
      if (!isParticipant) {
        await this.router.navigateByUrl(
          `/participant/signin?returnUrl=${encodeURIComponent(`/participant/room/${this.code}`)}`
        );
        return;
      }

      const dto = await this.api.participantEnterRoom(this.code);
      this.room.set(dto);
      await this.connectHub();
    } catch {
      this.error.set('Unable to enter room.');
    } finally {
      this.loading.set(false);
    }
  }

  private async connectHub(): Promise<void> {
    if (this.hub) {
      return;
    }

    const conn = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/quiz')
      .withAutomaticReconnect()
      .build();

    conn.on('QuizStarted', () => {
      const current = this.room();
      if (current) {
        this.room.set({ ...current, isStarted: true });
      }
    });

    await conn.start();
    await conn.invoke('JoinQuizGroup', this.code);
    this.hub = conn;
  }
}
