using System;
using System.Collections.Generic;

namespace HospitalManagementPortal.Models;

public class User
{
    public string Id { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public string Role { get; set; } = null!;
    public DateTime RegistrationDate { get; set; }
    public bool IsActive { get; set; }

    public virtual ICollection<DoctorProfile> DoctorProfiles { get; set; } = new List<DoctorProfile>();
    public virtual ICollection<PatientProfile> PatientProfiles { get; set; } = new List<PatientProfile>();
    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}
