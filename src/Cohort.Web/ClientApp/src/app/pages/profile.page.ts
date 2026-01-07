import { Component, computed, effect } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ApiService } from '../services/api.service';

@Component({
  selector: 'app-profile-page',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="container py-3">
      <h2 class="mb-3">Profile</h2>

      @if (me(); as m) {
        <div class="card">
          <div class="card-body">
            <div><strong>Name:</strong> {{ m.name }}</div>
            <div><strong>Auth Source:</strong> {{ m.authSource }}</div>
            <div><strong>Role:</strong> {{ m.appRole }}</div>
            <div><strong>oid:</strong> {{ m.oid }}</div>
            <div><strong>tid:</strong> {{ m.tid }}</div>
            <div><strong>sub:</strong> {{ m.sub }}</div>
            <div><strong>NameIdentifier:</strong> {{ m.nameIdentifier }}</div>
          </div>
        </div>

        <h5 class="mt-3">Claims</h5>
        <table class="table table-sm table-striped">
          <thead>
            <tr>
              <th>Type</th>
              <th>Value</th>
            </tr>
          </thead>
          <tbody>
            @for (c of m.claims; track c.type + ':' + c.value) {
              <tr>
                <td class="text-nowrap">{{ c.type }}</td>
                <td class="text-break">{{ c.value }}</td>
              </tr>
            }
          </tbody>
        </table>
      } @else {
        <div class="alert alert-warning">Not signed in.</div>
      }
    </div>
  `,
})
export class ProfilePage {
  readonly me = computed(() => this.api.me());

  constructor(private readonly api: ApiService) {
    effect(() => {
      if (this.api.me() === null) {
        void this.api.tryLoadMe();
      }
    });
  }
}
