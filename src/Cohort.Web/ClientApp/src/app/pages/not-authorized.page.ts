import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-not-authorized-page',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './not-authorized.page.html',
})
export class NotAuthorizedPage {}
