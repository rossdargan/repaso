using Microsoft.EntityFrameworkCore;
using SpanishPractice.Api.Models;

namespace SpanishPractice.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<VocabularyImport> VocabularyImports => Set<VocabularyImport>();
    public DbSet<WordEntry> WordEntries => Set<WordEntry>();
    public DbSet<WordProgress> WordProgressEntries => Set<WordProgress>();
    public DbSet<Attempt> Attempts => Set<Attempt>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<WordVariant> WordVariants => Set<WordVariant>();
    public DbSet<WordExample> WordExamples => Set<WordExample>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<VocabularyImport>()
            .Property(x => x.Status)
            .HasConversion<int>();

        modelBuilder.Entity<Category>()
            .HasIndex(x => x.Name)
            .IsUnique();

        modelBuilder.Entity<WordEntry>()
            .HasOne(x => x.VocabularyImport)
            .WithMany(x => x.Words)
            .HasForeignKey(x => x.VocabularyImportId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<WordEntry>()
            .HasOne(x => x.Category)
            .WithMany(x => x.WordEntries)
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<WordEntry>()
            .Property(x => x.Gender)
            .HasConversion<int>();

        modelBuilder.Entity<WordEntry>()
            .Property(x => x.Number)
            .HasConversion<int>();

        modelBuilder.Entity<WordEntry>()
            .Property(x => x.State)
            .HasConversion<int>();

        modelBuilder.Entity<WordProgress>()
            .HasOne(x => x.WordEntry)
            .WithOne(x => x.Progress)
            .HasForeignKey<WordProgress>(x => x.WordEntryId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<WordProgress>()
            .Property(x => x.PriorityScore)
            .HasDefaultValue(1.0);

        modelBuilder.Entity<Attempt>()
            .Property(x => x.PromptLanguage)
            .HasConversion<int>();

        modelBuilder.Entity<Attempt>()
            .Property(x => x.ResultType)
            .HasConversion<int>();

        modelBuilder.Entity<Attempt>()
            .HasOne(x => x.WordEntry)
            .WithMany(x => x.Attempts)
            .HasForeignKey(x => x.WordEntryId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<WordVariant>()
            .Property(x => x.Language)
            .HasConversion<int>();

        modelBuilder.Entity<WordVariant>()
            .HasOne(x => x.WordEntry)
            .WithMany(x => x.Variants)
            .HasForeignKey(x => x.WordEntryId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<WordVariant>()
            .HasIndex(x => new { x.WordEntryId, x.Language, x.SortOrder });

        modelBuilder.Entity<WordVariant>()
            .HasIndex(x => x.NormalizedText);

        modelBuilder.Entity<WordExample>()
            .HasOne(x => x.WordEntry)
            .WithMany(x => x.Examples)
            .HasForeignKey(x => x.WordEntryId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<WordExample>()
            .HasIndex(x => new { x.WordEntryId, x.SortOrder });
    }
}
