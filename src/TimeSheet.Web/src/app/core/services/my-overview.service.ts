import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { MyOverviewLine } from '../models/my-overview.models';

@Injectable({ providedIn: 'root' })
export class MyOverviewService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/me/overview`;

  /// onBehalfOfUserId is honored server-side only for an Admin actively impersonating that user (see
  /// MeOverviewFunctions.ResolveViewTargetAsync) - mirrors TimesheetEntriesService's own identical pattern.
  get(onBehalfOfUserId?: number | null): Observable<MyOverviewLine[]> {
    let params = new HttpParams();
    if (onBehalfOfUserId) params = params.set('onBehalfOfUserId', onBehalfOfUserId);
    return this.http.get<MyOverviewLine[]>(this.baseUrl, { params });
  }
}
