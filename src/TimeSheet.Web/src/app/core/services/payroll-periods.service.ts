import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PayrollPeriod, PayrollPeriodDetail } from '../models/payroll-period.models';

@Injectable({ providedIn: 'root' })
export class PayrollPeriodsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/payroll-periods`;

  list(): Observable<PayrollPeriod[]> {
    return this.http.get<PayrollPeriod[]>(this.baseUrl);
  }

  getById(id: number): Observable<PayrollPeriodDetail> {
    return this.http.get<PayrollPeriodDetail>(`${this.baseUrl}/${id}`);
  }

  runNow(): Observable<PayrollPeriod> {
    return this.http.post<PayrollPeriod>(`${this.baseUrl}/run-now`, {});
  }
}
