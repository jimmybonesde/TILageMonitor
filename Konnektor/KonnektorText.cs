namespace TILageMonitor.Konnektor;

/// <summary>DE/EN helper for dynamic Konnektor messages.</summary>
public static class KonnektorText
{
    public static string L(string de, string en) => LocalizationService.IsGerman ? de : en;
}
