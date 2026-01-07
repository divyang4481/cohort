import { Routes } from '@angular/router';

import { HomePage } from './pages/home.page';
import { ProfilePage } from './pages/profile.page';
import { AdminPage } from './pages/admin.page';
import { HostQuizzesPage } from './pages/host-quizzes.page';
import { HostQuizNewPage } from './pages/host-quiz-new.page';
import { HostQuizDetailsPage } from './pages/host-quiz-details.page';
import { ParticipantSignInPage } from './pages/participant-signin.page';
import { ParticipantJoinPage } from './pages/participant-join.page';
import { ParticipantRoomPage } from './pages/participant-room.page';
import { NotAuthorizedPage } from './pages/not-authorized.page';

export const routes: Routes = [
	{ path: '', component: HomePage, data: { area: 'default' } },

	{ path: 'profile', component: ProfilePage, data: { area: 'default' } },

	{ path: 'access/not-authorized', component: NotAuthorizedPage, data: { area: 'default' } },

	{ path: 'admin', component: AdminPage, data: { area: 'admin' } },

	{ path: 'host', redirectTo: 'host/quizzes', pathMatch: 'full' },
	{ path: 'host/quizzes', component: HostQuizzesPage, data: { area: 'host' } },
	{ path: 'host/quizzes/new', component: HostQuizNewPage, data: { area: 'host' } },
	{ path: 'host/quizzes/:id', component: HostQuizDetailsPage, data: { area: 'host' } },

	{ path: 'participant', redirectTo: 'participant/signin', pathMatch: 'full' },
	{ path: 'participant/signin', component: ParticipantSignInPage, data: { area: 'participant' } },
	{ path: 'participant/join/:code', component: ParticipantJoinPage, data: { area: 'participant' } },
	{ path: 'participant/room/:code', component: ParticipantRoomPage, data: { area: 'participant' } },

	{ path: '**', redirectTo: '' },
];
