import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ApiService } from '../services/api.service';

@Component({
  selector: 'app-host-quiz-new-page',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './host-quiz-new.page.html',
})
export class HostQuizNewPage {
  title = '';
  durationSeconds = 300;
  defaultQuestionTimeoutSeconds = 30;
  targetQuestionCount: number | null = null;

  readonly saving = signal(false);
  readonly error = signal<string | null>(null);

  constructor(private readonly api: ApiService, private readonly router: Router) {}

  async create(): Promise<void> {
    this.error.set(null);

    const trimmed = this.title.trim();
    if (!trimmed) {
      this.error.set('Title is required.');
      return;
    }

    this.saving.set(true);
    try {
      const created = await this.api.hostCreateQuiz(
        trimmed,
        Number(this.durationSeconds),
        Number(this.defaultQuestionTimeoutSeconds),
        this.targetQuestionCount !== null ? Number(this.targetQuestionCount) : null
      );
      await this.router.navigateByUrl(`/host/quizzes/${created.id}`);
    } catch {
      this.error.set('Create failed.');
    } finally {
      this.saving.set(false);
    }
  }
}
