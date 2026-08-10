using System.Globalization;

namespace DmC.Qa.Shared;

/// <summary>
/// Masaüstü istemcilerinde kullanıcıya gösterilen kültür, durum ve sistem metinleri için
/// merkezi Türkçe dönüştürme katmanı. API ve veritabanı teknik değerleri İngilizce kalabilir;
/// kullanıcı arayüzüne ham teknik değer taşınmamalıdır.
/// </summary>
public static class TurkishUi
{
    public static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("tr-TR");

    private static readonly IReadOnlyDictionary<string, string> StatusTranslations =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["new"] = "Yeni",
            ["acknowledged"] = "İncelendi",
            ["in_progress"] = "Üzerinde Çalışılıyor",
            ["retest_required"] = "Retest Bekliyor",
            ["resolved"] = "Çözüldü",
            ["on_hold"] = "Beklemede",
            ["duplicate"] = "Tekrar Rapor",
            ["not_a_bug"] = "Bug Değil",
            ["wont_fix"] = "Düzeltilmeyecek",
            ["reopened"] = "Yeniden Açıldı",
            ["open"] = "Açık",
            ["completed"] = "Tamamlandı",
            ["cancelled"] = "İptal Edildi",
            ["active"] = "Aktif",
            ["closed"] = "Kapalı",
            ["purge_pending"] = "Kalıcı Silme Bekliyor",
            ["purged"] = "Kalıcı Olarak Silindi",
            ["uploading"] = "Yükleniyor",
            ["candidate"] = "Aday Build",
            ["current"] = "Güncel Test Build'i",
            ["superseded"] = "Yerine Yeni Build Geldi",
            ["archived"] = "Arşivlendi",
            ["requested"] = "Talep Edildi",
            ["second_admin_approved"] = "İkinci Admin Onayladı",
            ["running"] = "İşleniyor",
            ["failed"] = "Başarısız",
            ["passed"] = "Başarılı",
            ["uncertain"] = "Emin Değilim"
        };

    private static readonly IReadOnlyDictionary<string, string> RoleTranslations =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["tester"] = "Tester",
            ["developer"] = "Developer",
            ["admin"] = "Admin",
            ["super_admin"] = "Süper Admin"
        };

    private static readonly IReadOnlyDictionary<string, string> SeverityTranslations =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["info"] = "Bilgi",
            ["warning"] = "Uyarı",
            ["critical"] = "Kritik",
            ["must_read"] = "Okunması Zorunlu"
        };

    private static readonly IReadOnlyDictionary<string, string> ReleaseChannelTranslations =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["stable"] = "Kararlı",
            ["beta"] = "Beta"
        };

    public static void ApplyCulture()
    {
        CultureInfo.DefaultThreadCurrentCulture = Culture;
        CultureInfo.DefaultThreadCurrentUICulture = Culture;
        CultureInfo.CurrentCulture = Culture;
        CultureInfo.CurrentUICulture = Culture;
    }

    public static string Status(string? value) => Translate(StatusTranslations, value);

    public static string Role(string? value) => Translate(RoleTranslations, value);

    public static string Severity(string? value) => Translate(SeverityTranslations, value);

    public static string ReleaseChannel(string? value) => Translate(ReleaseChannelTranslations, value);

    public static string Date(DateTimeOffset? value, bool includeTime = true)
    {
        if (value is null)
        {
            return "—";
        }

        return value.Value.ToLocalTime().ToString(
            includeTime ? "d MMMM yyyy HH:mm" : "d MMMM yyyy",
            Culture);
    }

    public static string Date(DateTime? value, bool includeTime = true)
    {
        if (value is null)
        {
            return "—";
        }

        return value.Value.ToLocalTime().ToString(
            includeTime ? "d MMMM yyyy HH:mm" : "d MMMM yyyy",
            Culture);
    }

    public static string FileSize(long? bytes)
    {
        if (bytes is null || bytes < 0)
        {
            return "—";
        }

        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double size = bytes.Value;
        var unit = 0;

        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        return $"{size:0.##} {units[unit]}";
    }

    public static string Deadline(DateTimeOffset? deadline, DateTimeOffset? now = null)
    {
        if (deadline is null)
        {
            return "Son tarih belirtilmedi";
        }

        var reference = now ?? DateTimeOffset.Now;
        var remaining = deadline.Value.ToLocalTime() - reference.ToLocalTime();

        if (remaining <= TimeSpan.Zero)
        {
            return "Süre doldu";
        }

        if (remaining.TotalDays >= 2)
        {
            return $"{Math.Floor(remaining.TotalDays):0} gün kaldı";
        }

        if (remaining.TotalHours >= 2)
        {
            return $"{Math.Floor(remaining.TotalHours):0} saat kaldı";
        }

        return $"{Math.Max(1, Math.Ceiling(remaining.TotalMinutes)):0} dakika kaldı";
    }

    public static string Reproduction(int? hits, int? attempts)
    {
        if (hits is null || attempts is null || attempts <= 0)
        {
            return "Belirtilmedi";
        }

        return $"{attempts} denemenin {hits} tanesinde oluştu ({hits}/{attempts})";
    }

    public static string Boolean(bool value) => value ? "Evet" : "Hayır";

    private static string Translate(IReadOnlyDictionary<string, string> source, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "—";
        }

        return source.TryGetValue(value.Trim(), out var translated)
            ? translated
            : HumanizeTechnicalValue(value);
    }

    private static string HumanizeTechnicalValue(string value)
    {
        var normalized = value.Trim().Replace('_', ' ').Replace('-', ' ');
        if (normalized.Length == 0)
        {
            return "—";
        }

        return Culture.TextInfo.ToTitleCase(normalized.ToLower(Culture));
    }
}
