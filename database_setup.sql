-- ==========================================
-- Hospital Management Portal Database Setup
-- Run this script in Microsoft SQL Server Management Studio (SSMS)
-- ==========================================

-- Create Database if not exists
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'HospitalManagementDB')
BEGIN
    CREATE DATABASE HospitalManagementDB;
END
GO

USE HospitalManagementDB;
GO

-- Drop foreign keys if they exist (for clean re-runs)
IF OBJECT_ID('dbo.Notifications', 'U') IS NOT NULL ALTER TABLE dbo.Notifications DROP CONSTRAINT IF EXISTS FK_Notifications_Users;
IF OBJECT_ID('dbo.Appointments', 'U') IS NOT NULL ALTER TABLE dbo.Appointments DROP CONSTRAINT IF EXISTS FK_Appointments_Patients;
IF OBJECT_ID('dbo.Appointments', 'U') IS NOT NULL ALTER TABLE dbo.Appointments DROP CONSTRAINT IF EXISTS FK_Appointments_Doctors;
IF OBJECT_ID('dbo.Availabilities', 'U') IS NOT NULL ALTER TABLE dbo.Availabilities DROP CONSTRAINT IF EXISTS FK_Availabilities_Doctors;
IF OBJECT_ID('dbo.PatientProfiles', 'U') IS NOT NULL ALTER TABLE dbo.PatientProfiles DROP CONSTRAINT IF EXISTS FK_PatientProfiles_Users;
IF OBJECT_ID('dbo.DoctorProfiles', 'U') IS NOT NULL ALTER TABLE dbo.DoctorProfiles DROP CONSTRAINT IF EXISTS FK_DoctorProfiles_Users;

-- Drop tables if they exist
DROP TABLE IF EXISTS dbo.Notifications;
DROP TABLE IF EXISTS dbo.Appointments;
DROP TABLE IF EXISTS dbo.Availabilities;
DROP TABLE IF EXISTS dbo.PatientProfiles;
DROP TABLE IF EXISTS dbo.DoctorProfiles;
DROP TABLE IF EXISTS dbo.Users;
GO

-- 1. Users Table
CREATE TABLE dbo.Users (
    Id NVARCHAR(450) NOT NULL PRIMARY KEY,
    FullName NVARCHAR(256) NOT NULL,
    Email NVARCHAR(256) NOT NULL UNIQUE,
    PasswordHash NVARCHAR(MAX) NOT NULL,
    Role NVARCHAR(50) NOT NULL, -- 'Admin', 'Doctor', 'Patient'
    RegistrationDate DATETIME2 NOT NULL DEFAULT GETDATE(),
    IsActive BIT NOT NULL DEFAULT 1
);
CREATE INDEX IX_Users_Email ON dbo.Users(Email);
GO

-- 2. Doctor Profiles Table
CREATE TABLE dbo.DoctorProfiles (
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    UserId NVARCHAR(450) NOT NULL,
    Specialization NVARCHAR(100) NOT NULL,
    Qualifications NVARCHAR(500) NOT NULL,
    Biography NVARCHAR(MAX) NULL,
    Status NVARCHAR(50) NOT NULL DEFAULT 'Pending', -- 'Pending', 'Approved', 'Rejected'
    ExperienceYears INT NOT NULL DEFAULT 0,
    ProfilePicturePath NVARCHAR(500) NULL,
    CONSTRAINT FK_DoctorProfiles_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(Id) ON DELETE CASCADE
);
CREATE INDEX IX_DoctorProfiles_UserId ON dbo.DoctorProfiles(UserId);
CREATE INDEX IX_DoctorProfiles_Status ON dbo.DoctorProfiles(Status);
GO

-- 3. Patient Profiles Table
CREATE TABLE dbo.PatientProfiles (
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    UserId NVARCHAR(450) NOT NULL,
    DateOfBirth DATE NOT NULL,
    Gender NVARCHAR(20) NOT NULL,
    BloodGroup NVARCHAR(10) NULL,
    MedicalHistorySummary NVARCHAR(MAX) NULL,
    CONSTRAINT FK_PatientProfiles_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(Id) ON DELETE CASCADE
);
CREATE INDEX IX_PatientProfiles_UserId ON dbo.PatientProfiles(UserId);
GO

-- 4. Availabilities Table
CREATE TABLE dbo.Availabilities (
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    DoctorId INT NOT NULL,
    DayOfWeek INT NOT NULL, -- 0 = Sunday, 1 = Monday, 2 = Tuesday, 3 = Wednesday, 4 = Thursday, 5 = Friday, 6 = Saturday
    StartTime TIME NOT NULL,
    EndTime TIME NOT NULL,
    CONSTRAINT FK_Availabilities_Doctors FOREIGN KEY (DoctorId) REFERENCES dbo.DoctorProfiles(Id) ON DELETE CASCADE
);
CREATE INDEX IX_Availabilities_DoctorId ON dbo.Availabilities(DoctorId);
GO

-- 5. Appointments Table
CREATE TABLE dbo.Appointments (
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    PatientId INT NOT NULL,
    DoctorId INT NOT NULL,
    AppointmentDate DATETIME2 NOT NULL,
    Status NVARCHAR(50) NOT NULL DEFAULT 'Pending', -- 'Pending', 'Approved', 'Rejected'
    SymptomDescription NVARCHAR(MAX) NULL,
    ConsultationNotes NVARCHAR(MAX) NULL,
    PatientPrescriptionPath NVARCHAR(500) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_Appointments_Patients FOREIGN KEY (PatientId) REFERENCES dbo.PatientProfiles(Id),
    CONSTRAINT FK_Appointments_Doctors FOREIGN KEY (DoctorId) REFERENCES dbo.DoctorProfiles(Id) ON DELETE CASCADE
);
CREATE INDEX IX_Appointments_PatientId ON dbo.Appointments(PatientId);
CREATE INDEX IX_Appointments_DoctorId ON dbo.Appointments(DoctorId);
CREATE INDEX IX_Appointments_Date ON dbo.Appointments(AppointmentDate);
GO

-- 6. Notifications Table
CREATE TABLE dbo.Notifications (
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    UserId NVARCHAR(450) NOT NULL,
    Message NVARCHAR(MAX) NOT NULL,
    IsRead BIT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_Notifications_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(Id) ON DELETE CASCADE
);
CREATE INDEX IX_Notifications_UserId ON dbo.Notifications(UserId);
CREATE INDEX IX_Notifications_IsRead ON dbo.Notifications(IsRead);
GO

-- Print success message
PRINT 'HospitalManagementDB schema created successfully!';
