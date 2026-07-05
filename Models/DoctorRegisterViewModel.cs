using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace HospitalManagementPortal.Models;

public class DoctorRegisterViewModel
{
    [Required(ErrorMessage = "Full Name is required.")]
    [StringLength(100, ErrorMessage = "Full Name cannot exceed 100 characters.")]
    [Display(Name = "Full Name")]
    public string FullName { get; set; } = null!;

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Invalid email address.")]
    public string Email { get; set; } = null!;

    [Required(ErrorMessage = "Password is required.")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters long.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = null!;

    [DataType(DataType.Password)]
    [Display(Name = "Confirm Password")]
    [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
    public string ConfirmPassword { get; set; } = null!;

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

    [Display(Name = "Profile Image")]
    public IFormFile? ProfileImage { get; set; }
}
