using System;

namespace HospitalManagementPortal.Models;

public class Appointment
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    public int DoctorId { get; set; }
    public DateTime AppointmentDate { get; set; }
    public string Status { get; set; } = "Pending"; // 'Pending', 'Approved', 'Rejected'
    public string? SymptomDescription { get; set; }
    public string? ConsultationNotes { get; set; }
    public string? PatientPrescriptionPath { get; set; }
    public DateTime CreatedAt { get; set; }

    public virtual PatientProfile Patient { get; set; } = null!;
    public virtual DoctorProfile Doctor { get; set; } = null!;
}
