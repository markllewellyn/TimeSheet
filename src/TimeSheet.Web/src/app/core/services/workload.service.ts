import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { EstimatedWeeklyWorkload } from '../models/workload.models';

@Injectable({ providedIn: 'root' })
export class WorkloadService {
  private readonly http = inject(HttpClient);

  estimatedHoursThisWeek(): Observable<EstimatedWeeklyWorkload> {
    return this.http.get<EstimatedWeeklyWorkload>(`${environment.apiBaseUrl}/me/estimated-hours-this-week`);
  }
}
