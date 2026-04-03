import { Component, signal } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { RequestListComponent } from './components/request-list/request-list.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, RequestListComponent],
  template: `
    <app-request-list></app-request-list>
    <router-outlet></router-outlet>
  `,
  styleUrl: './app.scss'
})
export class App {
  title = 'approval-ui';
}
