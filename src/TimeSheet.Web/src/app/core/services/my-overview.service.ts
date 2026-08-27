import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { MyOverviewLine } from '../models/my-overview.models';

@Injectable({ providedIn: 'root' })
export class MyOverviewService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/me/overview`;

  get(): Observable<MyOverviewLine[]> {
    return this.http.get<MyOverviewLine[]>(this.baseUrl);
  }
}
