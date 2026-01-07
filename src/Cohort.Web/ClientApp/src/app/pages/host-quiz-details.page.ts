import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../services/api.service';
import type { HostQuizDetailsDto, QuizQuestionDto } from '../services/api.types';

type OptionDraft = { id?: string; text: string; isCorrect: boolean; order?: number };

@Component({
  selector: 'app-host-quiz-details-page',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './host-quiz-details.page.html',
})
export class HostQuizDetailsPage {
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly quiz = signal<HostQuizDetailsDto | null>(null);

  newQuestionText = '';
  newQuestionTimeout: number | null = null;
  newQuestionType: 'single' | 'multiple' = 'single';
  newQuestionOptions: OptionDraft[] = [
    { text: 'Option 1', isCorrect: true },
    { text: 'Option 2', isCorrect: false },
  ];
  readonly questionError = signal<string | null>(null);

  constructor(private readonly api: ApiService, private readonly route: ActivatedRoute) {
    void this.load();
  }

  private get id(): string {
    return this.route.snapshot.paramMap.get('id') ?? '';
  }

  qrUrl(): string {
    return `/api/host/quizzes/${this.id}/qr`;
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      this.quiz.set(await this.api.hostGetQuiz(this.id));
    } catch {
      this.error.set('Failed to load quiz.');
    } finally {
      this.loading.set(false);
    }
  }

  async startQuiz(): Promise<void> {
    try {
      await this.api.hostStartQuiz(this.id);
      await this.load();
    } catch {
      this.error.set('Start failed.');
    }
  }

  async publish(): Promise<void> {
    this.questionError.set(null);
    try {
      const result = await this.api.hostPublishQuiz(this.id);
      const current = this.quiz();
      if (current) {
        this.quiz.set({ ...current, isPublished: true, publishedUtc: result.publishedUtc ?? null, shareUrl: result.shareUrl, shareMessage: result.shareMessage });
      }
    } catch {
      this.error.set('Publish failed. Please ensure each question has options and a correct answer.');
    }
  }

  async copyShare(): Promise<void> {
    const text = this.quiz()?.shareMessage;
    if (!text) {
      return;
    }
    try {
      if (navigator.clipboard) {
        await navigator.clipboard.writeText(text);
      }
    } catch {
      // noop fallback
    }
  }

  onNewQuestionTypeChange(type: 'single' | 'multiple'): void {
    this.newQuestionType = type;
    this.ensureSingleCorrect(this.newQuestionOptions, type);
  }

  addNewOption(): void {
    this.newQuestionOptions = [...this.newQuestionOptions, { text: `Option ${this.newQuestionOptions.length + 1}`, isCorrect: false }];
  }

  removeNewOption(opt: OptionDraft): void {
    if (this.newQuestionOptions.length <= 2) {
      return;
    }
    this.newQuestionOptions = this.newQuestionOptions.filter(o => o !== opt);
  }

  setNewOptionCorrect(opt: OptionDraft, checked = true): void {
    if (this.newQuestionType === 'single') {
      this.newQuestionOptions = this.newQuestionOptions.map(o => ({ ...o, isCorrect: o === opt }));
    } else {
      opt.isCorrect = checked;
    }
  }

  addOption(q: QuizQuestionDto): void {
    q.options.push({ id: '', text: `Option ${q.options.length + 1}`, isCorrect: q.questionType === 'single' ? q.options.every(o => !o.isCorrect) : false, order: q.options.length + 1 });
    this.ensureSingleCorrect(q.options, q.questionType);
  }

  removeOption(q: QuizQuestionDto, opt: OptionDraft): void {
    if (q.options.length <= 2) {
      return;
    }
    q.options = q.options.filter(o => o !== opt);
    q.options.forEach((o, idx) => (o.order = idx + 1));
  }

  onQuestionTypeChanged(q: QuizQuestionDto, type: 'single' | 'multiple'): void {
    q.questionType = type;
    this.ensureSingleCorrect(q.options, type);
  }

  setOptionCorrect(q: QuizQuestionDto, opt: OptionDraft, checked = true): void {
    if (q.questionType === 'single') {
      q.options = q.options.map(o => ({ ...o, isCorrect: o === opt }));
    } else {
      opt.isCorrect = checked;
    }
  }

  private validateQuestion(text: string, options: OptionDraft[], type: 'single' | 'multiple'): string | null {
    if (!text.trim()) {
      return 'Question text is required.';
    }

    if (options.length < 2) {
      return 'At least two options are required.';
    }

    if (options.some(o => !o.text.trim())) {
      return 'Each option needs text.';
    }

    const correctCount = options.filter(o => o.isCorrect).length;
    if (type === 'single' && correctCount !== 1) {
      return 'Single choice needs exactly one correct option.';
    }

    if (type === 'multiple' && correctCount < 1) {
      return 'Multiple select needs at least one correct option.';
    }

    return null;
  }

  private toOptionPayload(options: OptionDraft[]): { id?: string; text: string; isCorrect: boolean; order: number }[] {
    return options.map((o, idx) => ({ id: o.id, text: o.text.trim(), isCorrect: !!o.isCorrect, order: idx + 1 }));
  }

  private ensureSingleCorrect(options: OptionDraft[], type: 'single' | 'multiple'): void {
    if (type !== 'single') {
      return;
    }
    let firstCorrectSet = false;
    options.forEach(o => {
      if (o.isCorrect && !firstCorrectSet) {
        firstCorrectSet = true;
      } else {
        o.isCorrect = false;
      }
    });
    if (!firstCorrectSet && options.length > 0) {
      options[0].isCorrect = true;
    }
  }

  async saveQuestion(q: QuizQuestionDto): Promise<void> {
    this.questionError.set(null);
    const text = q.text.trim();
    const validation = this.validateQuestion(text, q.options, q.questionType);
    if (validation) {
      this.questionError.set(validation);
      return;
    }

    const timeout = q.timeoutSecondsOverride == null
      ? null
      : Number(q.timeoutSecondsOverride);

    try {
      await this.api.hostUpdateQuestion(this.id, q.id, {
        text,
        timeoutSecondsOverride: timeout,
        questionType: q.questionType,
        options: this.toOptionPayload(q.options),
      });
      await this.load();
    } catch {
      this.questionError.set('Save failed.');
    }
  }

  async deleteQuestion(q: QuizQuestionDto): Promise<void> {
    this.questionError.set(null);
    try {
      await this.api.hostDeleteQuestion(this.id, q.id);
      await this.load();
    } catch {
      this.questionError.set('Delete failed.');
    }
  }

  async addQuestion(): Promise<void> {
    this.questionError.set(null);
    const text = this.newQuestionText.trim();
    const validation = this.validateQuestion(text, this.newQuestionOptions, this.newQuestionType);
    if (validation) {
      this.questionError.set(validation);
      return;
    }

    const timeout = this.newQuestionTimeout == null
      ? null
      : Number(this.newQuestionTimeout);

    try {
      await this.api.hostAddQuestion(this.id, {
        text,
        timeoutSecondsOverride: timeout,
        questionType: this.newQuestionType,
        options: this.toOptionPayload(this.newQuestionOptions),
      });
      this.resetNewQuestionForm();
      await this.load();
    } catch {
      this.questionError.set('Add failed.');
    }
  }

  private resetNewQuestionForm(): void {
    this.newQuestionText = '';
    this.newQuestionTimeout = null;
    this.newQuestionType = 'single';
    this.newQuestionOptions = [
      { text: 'Option 1', isCorrect: true },
      { text: 'Option 2', isCorrect: false },
    ];
  }
}
