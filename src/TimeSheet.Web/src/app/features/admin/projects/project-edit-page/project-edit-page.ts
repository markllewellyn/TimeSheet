import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ProjectsAdminService } from '../../../../core/services/projects-admin.service';
import { UsersAdminService } from '../../../../core/services/users-admin.service';
import { PaymentModel, ProjectAttachment, ProjectEstimate, ProjectType } from '../../../../core/models/project.models';
import { AppUser } from '../../../../core/models/user.models';
import { ConfirmService } from '../../../../core/services/confirm.service';
import { LoadingSpinner } from '../../../../core/components/loading-spinner/loading-spinner';

@Component({
  selector: 'app-project-edit-page',
  standalone: true,
  imports: [FormsModule, DecimalPipe, DatePipe, RouterLink, LoadingSpinner],
  templateUrl: './project-edit-page.html',
})
export class ProjectEditPage {
  private readonly projectsAdmin = inject(ProjectsAdminService);
  private readonly usersAdmin = inject(UsersAdminService);
  private readonly confirmService = inject(ConfirmService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  private readonly editId = this.route.snapshot.paramMap.get('id');
  protected readonly isEditMode = this.editId !== null;
  // A brand-new project's form is immediately usable - only edit mode gates on the project record arriving.
  protected readonly loaded = signal(!this.isEditMode);
  private readonly clientIdFromRoute = Number(this.route.snapshot.paramMap.get('clientId'));
  private readonly projectClientId = signal(this.clientIdFromRoute);

  protected readonly name = signal('');
  protected readonly code = signal('');
  protected readonly description = signal('');
  protected readonly paymentModel = signal<PaymentModel>('TimeAndMaterials');
  protected readonly projectType = signal<ProjectType>('Development');
  protected readonly projectManagerUserId = signal<number | null>(null);
  protected readonly users = signal<AppUser[]>([]);
  protected readonly canInvoice = signal(true);
  protected readonly isCostExempt = signal(false);
  protected readonly currencyOverride = signal('');
  protected readonly startDate = signal(new Date().toISOString().slice(0, 10));
  protected readonly endDate = signal('');
  protected readonly budgetHours = signal<number | null>(null);
  protected readonly fixedFeeAmount = signal<number | null>(null);
  protected readonly isActive = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly saving = signal(false);

  protected readonly estimate = signal<ProjectEstimate | null>(null);

  protected readonly attachments = signal<ProjectAttachment[]>([]);
  // Only ever false->true, on the first loadAttachments() call - later reloads (after upload/delete) don't
  // reset it, so those don't flash the spinner/empty-state over an already-visible list.
  protected readonly attachmentsLoaded = signal(false);
  protected readonly uploading = signal(false);
  protected readonly attachmentsError = signal<string | null>(null);

  constructor() {
    this.usersAdmin.list(false).subscribe((users) => this.users.set(users));

    if (this.editId) {
      const id = Number(this.editId);
      this.projectsAdmin.getEstimate(id).subscribe((e) => this.estimate.set(e));
      this.loadAttachments(id);
      this.projectsAdmin.getById(id).subscribe({
        next: (p) => {
          this.projectClientId.set(p.clientId);
          this.name.set(p.name);
          this.code.set(p.code);
          this.description.set(p.description ?? '');
          this.paymentModel.set(p.paymentModel);
          this.projectType.set(p.projectType);
          this.projectManagerUserId.set(p.projectManagerUserId);
          // Null and false both mean "not invoiceable" everywhere else this field is read (the Contract-expense
          // picker/gate use `canInvoice !== true`) - defaulting an ambiguous null to true here would silently
          // flip a project's invoicing eligibility as a side effect of an unrelated edit.
          this.canInvoice.set(p.canInvoice ?? false);
          this.isCostExempt.set(p.isCostExempt);
          this.currencyOverride.set(p.currencyOverride ?? '');
          this.startDate.set(p.startDate);
          this.endDate.set(p.endDate ?? '');
          this.budgetHours.set(p.budgetHours);
          this.fixedFeeAmount.set(p.fixedFeeAmount);
          this.isActive.set(p.isActive);
          this.loaded.set(true);
        },
        error: (err) => {
          this.loaded.set(true);
          this.error.set(err?.error?.error ?? 'Could not load this project.');
        },
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
          projectType: this.projectType(),
          canInvoice: this.canInvoice(),
          isCostExempt: this.isCostExempt(),
          currencyOverride: this.currencyOverride() || null,
          endDate: this.endDate() || null,
          budgetHours: this.budgetHours(),
          fixedFeeAmount: this.fixedFeeAmount(),
          isActive: this.isActive(),
          projectManagerUserId: this.projectManagerUserId(),
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
        projectType: this.projectType(),
        canInvoice: this.canInvoice(),
        isCostExempt: this.isCostExempt(),
        currencyOverride: this.currencyOverride() || null,
        startDate: this.startDate(),
        endDate: this.endDate() || null,
        budgetHours: this.budgetHours(),
        fixedFeeAmount: this.fixedFeeAmount(),
        projectManagerUserId: this.projectManagerUserId(),
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

  private loadAttachments(projectId: number): void {
    this.projectsAdmin.listAttachments(projectId).subscribe({
      next: (a) => {
        this.attachments.set(a);
        this.attachmentsLoaded.set(true);
      },
      error: (err) => {
        this.attachmentsLoaded.set(true);
        this.attachmentsError.set(err?.error?.error ?? 'Could not load the project documents.');
      },
    });
  }

  protected onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file || !this.editId) return;

    this.uploading.set(true);
    this.attachmentsError.set(null);
    this.projectsAdmin.uploadAttachment(Number(this.editId), file).subscribe({
      next: () => {
        this.uploading.set(false);
        input.value = '';
        this.loadAttachments(Number(this.editId));
      },
      error: (err) => {
        this.uploading.set(false);
        input.value = '';
        this.attachmentsError.set(err?.error?.error ?? 'Could not upload the document.');
      },
    });
  }

  protected downloadAttachment(attachment: ProjectAttachment): void {
    this.projectsAdmin.downloadAttachment(attachment.id).subscribe((blob) => {
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = attachment.fileName;
      link.click();
      URL.revokeObjectURL(url);
    });
  }

  protected async deleteAttachment(attachment: ProjectAttachment): Promise<void> {
    if (!this.editId) return;

    const confirmed = await this.confirmService.confirm(`Delete "${attachment.fileName}"?`, {
      confirmLabel: 'Delete',
      destructive: true,
    });
    if (!confirmed) return;

    this.projectsAdmin.deleteAttachment(attachment.id).subscribe({
      next: () => this.loadAttachments(Number(this.editId)),
      error: (err) => this.attachmentsError.set(err?.error?.error ?? 'Could not delete the document.'),
    });
  }
}
