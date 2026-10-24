using Microsoft.EntityFrameworkCore;

namespace MealManagement.Api.Data;

/// <summary>
/// Database-first mapping of the tables created by database/01_Schema.sql.
/// </summary>
public class MealDbContext : DbContext
{
    public MealDbContext(DbContextOptions<MealDbContext> options) : base(options) { }

    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Designation> Designations => Set<Designation>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<MealType> MealTypes => Set<MealType>();
    public DbSet<MealEligibility> MealEligibilities => Set<MealEligibility>();
    public DbSet<MealOccasion> MealOccasions => Set<MealOccasion>();
    public DbSet<Holiday> Holidays => Set<Holiday>();
    public DbSet<MealDailyCost> MealDailyCosts => Set<MealDailyCost>();
    public DbSet<MealEntry> MealEntries => Set<MealEntry>();
    public DbSet<MealEntryLog> MealEntryLogs => Set<MealEntryLog>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Department>(e =>
        {
            e.ToTable("Department");
            e.HasKey(x => x.DepartmentId);
            e.Property(x => x.Name).HasMaxLength(100);
        });

        b.Entity<Designation>(e =>
        {
            e.ToTable("Designation");
            e.HasKey(x => x.DesignationId);
            e.Property(x => x.Name).HasMaxLength(100);
        });

        b.Entity<Employee>(e =>
        {
            e.ToTable("Employee");
            e.HasKey(x => x.EmployeeId);
            e.Property(x => x.EmployeeCode).HasMaxLength(30);
            e.Property(x => x.EmployeeName).HasMaxLength(150);
            e.Property(x => x.MobileNo).HasMaxLength(20).IsUnicode(false);
            e.Property(x => x.Email).HasMaxLength(150);
            e.Property(x => x.Status).HasMaxLength(10).IsUnicode(false);
            e.Property(x => x.Role).HasMaxLength(10).IsUnicode(false);
            e.Property(x => x.PasswordHash).HasMaxLength(500);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            e.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId);
            e.HasOne(x => x.Designation).WithMany().HasForeignKey(x => x.DesignationId);
            e.HasMany(x => x.Eligibilities).WithOne(x => x.Employee!).HasForeignKey(x => x.EmployeeId);
        });

        b.Entity<MealType>(e =>
        {
            e.ToTable("MealType");
            e.HasKey(x => x.MealTypeId);
            e.Property(x => x.Code).HasMaxLength(20).IsUnicode(false);
            e.Property(x => x.Name).HasMaxLength(50);
            e.Property(x => x.DefaultTotalCost).HasPrecision(10, 2);
            e.Property(x => x.DefaultEmployeeCost).HasPrecision(10, 2);
        });

        b.Entity<MealEligibility>(e =>
        {
            e.ToTable("MealEligibility");
            e.HasKey(x => x.MealEligibilityId);
            e.Property(x => x.Remarks).HasMaxLength(250);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            e.HasOne(x => x.MealType).WithMany().HasForeignKey(x => x.MealTypeId);
        });

        b.Entity<MealOccasion>(e =>
        {
            e.ToTable("MealOccasion");
            e.HasKey(x => x.MealOccasionId);
            e.Property(x => x.Title).HasMaxLength(150);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            e.HasOne(x => x.MealType).WithMany().HasForeignKey(x => x.MealTypeId);
        });

        b.Entity<Holiday>(e =>
        {
            e.ToTable("Holiday");
            e.HasKey(x => x.HolidayId);
            e.Property(x => x.Description).HasMaxLength(150);
        });

        b.Entity<MealDailyCost>(e =>
        {
            e.ToTable("MealDailyCost");
            e.HasKey(x => x.MealDailyCostId);
            e.Property(x => x.TotalCost).HasPrecision(10, 2);
            e.Property(x => x.EmployeeCost).HasPrecision(10, 2);
            e.Property(x => x.CompanyCost).HasPrecision(10, 2)
                .HasComputedColumnSql("[TotalCost]-[EmployeeCost]", stored: true);
            e.Property(x => x.Remarks).HasMaxLength(250);
            e.HasOne(x => x.MealType).WithMany().HasForeignKey(x => x.MealTypeId);
        });

        b.Entity<MealEntry>(e =>
        {
            e.ToTable("MealEntry");
            e.HasKey(x => x.MealEntryId);
            e.Property(x => x.Source).HasMaxLength(10).IsUnicode(false);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            e.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId);
            e.HasOne(x => x.MealType).WithMany().HasForeignKey(x => x.MealTypeId);
            e.HasIndex(x => new { x.EmployeeId, x.MealDate, x.MealTypeId }).IsUnique();
        });

        b.Entity<MealEntryLog>(e =>
        {
            e.ToTable("MealEntryLog");
            e.HasKey(x => x.MealEntryLogId);
            e.Property(x => x.Source).HasMaxLength(10).IsUnicode(false);
            e.Property(x => x.ChangedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        });

        b.Entity<AppSetting>(e =>
        {
            e.ToTable("AppSetting");
            e.HasKey(x => x.SettingKey);
            e.Property(x => x.SettingKey).HasMaxLength(50).IsUnicode(false);
            e.Property(x => x.SettingValue).HasMaxLength(200);
            e.Property(x => x.Description).HasMaxLength(300);
        });
    }
}
