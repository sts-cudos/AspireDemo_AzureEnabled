import { AsyncPipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component, inject } from '@angular/core';
import { BehaviorSubject } from 'rxjs';

@Component({
  selector: 'app-page',
  standalone: true,
  imports: [AsyncPipe],
  templateUrl: './page.component.html',
  styleUrl: './page.component.scss'
})
export class PageComponent {
  private client: HttpClient = inject(HttpClient);

  pingResult = new BehaviorSubject<string>('');
  addResult = new BehaviorSubject<string>('');;
  postgresResult = new BehaviorSubject<string>('');;
  
  async ping() {
    this.client.get('/api/ping', { responseType: 'text' }).pipe().subscribe((data: string) => {
      this.pingResult.next(data);
    });
  }

  async add() {
    this.client.get(`/api/add`).pipe().subscribe((data: any) => {
      this.addResult.next(data.sum);
    });
  }

  async postgres() {
    this.client.get(`/api/answer-from-db`).pipe().subscribe((data: any) => {
      this.postgresResult.next(data.answer);
    });
  }
}
