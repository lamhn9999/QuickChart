using LQ.Models.AnalystModels;
using LQ.Models.TableModels;
using Microsoft.EntityFrameworkCore;

namespace LQ.Data.Database;

public class LQDbContext : DbContext
{
    public LQDbContext(DbContextOptions<LQDbContext> options) : base(options) { }
    public DbSet<CSVFile> CSVFiles { get; set; }
    public DbSet<Field> Fields { get; set; }
    public DbSet<Value> Values { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        ConfigureSchemaAnalyst(modelBuilder);
        ConfigureSchemaApp(modelBuilder);
    }

    // Schema: app
    private static void ConfigureSchemaApp(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CSVFile>(eb =>
        {
            eb.ToTable("CSVFile", "app");
            eb.Property(f => f.AnalystID)
                .IsRequired();
            eb.HasOne(f=> f.Analyst)
                .WithMany(u => u.CSVFiles)
                .HasForeignKey(f=> f.AnalystID)
                .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<Field>().ToTable("Field", "app");
        modelBuilder.Entity<Value>(eb =>
            {
                eb.HasKey(value => new { value.FieldID, value.RowIndex });
                eb.ToTable("Value", "app"); 
                eb.Property(v => v.Existance).HasDefaultValue(true);
            });
    }
    
    // Schema: analyst
    private static void ConfigureSchemaAnalyst(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Analyst>().ToTable("Analyst", "analyst");
    }
}