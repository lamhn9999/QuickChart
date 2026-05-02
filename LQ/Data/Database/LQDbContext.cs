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
    public DbSet<Canvas> Canvases { get; set; }

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
                .IsRequired(false);
            eb.HasOne(f=> f.Analyst)
                .WithMany(u => u.CSVFiles)
                .HasForeignKey(f=> f.AnalystID)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Cascade);
            eb.Property(f => f.GuestToken).IsRequired(false);
            eb.HasIndex(f => f.GuestToken);
            eb.Property(f => f.PublicToken).HasDefaultValueSql("gen_random_uuid()");
            eb.HasIndex(f => f.PublicToken).IsUnique();
            eb.Property(f => f.Status)
                .HasConversion<int>()
                .HasDefaultValue(UploadStatus.Ready)
                .HasSentinel(UploadStatus.Ready);
            eb.Property(f => f.ProcessingPayload)
                .HasColumnType("jsonb")
                .IsRequired(false);
            eb.HasIndex(f => new { f.AnalystID, f.Status });
            eb.HasIndex(f => new { f.GuestToken, f.Status });
        });
        modelBuilder.Entity<Field>().ToTable("Field", "app");
        modelBuilder.Entity<Value>(eb =>
            {
                eb.HasKey(value => new { value.FieldID, value.RowIndex });
                eb.ToTable("Value", "app");
                eb.Property(v => v.Existance).HasDefaultValue(true);
            });
        modelBuilder.Entity<Canvas>(eb =>
        {
            eb.ToTable("Canvas", "app");
            eb.HasKey(c => c.CSVFileID);
            eb.Property(c => c.CSVFileID).ValueGeneratedNever();
            eb.HasOne(c => c.CSVFile)
                .WithOne()
                .HasForeignKey<Canvas>(c => c.CSVFileID)
                .OnDelete(DeleteBehavior.Cascade);
            eb.Property(c => c.Document).HasColumnType("jsonb").IsRequired();
        });
    }
    
    // Schema: analyst
    private static void ConfigureSchemaAnalyst(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Analyst>().ToTable("Analyst", "analyst");
    }
}