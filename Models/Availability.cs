using System;

namespace HospitalManagementPortal.Models;

public class Availability
{
    public int Id { get; set; }
    public int DoctorId { get; set; }
    public int DayOfWeek { get; set; } // 0 = Sunday, 1 = Monday, etc.
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }

    public virtual DoctorProfile Doctor { get; set; } = null!;
}
