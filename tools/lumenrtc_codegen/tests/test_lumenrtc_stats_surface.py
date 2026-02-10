import unittest
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[3]
STATS_REPORT_PATH = REPO_ROOT / "src" / "LumenRTC" / "Stats" / "RtcStatsReport.cs"
STAT_PATH = REPO_ROOT / "src" / "LumenRTC" / "Stats" / "RtcStat.cs"
STAT_QUERY_PATH = REPO_ROOT / "src" / "LumenRTC" / "Stats" / "RtcStatQuery.cs"
STAT_TYPES_PATH = REPO_ROOT / "src" / "LumenRTC" / "Stats" / "RtcStatTypes.cs"


class StatsSurfaceTests(unittest.TestCase):
    def test_stats_report_has_typed_query_and_indexes(self) -> None:
        text = STATS_REPORT_PATH.read_text(encoding="utf-8")
        expected = [
            "private readonly Dictionary<string, RtcStat> _statsById;",
            "private readonly Dictionary<string, List<RtcStat>> _statsByType;",
            "public bool TryGetById(string id, out RtcStat? stat)",
            "public IEnumerable<RtcStat> Query(RtcStatQuery query)",
            "public RtcStat? GetFirst(in RtcStatQuery query)",
            "public IReadOnlyList<RtcStat> ToList(in RtcStatQuery query)",
            "public bool TryGetSelectedCandidatePair(out RtcStat? selected)",
            "ParseStatsContainer(document.RootElement, stats);",
        ]
        missing = [item for item in expected if item not in text]
        self.assertFalse(missing, f"RtcStatsReport surface missing: {missing}")

    def test_stats_report_parser_supports_multiple_root_shapes(self) -> None:
        text = STATS_REPORT_PATH.read_text(encoding="utf-8")
        expected = [
            "case JsonValueKind.Array:",
            "case JsonValueKind.Object:",
            "if (root.TryGetProperty(\"stats\", out var nestedStats))",
            "if (LooksLikeStat(property.Value))",
        ]
        missing = [item for item in expected if item not in text]
        self.assertFalse(missing, f"RtcStatsReport parser coverage missing: {missing}")

    def test_rtc_stat_has_rich_property_accessors(self) -> None:
        text = STAT_PATH.read_text(encoding="utf-8")
        expected = [
            "public bool TryGetProperty(string name, out JsonElement value)",
            "public bool TryGetStringArray(string name, out IReadOnlyList<string> values)",
            "public string? GetStringOrDefault(string name, string? fallback = null)",
            "public double? GetDoubleOrNull(string name)",
            "public uint? GetUInt32OrNull(string name)",
            "public bool IsType(string type)",
        ]
        missing = [item for item in expected if item not in text]
        self.assertFalse(missing, f"RtcStat accessor surface missing: {missing}")

    def test_stat_query_and_type_catalog_exist(self) -> None:
        query_text = STAT_QUERY_PATH.read_text(encoding="utf-8")
        types_text = STAT_TYPES_PATH.read_text(encoding="utf-8")
        self.assertIn("public readonly record struct RtcStatQuery", query_text)
        expected_types = [
            "InboundRtp",
            "OutboundRtp",
            "CandidatePair",
            "Transport",
            "Codec",
        ]
        missing = [item for item in expected_types if item not in types_text]
        self.assertFalse(missing, f"RtcStatTypes catalog missing entries: {missing}")


if __name__ == "__main__":
    unittest.main()
