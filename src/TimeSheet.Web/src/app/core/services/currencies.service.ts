import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface Currency {
  id: number;
  currencyName: string;
  currencyCode: string;
}

@Injectable({ providedIn: 'root' })
export class CurrenciesService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/currencies`;

  list(): Observable<Currency[]> {
    return this.http.get<Currency[]>(this.baseUrl);
  }
}
