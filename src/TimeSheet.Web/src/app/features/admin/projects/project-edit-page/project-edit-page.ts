import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { ProjectsAdminService } from '../../../../core/services/projects-admin.service';
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
  protected readonly canInvoice = signal(true);
  protected readonly currencyOverride = signal('');
  protected readonly startDate = signal(new Date().toISOString().slice(0, 10));
  protected readonly endDate = signal('');
  protected readonly budgetHours = signal<number | null>(null);
  protected readonly fixedFeeAmount = signal<number | null>(null);
  protected readonly budgetAlertThresholdPercent = signal(80);
  protected readonly isActive = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly saving = signal(false);

  constructor() {
    if (this.editId) {
      const id = Number(this.editId);
      this.projectsAdmin.getById(id).subscribe((p) => {
        this.projectClientId.set(p.clientId);
        this.name.set(p.name);
        this.code.set(p.code);
        this.description.set(p.description ?? '');
        this.paymentModel.set(p.paymentModel);
        // Null and false both mean "not invoiceable" everywhere else this field is read (the Contract-expense
        // picker/gate use `canInvoice !== true`) - defaulting an ambiguous null to true here would silently
        // flip a project's invoicing eligibility as a side effect of an unrelated edit.
        this.canInvoice.set(p.canInvoice ?? false);
        this.currencyOverride.set(p.currencyOverride ?? '');
        this.startDate.set(p.startDate);
        this.endDate.set(p.endDate ?? '');
        this.budgetHours.set(p.budgetHours);
        this.fixedFeeAmount.set(p.fixedFeeAmount);
        this.budgetAlertThresholdPercent.set(p.budgetAlertThresholdPercent);
        this.isActive.set(p.isActive);
      });
    }
  }

  protected save(): void {
    this.saving.set(true);

    if (this.isEditMode) {
      this.projectsAdmin
        .update(Number(this.editId), {
          name: this.name(),
          description: this.description() || null,
          canInvoice: this.canInvoice(),
          currencyOverride: this.currencyOverride() || null,
          endDate: this.endDate() || null,
          budgetHours: this.budgetHours(),
          fixedFeeAmount: this.fixedFeeAmount(),
          budgetAlertThresholdPercent: this.budgetAlertThresholdPercent(),
          isActive: this.isActive(),
        })
        .subscribe({
          next: () => this.router.navigate(['/admin/clients', this.projectClientId(), 'projects']),
          error: (err) => {
            this.saving.set(false);
            this.error.set(err?.error?.error ?? 'Could not save the project.');
          },
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
        canInvoice: this.canInvoice(),
        currencyOverride: this.currencyOverride() || null,
        startDate: this.startDate(),
        endDate: this.endDate() || null,
        budgetHours: this.budgetHours(),
        fixedFeeAmount: this.fixedFeeAmount(),
        budgetAlertThresholdPercent: this.budgetAlertThresholdPercent(),
      })
      .subscribe({
        next: () => this.router.navigate(['/admin/clients', this.clientIdFromRoute, 'projects']),
        error: (err) => {
          this.saving.set(false);
          this.error.set(err?.error?.error ?? 'Could not create the project.');
        },
      });
  }

  protected cancel(): void {
    this.router.navigate(['/admin/clients']);
  }
}
