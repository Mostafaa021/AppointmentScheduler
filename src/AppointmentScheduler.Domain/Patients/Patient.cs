using AppointmentScheduler.Domain.Common;

namespace AppointmentScheduler.Domain.Patients;

public sealed class Patient : IEntity<int>
{
    public int Id { get; private set; }
    public string Mrn { get; private set; } = "";
    public string FullName { get; private set; } = "";
    public DateOnly DateOfBirth { get; private set; }
    public string Phone { get; private set; } = "";

    private Patient() { } // EF

    public Patient(string mrn, string fullName, DateOnly dateOfBirth, string phone)
    {
        Mrn = mrn;
        FullName = fullName;
        DateOfBirth = dateOfBirth;
        Phone = phone;
    }
}
