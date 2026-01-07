import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ApiService } from '../services/api.service';
import type { HostQuizSummaryDto } from '../services/api.types';

@Component({
  selector: 'app-host-quizzes-page',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './host-quizzes.page.html',
})
export class HostQuizzesPage implements OnInit {
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly quizzes = signal<HostQuizSummaryDto[]>([]);

  constructor(private readonly api: ApiService) {}

  ngOnInit(): void {
    void this.load();
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);

    try {
      this.quizzes.set(await this.api.hostListQuizzes());
    } catch {
      this.error.set('Failed to load quizzes.');
    } finally {
      this.loading.set(false);
    }
  }
}
