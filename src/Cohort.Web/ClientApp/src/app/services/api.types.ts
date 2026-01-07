export type ClaimDto = { type: string; value: string };

export type MeDto = {
  isAuthenticated: boolean;
  name?: string | null;
  authSource?: string | null;
  participantMode?: string | null;
  appRole?: string | null;
  oid?: string | null;
  tid?: string | null;
  sub?: string | null;
  nameIdentifier?: string | null;
  claims: ClaimDto[];
};

export type UiConfigDto = {
  themes: {
    admin: { navbarClass: string };
    host: { navbarClass: string };
    participant: { navbarClass: string };
    defaultTheme: { navbarClass: string };
  };
};

export type QuizQuestionDto = {
  id: string;
  order: number;
  text: string;
  timeoutSecondsOverride?: number | null;
  effectiveTimeoutSeconds: number;
  questionType: 'single' | 'multiple';
  options: QuizQuestionOptionDto[];
};

export type QuizQuestionOptionDto = {
  id: string;
  order: number;
  text: string;
  isCorrect: boolean;
};

export type HostQuizSummaryDto = {
  id: string;
  title: string;
  joinCode: string;
  durationSeconds: number;
  targetQuestionCount?: number | null;
  isStarted: boolean;
  isPublished: boolean;
  createdUtc: string;
  startedUtc?: string | null;
  publishedUtc?: string | null;
};

export type HostQuizDetailsDto = {
  id: string;
  title: string;
  joinCode: string;
  joinUrl: string;
  shareUrl: string;
  shareMessage: string;
  durationSeconds: number;
  defaultQuestionTimeoutSeconds: number;
  targetQuestionCount?: number | null;
  isStarted: boolean;
  isPublished: boolean;
  createdUtc: string;
  startedUtc?: string | null;
  publishedUtc?: string | null;
  questions: QuizQuestionDto[];
};

export type ParticipantQuizPublicDto = {
  title: string;
  joinCode: string;
  durationSeconds: number;
  isStarted: boolean;
  startedUtc?: string | null;
};

export type ParticipantQuizRoomDto = {
  title: string;
  joinCode: string;
  displayName: string;
  durationSeconds: number;
  isStarted: boolean;
  startedUtc?: string | null;
};
