using System.Collections.Generic;
using System.Globalization;

namespace WindowResizeCapture.Studio;

// The word "Search" as Windows itself writes it, for the search box drawn on
// the studio's taskbar.
//
// This is scenery, not product text, which is why it is not in Strings.resx:
// a translator working on the app should never be asked to translate a label
// that only ever appears in a store picture. It is small enough to keep here
// rather than in store-shots, because the set cannot be drawn without it.
internal static class StudioSearchLabel
{
    private static readonly Dictionary<string, string> ByLanguage = new()
    {
        ["en"] = "Search",
        ["ja"] = "検索",
        ["de"] = "Suchen",
        ["fr"] = "Rechercher",
        ["es"] = "Buscar",
        ["pt"] = "Pesquisar",
        ["it"] = "Cerca",
        ["ru"] = "Поиск",
        ["ko"] = "검색",
        ["zh-Hans"] = "搜索",
        ["zh-Hant"] = "搜尋",
        ["ar"] = "بحث",
        ["hi"] = "खोजें",
        ["id"] = "Cari",
        ["th"] = "ค้นหา",
        ["vi"] = "Tìm kiếm",
    };

    internal static string For(CultureInfo language)
    {
        // Try the full tag first so that zh-Hans and zh-Hant stay apart, then
        // the language on its own, then English.
        if (ByLanguage.TryGetValue(language.Name, out var exact))
            return exact;

        if (ByLanguage.TryGetValue(language.TwoLetterISOLanguageName, out var neutral))
            return neutral;

        return ByLanguage["en"];
    }
}
