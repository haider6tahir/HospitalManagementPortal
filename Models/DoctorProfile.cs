using System;
using System.Collections.Generic;

namespace HospitalManagementPortal.Models;

public class DoctorProfile
{
    public int Id { get; set; }
    public string UserId { get; set; } = null!;
    public string Specialization { get; set; } = null!;
    public string Qualifications { get; set; } = null!;
    public string? Biography { get; set; }
    public string? ProfilePicturePath { get; set; }
    public string Status { get; set; } = "Pending"; // 'Pending', 'Approved', 'Rejected'
    public int ExperienceYears { get; set; }
    public int ConsultationFee { get; set; } = 500;

    public virtual User User { get; set; } = null!;
    public virtual ICollection<Availability> Availabilities { get; set; } = new List<Availability>();
    public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
}
