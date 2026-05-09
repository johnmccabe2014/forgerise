// Numeric enum mirrors of the API (see SessionType / AttendanceStatus).
// Kept as plain numbers because that's how the API serializes them.

export const SESSION_TYPE = {
  Training: 0,
  Match: 1,
  Other: 2,
} as const;

export type SessionTypeValue =
  (typeof SESSION_TYPE)[keyof typeof SESSION_TYPE];

export function sessionTypeLabel(value: number): string {
  switch (value) {
    case SESSION_TYPE.Training:
      return "Training";
    case SESSION_TYPE.Match:
      return "Match";
    case SESSION_TYPE.Other:
      return "Other";
    default:
      return "Session";
  }
}

export const ATTENDANCE_STATUS = {
  Absent: 0,
  Present: 1,
  Late: 2,
  Excused: 3,
} as const;

export type AttendanceStatusValue =
  (typeof ATTENDANCE_STATUS)[keyof typeof ATTENDANCE_STATUS];

export function attendanceStatusLabel(value: number): string {
  switch (value) {
    case ATTENDANCE_STATUS.Absent:
      return "Absent";
    case ATTENDANCE_STATUS.Present:
      return "Present";
    case ATTENDANCE_STATUS.Late:
      return "Late";
    case ATTENDANCE_STATUS.Excused:
      return "Excused";
    default:
      return "Unknown";
  }
}

export const ATTENDANCE_STATUS_OPTIONS: ReadonlyArray<{
  value: AttendanceStatusValue;
  label: string;
}> = [
  { value: ATTENDANCE_STATUS.Present, label: "Present" },
  { value: ATTENDANCE_STATUS.Late, label: "Late" },
  { value: ATTENDANCE_STATUS.Excused, label: "Excused" },
  { value: ATTENDANCE_STATUS.Absent, label: "Absent" },
];
