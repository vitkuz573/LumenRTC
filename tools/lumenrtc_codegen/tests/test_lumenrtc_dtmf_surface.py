import json
import re
import unittest
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[3]
IDL_PATH = REPO_ROOT / "abi" / "generated" / "lumenrtc" / "lumenrtc.idl.json"
MANAGED_API_PATH = REPO_ROOT / "abi" / "bindings" / "lumenrtc.managed_api.json"
MANAGED_API_SOURCE_PATH = REPO_ROOT / "abi" / "bindings" / "lumenrtc.managed_api.source.json"
SRC_ROOT = REPO_ROOT / "src" / "LumenRTC"
RTP_SENDER_PATH = SRC_ROOT / "Rtp" / "RtpSender.cs"


def load_json(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


class DtmfSurfaceTests(unittest.TestCase):
    def test_dtmf_native_functions_are_present_in_idl(self) -> None:
        idl = load_json(IDL_PATH)
        functions = {
            item.get("name")
            for item in idl.get("functions", [])
            if isinstance(item, dict) and isinstance(item.get("name"), str)
        }
        expected = {
            "lrtc_rtp_sender_get_dtmf_sender",
            "lrtc_dtmf_sender_can_insert",
            "lrtc_dtmf_sender_insert",
            "lrtc_dtmf_sender_tones",
            "lrtc_dtmf_sender_duration",
            "lrtc_dtmf_sender_inter_tone_gap",
            "lrtc_dtmf_sender_comma_delay",
            "lrtc_dtmf_sender_set_callbacks",
            "lrtc_dtmf_sender_release",
        }
        missing = sorted(expected - functions)
        self.assertFalse(missing, f"DTMF functions missing from IDL: {missing}")

    def test_dtmf_functions_are_in_required_native_list(self) -> None:
        managed_api = load_json(MANAGED_API_PATH)
        required = {
            item
            for item in managed_api.get("required_native_functions", [])
            if isinstance(item, str) and item
        }
        expected = {
            "lrtc_dtmf_sender_can_insert",
            "lrtc_dtmf_sender_insert",
            "lrtc_dtmf_sender_tones",
            "lrtc_dtmf_sender_duration",
            "lrtc_dtmf_sender_inter_tone_gap",
            "lrtc_dtmf_sender_comma_delay",
        }
        missing = sorted(expected - required)
        self.assertFalse(missing, f"DTMF functions missing from required_native_functions: {missing}")

    def test_manual_dtmf_surface_references_core_native_calls(self) -> None:
        expected = {
            "lrtc_rtp_sender_get_dtmf_sender",
            "lrtc_dtmf_sender_set_callbacks",
        }
        pattern = re.compile(r"NativeMethods\.(lrtc_[A-Za-z0-9_]+)\b")
        refs: set[str] = set()

        for path in SRC_ROOT.rglob("*.cs"):
            normalized = str(path).replace("\\", "/")
            if "/obj/" in normalized or "/bin/" in normalized:
                continue
            text = path.read_text(encoding="utf-8")
            refs.update(pattern.findall(text))

        missing = sorted(expected - refs)
        self.assertFalse(missing, f"Manual DTMF surface is missing core native references: {missing}")

    def test_dtmf_handle_api_contains_expected_members(self) -> None:
        source = load_json(MANAGED_API_SOURCE_PATH)
        handle_api = source.get("handle_api", [])
        dtmf = None
        for item in handle_api:
            if isinstance(item, dict) and item.get("class") == "DtmfSender":
                dtmf = item
                break

        self.assertIsNotNone(dtmf, "managed_api.handle_api is missing DtmfSender class")
        members = dtmf.get("members", [])
        self.assertIsInstance(members, list)

        rendered = []
        for member in members:
            if not isinstance(member, dict):
                continue
            line = member.get("line")
            if isinstance(line, str):
                rendered.append(line)
            body = member.get("body")
            if isinstance(body, list):
                rendered.extend(item for item in body if isinstance(item, str))

        joined = "\n".join(rendered)
        expected_snippets = [
            "lrtc_dtmf_sender_can_insert",
            "lrtc_dtmf_sender_insert",
            "lrtc_dtmf_sender_tones",
            "lrtc_dtmf_sender_duration",
            "lrtc_dtmf_sender_inter_tone_gap",
            "lrtc_dtmf_sender_comma_delay",
        ]
        missing = [item for item in expected_snippets if item not in joined]
        self.assertFalse(missing, f"DTMF handle API members are incomplete: missing {missing}")

    def test_rtp_sender_exposes_dtmf_convenience_api(self) -> None:
        text = RTP_SENDER_PATH.read_text(encoding="utf-8")
        expected_snippets = [
            "public bool CanInsertDtmf =>",
            "public bool TryInsertDtmf(string tones, out string? error)",
            "public bool TryInsertDtmf(string tones, DtmfInsertOptions options, out string? error)",
            "public void InsertDtmf(string tones, DtmfInsertOptions options)",
            "public void SetDtmfToneChangeHandler(Action<DtmfToneChange>? handler)",
        ]
        missing = [item for item in expected_snippets if item not in text]
        self.assertFalse(missing, f"RtpSender DTMF convenience API is incomplete: missing {missing}")


if __name__ == "__main__":
    unittest.main()
