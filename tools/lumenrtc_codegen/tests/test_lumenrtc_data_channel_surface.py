import json
import re
import unittest
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[3]
IDL_PATH = REPO_ROOT / "abi" / "generated" / "lumenrtc" / "lumenrtc.idl.json"
MANAGED_API_PATH = REPO_ROOT / "abi" / "bindings" / "lumenrtc.managed_api.json"
MANAGED_API_SOURCE_PATH = REPO_ROOT / "abi" / "bindings" / "lumenrtc.managed_api.source.json"
SRC_ROOT = REPO_ROOT / "src" / "LumenRTC"
DATA_CHANNEL_PATH = SRC_ROOT / "PeerConnection" / "DataChannel.cs"


def load_json(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


class DataChannelSurfaceTests(unittest.TestCase):
    def test_data_channel_native_functions_are_present_in_idl(self) -> None:
        idl = load_json(IDL_PATH)
        functions = {
            item.get("name")
            for item in idl.get("functions", [])
            if isinstance(item, dict) and isinstance(item.get("name"), str)
        }
        expected = {
            "lrtc_peer_connection_create_data_channel",
            "lrtc_data_channel_send",
            "lrtc_data_channel_set_callbacks",
            "lrtc_data_channel_close",
            "lrtc_data_channel_release",
        }
        missing = sorted(expected - functions)
        self.assertFalse(missing, f"DataChannel functions missing from IDL: {missing}")

    def test_data_channel_functions_are_in_required_native_list(self) -> None:
        managed_api = load_json(MANAGED_API_PATH)
        required = {
            item
            for item in managed_api.get("required_native_functions", [])
            if isinstance(item, str) and item
        }
        expected = {
            "lrtc_data_channel_send",
            "lrtc_data_channel_close",
        }
        missing = sorted(expected - required)
        self.assertFalse(
            missing,
            f"DataChannel functions missing from required_native_functions: {missing}",
        )

    def test_manual_data_channel_surface_references_core_native_calls(self) -> None:
        expected = {
            "lrtc_data_channel_set_callbacks",
            "lrtc_peer_connection_create_data_channel",
        }
        pattern = re.compile(r"NativeMethods\.(lrtc_[A-Za-z0-9_]+)\b")
        refs: set[str] = set()

        for path in SRC_ROOT.rglob("*.cs"):
            normalized = str(path).replace("\\", "/")
            if "/obj/" in normalized or "/bin/" in normalized:
                continue
            refs.update(pattern.findall(path.read_text(encoding="utf-8")))

        missing = sorted(expected - refs)
        self.assertFalse(
            missing,
            f"Manual DataChannel surface is missing core native references: {missing}",
        )

    def test_data_channel_handle_api_contains_expected_members(self) -> None:
        source = load_json(MANAGED_API_SOURCE_PATH)
        handle_api = source.get("handle_api", [])
        data_channel = None
        for item in handle_api:
            if isinstance(item, dict) and item.get("class") == "DataChannel":
                data_channel = item
                break

        self.assertIsNotNone(data_channel, "managed_api.handle_api is missing DataChannel class")
        members = data_channel.get("members", [])
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
            "lrtc_data_channel_send",
            "lrtc_data_channel_close",
        ]
        missing = [item for item in expected_snippets if item not in joined]
        self.assertFalse(missing, f"DataChannel handle API members are incomplete: missing {missing}")

    def test_data_channel_class_exposes_high_level_convenience_methods(self) -> None:
        text = DATA_CHANNEL_PATH.read_text(encoding="utf-8")
        expected_snippets = [
            "public event Action<DataChannelState>? StateChanged",
            "public event Action<DataChannelMessage>? MessageReceived",
            "public void SetTextMessageHandler(Action<string>? handler)",
            "public void SendText(string text)",
            "public void SendJson<T>(T value, JsonSerializerOptions? options = null)",
            "public bool TrySendText(string text, out string? error)",
            "public void CloseChannel()",
        ]
        missing = [item for item in expected_snippets if item not in text]
        self.assertFalse(
            missing,
            f"DataChannel convenience API is incomplete: missing {missing}",
        )


if __name__ == "__main__":
    unittest.main()
