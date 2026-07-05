using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HospitalManagementPortal.Data;
using HospitalManagementPortal.Models;
using HospitalManagementPortal.Services;

namespace HospitalManagementPortal.Controllers;

public class AccountController : Controller
{
    private readonly HospitalDbContext _context;
    private readonly IPasswordService _passwordService;
    private readonly INotificationService _notificationService;
    private readonly IWebHostEnvironment _webHostEnvironment;

    public AccountController(
        HospitalDbContext context, 
        IPasswordService passwordService, 
        INotificationService notificationService,
        IWebHostEnvironment webHostEnvironment)
    {
        _context = context;
        _passwordService = passwordService;
        _notificationService = notificationService;
        _webHostEnvironment = webHostEnvironment;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToDashboard(User.FindFirst(ClaimTypes.Role)?.Value);
        }
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == model.Email);
            if (user == null || !_passwordService.VerifyPassword(model.Password, user.PasswordHash))
            {
                ModelState.AddModelError(string.Empty, "Invalid login attempt. Please check your credentials.");
                return View(model);
            }

            if (!user.IsActive)
            {
                ModelState.AddModelError(string.Empty, "Your account has been deactivated. Please contact support.");
                return View(model);
            }

            // Additional check for Doctor: must be Approved
            if (user.Role == "Doctor")
            {
                var docProfile = await _context.DoctorProfiles.FirstOrDefaultAsync(d => d.UserId == user.Id);
                if (docProfile == null || docProfile.Status == "Pending")
                {
                    ModelState.AddModelError(string.Empty, "Your doctor profile is pending administrator approval. You will receive an email once approved.");
                    return View(model);
                }
                if (docProfile.Status == "Rejected")
                {
                    ModelState.AddModelError(string.Empty, "Your doctor application was rejected by the administrator.");
                    return View(model);
                }
            }

            // Create user claims
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role)
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = model.RememberMe,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(2)
            };

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity), authProperties);

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToDashboard(user.Role);
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, $"An error occurred during sign-in. Please ensure the database is running. Details: {ex.Message}");
            return View(model);
        }
    }

    [HttpGet]
    public IActionResult RegisterPatient()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegisterPatient(PatientRegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var emailExists = await _context.Users.AnyAsync(u => u.Email == model.Email);
            if (emailExists)
            {
                ModelState.AddModelError("Email", "An account with this email address already exists.");
                return View(model);
            }

            var user = new User
            {
                Id = Guid.NewGuid().ToString(),
                FullName = model.FullName,
                Email = model.Email,
                PasswordHash = _passwordService.HashPassword(model.Password),
                Role = "Patient",
                RegistrationDate = DateTime.Now,
                IsActive = true
            };

            var patientProfile = new PatientProfile
            {
                UserId = user.Id,
                DateOfBirth = model.DateOfBirth,
                Gender = model.Gender,
                BloodGroup = model.BloodGroup,
                MedicalHistorySummary = model.MedicalHistorySummary
            };

            _context.Users.Add(user);
            _context.PatientProfiles.Add(patientProfile);
            await _context.SaveChangesAsync();

            // Send notification
            await _notificationService.SendNotificationAsync(user.Id, $"Welcome to our Hospital Management Portal, {user.FullName}! Your registration is complete.");

            TempData["SuccessMessage"] = "Registration successful! You can now log in.";
            return RedirectToAction(nameof(Login));
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, $"Error during registration: {ex.Message}");
            return View(model);
        }
    }

    [HttpGet]
    public IActionResult RegisterDoctor()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegisterDoctor(DoctorRegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var emailExists = await _context.Users.AnyAsync(u => u.Email == model.Email);
            if (emailExists)
            {
                ModelState.AddModelError("Email", "An account with this email address already exists.");
                return View(model);
            }

            string? imagePath = null;
            if (model.ProfileImage != null && model.ProfileImage.Length > 0)
            {
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                var extension = Path.GetExtension(model.ProfileImage.FileName).ToLower();
                if (!allowedExtensions.Contains(extension))
                {
                    ModelState.AddModelError("ProfileImage", "Only image files (.jpg, .jpeg, .png, .gif) are allowed.");
                    return View(model);
                }

                var uploadDir = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "doctors");
                if (!Directory.Exists(uploadDir))
                {
                    Directory.CreateDirectory(uploadDir);
                }

                var uniqueFileName = Guid.NewGuid().ToString() + extension;
                var filePath = Path.Combine(uploadDir, uniqueFileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await model.ProfileImage.CopyToAsync(stream);
                }

                imagePath = "/uploads/doctors/" + uniqueFileName;
            }

            var user = new User
            {
                Id = Guid.NewGuid().ToString(),
                FullName = model.FullName,
                Email = model.Email,
                PasswordHash = _passwordService.HashPassword(model.Password),
                Role = "Doctor",
                RegistrationDate = DateTime.Now,
                IsActive = true
            };

            var doctorProfile = new DoctorProfile
            {
                UserId = user.Id,
                Specialization = model.Specialization,
                Qualifications = model.Qualifications,
                Biography = model.Biography,
                ExperienceYears = model.ExperienceYears,
                Status = "Pending", // Explicitly pending admin approval
                ProfilePicturePath = imagePath
            };

            _context.Users.Add(user);
            _context.DoctorProfiles.Add(doctorProfile);
            await _context.SaveChangesAsync();

            // Send system alert to doctors context
            await _notificationService.SendNotificationAsync(user.Id, "Thank you for registering! Your doctor application has been submitted and is currently pending approval.");

            TempData["PendingApprovalMessage"] = "Doctor application submitted! Your registration is pending administrator approval. You will be authorized to log in once approved.";
            return RedirectToAction(nameof(Login));
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, $"Error during registration: {ex.Message}");
            return View(model);
        }
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }

    private IActionResult RedirectToDashboard(string? role)
    {
        return role switch
        {
            "Admin" => RedirectToAction("Dashboard", "Admin"),
            "Doctor" => RedirectToAction("Dashboard", "Doctor"),
            "Patient" => RedirectToAction("Dashboard", "Patient"),
            _ => RedirectToAction("Index", "Home")
        };
    }
}
