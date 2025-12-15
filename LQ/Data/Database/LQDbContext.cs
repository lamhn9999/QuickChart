using LQ.Models.AnalystModels;
using LQ.Models.TableModels;
using Microsoft.EntityFrameworkCore;

namespace LQ.Data.Database;

public class LQDbContext : DbContext
{
    public LQDbContext(DbContextOptions<LQDbContext> options) : base(options) { }
    public DbSet<Analyst> Analysts { get; set; }
    public DbSet<CSVFile> CSVFiles { get; set; }
    public DbSet<Field> Fields { get; set; }
    public DbSet<Value> Values { get; set; }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Analyst>().ToTable("Analyst");
        modelBuilder.Entity<CSVFile>().ToTable("CSVFile");
        modelBuilder.Entity<Field>().ToTable("Field");
        modelBuilder.Entity<Value>(eb =>
        {
            eb.HasKey(value => new { value.FieldID, value.RowIndex });
            eb.ToTable("Value"); 
            eb.Property(v => v.Existance).HasDefaultValue(true);
        });
    }
}