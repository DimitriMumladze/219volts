namespace TwoNineteenVolts.Venue.Application.Audit;

public static class AuditChanges
{
    public static string Fields(params (string Field, string Old, string New)[] fields)
    {
        var changed = fields
            .Where(f => f.Old != f.New)
            .Select(f => $"{f.Field}: {f.Old} → {f.New}");

        return string.Join("; ", changed);
    }

    public static string Change(string old, string changed)
    {
        return $"{old} → {changed}";
    }
}
