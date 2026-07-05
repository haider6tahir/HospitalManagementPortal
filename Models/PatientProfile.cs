using System;
using System.Collections.Generic;

namespace HospitalManagementPortal.Models;

public class PatientProfile
{
    public int Id { get; set; }
    public string UserId { get; set; } = null!;
    public DateTime DateOfBirth { get; set; }
    public string Gender { get; set; } = null!;
    public string? BloodGroup { get; set; }
    public string? MedicalHistorySummary { get; set; }

    public virtual User User { get; set; } = null!;
    public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
}
