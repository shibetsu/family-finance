class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<TxEntity>             Transactions     => Set<TxEntity>();
    public DbSet<BudgetCategoryEntity> BudgetCategories => Set<BudgetCategoryEntity>();
    public DbSet<RevenueCategoryEntity>RevenueCategories=> Set<RevenueCategoryEntity>();
    public DbSet<MerchantEntry>        MerchantCache    => Set<MerchantEntry>();
    public DbSet<ClosedMonthEntry>     ClosedMonths     => Set<ClosedMonthEntry>();
    public DbSet<BudgetDraftEntity>    BudgetDrafts     => Set<BudgetDraftEntity>();
    public DbSet<GoalEntity>           Goals            => Set<GoalEntity>();
    public DbSet<UserEntity>                Users               => Set<UserEntity>();
    public DbSet<PasswordResetTokenEntity>  PasswordResetTokens => Set<PasswordResetTokenEntity>();
    public DbSet<EmailConfigEntity>         EmailConfig         => Set<EmailConfigEntity>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<TxEntity>().ToTable("Transactions");
        mb.Entity<BudgetCategoryEntity>().ToTable("BudgetCategories");
        mb.Entity<RevenueCategoryEntity>().ToTable("RevenueCategories");
        mb.Entity<MerchantEntry>().ToTable("MerchantCache").HasKey(m => m.Description);
        mb.Entity<ClosedMonthEntry>().ToTable("ClosedMonths").HasKey(c => c.MonthKey);
        mb.Entity<BudgetDraftEntity>().ToTable("BudgetDrafts");
        mb.Entity<GoalEntity>().ToTable("Goals");
        mb.Entity<UserEntity>().ToTable("Users");
        mb.Entity<PasswordResetTokenEntity>().ToTable("PasswordResetTokens");
        mb.Entity<EmailConfigEntity>().ToTable("EmailConfig");
    }
}
