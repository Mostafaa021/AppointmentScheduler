namespace AppointmentScheduler.Domain.Doctors;

public sealed class Doctor
{
    public int Id { get; private set; }
    public string FullName { get; private set; } = "";
    public string Specialty { get; private set; } = "";

    private Doctor() { } // EF

    public Doctor(string fullName, string specialty)
    {
        FullName = fullName;
        Specialty = specialty;
    }
}
