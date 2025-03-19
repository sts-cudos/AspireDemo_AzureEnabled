import { HttpClient } from '@angular/common/http';
import { Component, inject } from '@angular/core';

@Component({
  selector: 'app-page',
  standalone: true,
  imports: [],
  templateUrl: './page.component.html',
  styleUrl: './page.component.scss'
})
export class PageComponent {
  private client: HttpClient = inject(HttpClient);

  pingResult = '';
  addResult = '';
  postgresResult = '';
  
  async ping() {
    this.client.get('/api/ping', { responseType: 'text' }).pipe().subscribe((data: string) => {
      this.pingResult = data;
    });
  }

  async add() {
    this.client.get(`/api/add`).pipe().subscribe((data: any) => {
      this.addResult = data.sum;
    });
  }

  async postgres() {
    this.client.get(`/api/answer-from-db`).pipe().subscribe((data: any) => {
      this.postgresResult = data
    });
  }
}
