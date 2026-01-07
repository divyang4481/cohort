import { Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import type {
  HostQuizDetailsDto,
  HostQuizSummaryDto,
  MeDto,
  ParticipantQuizPublicDto,
  ParticipantQuizRoomDto,
  UiConfigDto,
} from './api.types';

@Injectable({ providedIn: 'root' })
export class ApiService {
  constructor(private readonly http: HttpClient) {}

  readonly me = signal<MeDto | null>(null);
  readonly uiConfig = signal<UiConfigDto | null>(null);

  async loadMe(): Promise<void> {
    const dto = await firstValueFrom(this.http.get<MeDto>('/api/me'));
    this.me.set(dto);
  }

  async tryLoadMe(): Promise<void> {
    try {
      await this.loadMe();
    } catch {
      this.me.set(null);
    }
  }

  async loadUiConfig(): Promise<void> {
    const dto = await firstValueFrom(this.http.get<UiConfigDto>('/api/ui-config'));
    this.uiConfig.set(dto);
  }

  async anonymousParticipantSignIn(name: string): Promise<void> {
    await firstValueFrom(
      this.http.post('/api/participant/anonymous-signin', {
        name,
      })
    );
    await this.tryLoadMe();
  }

  async logoutLocalCookie(): Promise<void> {
    await firstValueFrom(this.http.post('/api/session/logout', {}));
    this.me.set(null);
  }

  async hostListQuizzes(): Promise<HostQuizSummaryDto[]> {
    return await firstValueFrom(this.http.get<HostQuizSummaryDto[]>('/api/host/quizzes'));
  }

  async hostCreateQuiz(
    title: string,
    durationSeconds: number,
    defaultQuestionTimeoutSeconds: number,
    targetQuestionCount: number | null
  ): Promise<{ id: string; joinCode: string }> {
    return await firstValueFrom(
      this.http.post<{ id: string; joinCode: string }>('/api/host/quizzes', {
        title,
        durationSeconds,
        defaultQuestionTimeoutSeconds,
        targetQuestionCount,
      })
    );
  }

  async hostGetQuiz(id: string): Promise<HostQuizDetailsDto> {
    return await firstValueFrom(this.http.get<HostQuizDetailsDto>(`/api/host/quizzes/${id}`));
  }

  async hostStartQuiz(id: string): Promise<void> {
    await firstValueFrom(this.http.post(`/api/host/quizzes/${id}/start`, {}));
  }

  async hostAddQuestion(
    quizId: string,
    payload: {
      text: string;
      timeoutSecondsOverride: number | null;
      questionType: 'single' | 'multiple';
      options: { id?: string; text: string; isCorrect: boolean; order?: number }[];
    }
  ): Promise<void> {
    await firstValueFrom(this.http.post(`/api/host/quizzes/${quizId}/questions`, payload));
  }

  async hostUpdateQuestion(
    quizId: string,
    questionId: string,
    payload: {
      text: string;
      timeoutSecondsOverride: number | null;
      questionType: 'single' | 'multiple';
      options: { id?: string; text: string; isCorrect: boolean; order?: number }[];
    }
  ): Promise<void> {
    await firstValueFrom(this.http.put(`/api/host/quizzes/${quizId}/questions/${questionId}`, payload));
  }

  async hostDeleteQuestion(quizId: string, questionId: string): Promise<void> {
    await firstValueFrom(
      this.http.delete(`/api/host/quizzes/${quizId}/questions/${questionId}`)
    );
  }

  async hostPublishQuiz(id: string): Promise<{ shareUrl: string; shareMessage: string; publishedUtc?: string | null; isPublished: boolean }> {
    return await firstValueFrom(
      this.http.post<{ shareUrl: string; shareMessage: string; publishedUtc?: string | null; isPublished: boolean }>(
        `/api/host/quizzes/${id}/publish`,
        {}
      )
    );
  }

  async participantGetQuiz(code: string): Promise<ParticipantQuizPublicDto> {
    return await firstValueFrom(
      this.http.get<ParticipantQuizPublicDto>(`/api/participant/quizzes/${code}`)
    );
  }

  async participantEnterRoom(code: string): Promise<ParticipantQuizRoomDto> {
    return await firstValueFrom(
      this.http.post<ParticipantQuizRoomDto>(`/api/participant/quizzes/${code}/enter`, {})
    );
  }
}
