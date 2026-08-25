import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { ProjectsAdminService, ProjectRate } from '../../../../core/services/projects-admin.service';
import { PaymentModel } from '../../../../core/models/project.models';

@Component({
  selector: 'app-project-edit-page',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './project-edit-page.html',
})
export class ProjectEditPage {
  private readonly projectsAdmin = inject(ProjectsAdminService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  private readonly editId = this.route.snapshot.paramMap.get('id');
  protected readonly isEditMode = this.editId !== null;
  private readonly clientIdFromRoute = Number(this.route.snapshot.paramMap.get('clientId'));
  private readonly projectClientId = signal(this.clientIdFromRoute);

  protected readonly name = signal('');
  protected readonly code = signal('');
  protected readonly description = signal('');
  protected readonly paymentModel = signal<PaymentModel>('TimeAndMaterials');
  protected readonly currencyOverride = signal('');
  protected readonly startDate = signal(new Date().toISOString().slice(0, 10));
  protected readonly endDate = signal('');
  protected readonly budgetHours = signal<number | null>(null);
  protected readonly fixedFeeAmount = signal<number | null>(null);
  protected readonly budgetAlertThresholdPercent = signal(80);
  protected readonly isActive = signal(true);
  protected readonly defaultCostRatePerHour = signal(0);
  protected readonly defaultBillingRatePerHour = signal<number | null>(null);
  protected readonly error = signal<string | null>(null);

  protected readonly rates = signal<ProjectRate[]>([]);
  protected readonly newRateUserId = signal<number | null>(null);
  protected readonly newRateBillingRate = signal<number | null>(null);
  protected readonly newRateCostRate = signal(0);
  protected readonly newRateEffectiveFrom = signal(new Date().toISOString().slice(0, 10));

  constructor() {
    if (this.editId) {
      const id = Number(this.editId);
      this.projectsAdmin.getById(id).subscribe((p) => {
        this.projectClientId.set(p.clientId);
        this.name.set(p.name);
        this.code.set(p.code);
        this.description.set(p.description ?? '');
        this.paymentModel.set(p.paymentModel);
        this.currencyOverride.set(p.currencyOverride ?? '');
        this.startDate.set(p.startDate);
        this.endDate.set(p.endDate ?? '');
        this.budgetHours.set(p.budgetHours);
        this.fixedFeeAmount.set(p.fixedFeeAmount);
        this.budgetAlertThresholdPercent.set(p.budgetAlertThresholdPercent);
        this.isActive.set(p.isActive);
      });
      this.refreshRates(id);
    }
  }

  private refreshRates(projectId: number): void {
    this.projectsAdmin.listRates(projectId).subscribe((rates) => this.rates.set(rates));
  }

  protected save(): void {
    if (this.isEditMode) {
      this.projectsAdmin
        .update(Number(this.editId), {
          name: this.name(),
          description: this.description() || null,
          currencyOverride: this.currencyOverride() || null,
          endDate: this.endDate() || null,
          budgetHours: this.budgetHours(),
          fixedFeeAmount: this.fixedFeeAmount(),
          budgetAlertThresholdPercent: this.budgetAlertThresholdPercent(),
          isActive: this.isActive(),
        })
        .subscribe({
          next: () => this.router.navigate(['/admin/clients', this.projectClientId(), 'projects']),
          error: (err) => this.error.set(err?.error?.error ?? 'Could not save the project.'),
        });
      return;
    }

    this.projectsAdmin
      .create({
        clientId: this.clientIdFromRoute,
        name: this.name(),
        code: this.code(),
        description: this.description() || null,
        paymentModel: this.paymentModel(),
        currencyOverride: this.currencyOverride() || null,
        startDate: this.startDate(),
        endDate: this.endDate() || null,
        budgetHours: this.budgetHours(),
        fixedFeeAmount: this.fixedFeeAmount(),
        budgetAlertThresholdPercent: this.budgetAlertThresholdPercent(),
        defaultCostRatePerHour: this.defaultCostRatePerHour(),
        defaultBillingRatePerHour: this.defaultBillingRatePerHour(),
      })
      .subscribe({
        next: () => this.router.navigate(['/admin/clients', this.clientIdFromRoute, 'projects']),
        error: (err) => this.error.set(err?.error?.error ?? 'Could not create the project.'),
      });
  }

  protected addRate(): void {
    if (!this.editId) return;
    this.projectsAdmin
      .createRate(Number(this.editId), {
        userId: this.newRateUserId(),
        billingRatePerHour: this.newRateBillingRate(),
        costRatePerHour: this.newRateCostRate(),
        effectiveFrom: this.newRateEffectiveFrom(),
        effectiveTo: null,
      })
      .subscribe({
        next: () => this.refreshRates(Number(this.editId)),
        error: (err) => this.error.set(err?.error?.error ?? 'Could not add the rate.'),
      });
  }

  protected cancel(): void {
    this.router.navigate(['/admin/clients']);
  }
}
