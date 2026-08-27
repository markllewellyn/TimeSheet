// Mirrors TimeSheet.Contracts.EstimatedWeeklyWorkloadDto / ProjectAllocationLineDto (C#) 1:1.

export interface ProjectAllocationLine {
  projectId: number;
  projectName: string;
  clientName: string;
  allocatedHoursPerWeek: number;
}

export interface EstimatedWeeklyWorkload {
  weekStart: string;
  estimatedHoursThisWeek: number;
  byProject: ProjectAllocationLine[];
  assignmentsMissingAllocation: number[];
}
