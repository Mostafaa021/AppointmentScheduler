---
name: add-migration
description: Create and review an EF Core migration for the scheduler database (PostgreSQL). Use whenever an entity, EF configuration, index or constraint changes.
---
# /add-migration <Name>

1. Make sure the model change is done in `src/AppointmentScheduler.Infrastructure/Persistence/Configurations/`.
2. Create:
   ```bash
   dotnet ef migrations add <Name> -p src/AppointmentScheduler.Infrastructure -s src/AppointmentScheduler.Api -o Persistence/Migrations
   ```
3. **Review the generated SQL** before accepting:
   ```bash
   dotnet ef migrations script <Previous> <Name> -p src/AppointmentScheduler.Infrastructure -s src/AppointmentScheduler.Api
   ```
   Check: no unintended drops/renames (EF may emit drop+add for a rename — fix with `RenameColumn`), NOT NULL columns on
   existing tables have defaults, indexes exist for new filters/FKs, raw SQL (exclusion constraints, extensions) present in
   `Up` **and** reversed in `Down`.
4. Apply locally: `dotnet ef database update -p src/AppointmentScheduler.Infrastructure -s src/AppointmentScheduler.Api`
   (or just run the API in Development — it migrates on startup).
5. Run integration tests (they build the schema from migrations).
6. Update the data-model section of `SPEC.md` and the spec's "Data changes".
