import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-home-page',
  standalone: true,
  imports: [RouterLink],
  template: `
    <div class="container py-3">
      <h2 class="mb-3">Cohort</h2>
      <div class="list-group">
        <a class="list-group-item list-group-item-action" routerLink="/host">Host</a>
        <a class="list-group-item list-group-item-action" routerLink="/admin">Admin</a>
        <a class="list-group-item list-group-item-action" routerLink="/participant">Participant</a>
        <a class="list-group-item list-group-item-action" routerLink="/profile">Profile</a>
      </div>
    </div>
  `,
})
export class HomePage {}
