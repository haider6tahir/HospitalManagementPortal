using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace HospitalManagementPortal.Models;

public class AdminDoctorViewModel
{
    public int? Id { get; set; } // Null for creation, set for edit

    [Required(ErrorMessage = "Full Name is required.")]
    [StringLength(100, ErrorMessage = "Full Name cannot exceed 100 characters.")]
    [Display(Name = "Full Name")]
    public string FullName { get; set; } = null!;

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Invalid email address.")]
    public string Email { get; set; } = null!;

    [DataType(DataType.Password)]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters long.")]
    public string? Password { get; set; } // Optional on Edit

    [Required(ErrorMessage = "Specialization is required.")]
    [StringLength(100, ErrorMessage = "Specialization cannot exceed 100 characters.")]
    public string Specialization { get; set; } = null!;

    [Required(ErrorMessage = "Qualifications are required.")]
    [StringLength(500, ErrorMessage = "Qualifications description cannot exceed 500 characters.")]
    public string Qualifications { get; set; } = null!;

    [Display(Name = "Biography / Profile Description")]
    public string? Biography { get; set; }

    [Required(ErrorMessage = "Years of Experience is required.")]
    [Range(0, 60, ErrorMessage = "Experience must be between 0 and 60 years.")]
    [Display(Name = "Years of Experience")]
    public int ExperienceYears { get; set; }

    [Required(ErrorMessage = "Status is required.")]
    public string Status { get; set; } = "Approved"; // Default to Approved when Admin creates

    [Display(Name = "Profile Image")]
    public IFormFile? ProfileImage { get; set; }

    public string? ExistingImagePath { get; set; }

    [Required(ErrorMessage = "Consultation Fee is required.")]
    [Range(500, 50000, ErrorMessage = "Consultation Fee must be at least Rs. 500.")]
    [Display(Name = "Consultation Fee (Rs.)")]
    public int ConsultationFee { get; set; } = 500;
}

public class AdminPatientViewModel
{
    public int? Id { get; set; } // Null for creation, set for edit

    [Required(ErrorMessage = "Full Name is required.")]
    [StringLength(100, ErrorMessage = "Full Name cannot exceed 100 characters.")]
    [Display(Name = "Full Name")]
    public string FullName { get; set; } = null!;

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Invalid email address.")]
    public string Email { get; set; } = null!;

    [DataType(DataType.Password)]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters long.")]
    public string? Password { get; set; } // Optional on Edit

    [Required(ErrorMessage = "Date of Birth is required.")]
    [DataType(DataType.Date)]
    [Display(Name = "Date of Birth")]
    public DateTime DateOfBirth { get; set; }

    [Required(ErrorMessage = "Gender is required.")]
    public string Gender { get; set; } = null!;

    [Display(Name = "Blood Group")]
    public string? BloodGroup { get; set; }

    [Display(Name = "Medical History Summary")]
    public string? MedicalHistorySummary { get; set; }
}
