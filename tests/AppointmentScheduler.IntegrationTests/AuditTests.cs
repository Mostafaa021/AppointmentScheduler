using System.Net;
using System.Net.Http.Json;
using AppointmentScheduler.Domain.Common;
using AppointmentScheduler.Domain.Patients;
using AppointmentScheduler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static AppointmentScheduler.IntegrationTests.ApiFactory;

namespace AppointmentScheduler.IntegrationTests;

/// <summary>AC-004-24 / AC-004-25 (ADR-0011). Tests in this collection run sequentially, so counting rows is stable.</summary>
[Collection(nameof(ApiCollection))]
public class AuditTests(ApiFactory api)
{
    private async Task<List<AuditRecord>> AuditAsync(Func<IQueryable<AuditRecord>, IQueryable<AuditRecord>> filter)
    {
        using var scope = api.Services.CreateScope();
        return await filter(scope.ServiceProvider.GetRequiredService<AppDbContext>().AuditRecords.AsNoTracking()).ToListAsync();
    }

    private async Task<Guid> UserIdAsync(string email)
    {
        using var scope = api.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users
            .Where(u => u.NormalizedEmail == email.ToUpperInvariant()).Select(u => u.Id).SingleAsync();
    }

    private void AssertRecord(AuditRecord r, Guid? userId, string action, string entityType, string? entityId)
    {
        Assert.Equal(userId, r.UserId);
        Assert.Equal(action, r.Action);
        Assert.Equal(entityType, r.EntityType);
        Assert.Equal(entityId, r.EntityId);
        Assert.Equal(api.Clock.GetUtcNow(), r.TimestampUtc);
        Assert.False(string.IsNullOrEmpty(r.TraceId));
    }

    [Fact]
    public async Task AC_004_24_Auth_events_are_audited_once_each()
    {
        var email = await api.CreateUserAsync();
        var userId = await UserIdAsync(email);
        var anon = api.CreateAnonymousClient();

        await anon.LoginAsync(email, "Wrong-Password-999");
        var unknownBefore = (await AuditAsync(q => q.Where(a => a.Action == "auth.login_failed" && a.UserId == null))).Count;
        await anon.LoginAsync($"ghost-{Guid.NewGuid():N}@test.local", "Whatever-Pass-1");
        var login = await anon.LoginAsync(email, TestUsers.Password);
        var first = login.RefreshToken();
        var second = (await anon.RefreshAsync(first)).RefreshToken();
        await anon.RefreshAsync(first); // reuse
        var client = await api.ClientForAsync(email);
        await client.PostWithRefreshCookieAsync("/api/v1/auth/logout", second);

        var mine = await AuditAsync(q => q.Where(a => a.UserId == userId));
        Assert.Equal(["auth.login_failed", "auth.login_succeeded", "auth.login_succeeded", "auth.logout", "auth.refresh_token_reused"],
            mine.Select(a => a.Action).Order());
        AssertRecord(mine.Single(a => a.Action == "auth.login_failed"), userId, "auth.login_failed", "User", userId.ToString());
        AssertRecord(mine.First(a => a.Action == "auth.login_succeeded"), userId, "auth.login_succeeded", "User", userId.ToString());
        var reused = mine.Single(a => a.Action == "auth.refresh_token_reused");
        Assert.Equal("RefreshTokenFamily", reused.EntityType);
        Assert.True(Guid.TryParse(reused.EntityId, out _));
        AssertRecord(mine.Single(a => a.Action == "auth.logout"), userId, "auth.logout", "User", userId.ToString());

        var unknown = await AuditAsync(q => q.Where(a => a.Action == "auth.login_failed" && a.UserId == null));
        Assert.Equal(unknownBefore + 1, unknown.Count);
        Assert.All(unknown, r => Assert.Null(r.EntityId));
    }

    [Fact]
    public async Task AC_004_24_Appointment_events_are_audited_and_rejections_are_not()
    {
        var (d, p) = await api.CreatePeopleAsync();
        var client = await api.ClientForAsync(TestUsers.Receptionist);
        var receptionistId = await UserIdAsync(TestUsers.Receptionist);
        var bookedBefore = (await AuditAsync(q => q.Where(a => a.Action.StartsWith("appointment.")))).Count;

        var id = (await (await client.BookAsync(d[0], p[0], At(10))).DataAsync()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.Conflict, (await client.BookAsync(d[0], p[1], At(10))).StatusCode);       // 409: no record
        Assert.Equal(HttpStatusCode.BadRequest, (await client.BookAsync(d[0], p[1], At(11), " ")).StatusCode); // 400: no record
        await client.PostAsJsonAsync($"/api/v1/appointments/{id}/reschedule", new { startUtc = At(12) });
        await client.PostAsJsonAsync($"/api/v1/appointments/{id}/cancel", new { reason = "x" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await client.PostAsJsonAsync($"/api/v1/appointments/{id}/cancel", new { reason = "x" })).StatusCode); // 422: no record

        var records = await AuditAsync(q => q.Where(a => a.EntityId == id.ToString()));
        Assert.Equal(["appointment.booked", "appointment.cancelled", "appointment.rescheduled"], records.Select(r => r.Action).Order());
        Assert.All(records, r => AssertRecord(r, receptionistId, r.Action, "Appointment", id.ToString()));
        Assert.Equal(bookedBefore + 3, (await AuditAsync(q => q.Where(a => a.Action.StartsWith("appointment.")))).Count);
    }

    [Fact]
    public async Task AC_004_24_User_admin_events_are_audited()
    {
        var admin = await api.ClientForAsync(TestUsers.Admin);
        var adminId = await UserIdAsync(TestUsers.Admin);
        var email = $"audited-{Guid.NewGuid():N}@test.local";

        var id = (await (await admin.PostAsJsonAsync("/api/v1/users", new { email, password = "Audited-Pass-42", roles = RoleSets.Receptionist }))
            .DataAsync()).GetProperty("id").GetGuid();
        await admin.PostAsJsonAsync("/api/v1/users", new { email, password = "Audited-Pass-42", roles = RoleSets.Receptionist }); // 409
        await admin.PutAsJsonAsync($"/api/v1/users/{id}/roles", new { roles = RoleSets.Admin });

        var records = await AuditAsync(q => q.Where(a => a.EntityId == id.ToString()));
        Assert.Equal(2, records.Count);
        AssertRecord(records.Single(r => r.Action == "user.created"), adminId, "user.created", "User", id.ToString());
        AssertRecord(records.Single(r => r.Action == "user.roles_changed"), adminId, "user.roles_changed", "User", id.ToString());
    }

    [Fact]
    public async Task AC_004_25_Audit_records_and_logs_contain_no_secrets_or_PII()
    {
        const string patientName = "Quillon Privacy-Testperson";
        const string phone = "+44 20 7946 9871";
        const string password = "Pii-Check-Pass-77";
        var email = $"pii-{Guid.NewGuid():N}@test.local";
        int patientId, doctorId = (await api.CreatePeopleAsync(doctors: 1, patients: 0)).Doctors[0];
        using (var scope = api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var patient = new Patient($"P-{Guid.NewGuid():N}"[..20], patientName, new DateOnly(1980, 1, 1), phone);
            db.Add(patient);
            await db.SaveChangesAsync();
            patientId = patient.Id;
        }
        var admin = await api.ClientForAsync(TestUsers.Admin);
        var anon = api.CreateAnonymousClient();

        var id = (await (await admin.PostAsJsonAsync("/api/v1/users", new { email, password, roles = RoleSets.Receptionist }))
            .DataAsync()).GetProperty("id").GetGuid();
        await admin.PutAsJsonAsync($"/api/v1/users/{id}/roles", new { roles = RoleSets.AdminReceptionist });
        await anon.LoginAsync(email, "Wrong-" + password);
        var login = await anon.LoginAsync(email, password);
        var accessToken = (await login.DataAsync()).GetProperty("accessToken").GetString()!;
        var refresh1 = login.RefreshToken();
        var refreshed = await anon.RefreshAsync(refresh1);
        var refresh2 = refreshed.RefreshToken();
        await anon.RefreshAsync(refresh1); // reuse
        var user = await api.ClientForAsync(email, password);
        var appointment = (await (await user.BookAsync(doctorId, patientId, At(15))).DataAsync()).GetProperty("id").GetGuid();
        await user.PostAsJsonAsync($"/api/v1/appointments/{appointment}/cancel", new { reason = "x" });
        await user.GetAsync($"/api/v1/patients?search={Uri.EscapeDataString("Quillon")}");
        await user.PostWithRefreshCookieAsync("/api/v1/auth/logout", refresh2);

        string[] secrets = [email, email.ToUpperInvariant(), password, accessToken, refresh1, refresh2, patientName, phone];
        var audit = (await AuditAsync(q => q)).Select(a => $"{a.UserId}|{a.Action}|{a.EntityType}|{a.EntityId}|{a.TraceId}").ToList();
        var logs = api.Logs.Entries.ToList();
        Assert.NotEmpty(logs);
        foreach (var secret in secrets)
        {
            Assert.DoesNotContain(audit, line => line.Contains(secret, StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(logs, line => line.Contains(secret, StringComparison.OrdinalIgnoreCase));
        }
    }
}
