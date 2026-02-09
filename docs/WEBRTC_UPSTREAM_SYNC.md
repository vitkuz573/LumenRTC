# WebRTC Upstream Sync (Google Source Of Truth)

This document records the canonical WebRTC upstream and the repeatable sync
procedure for LumenRTC.

## Canonical Upstream

- WebRTC upstream remote: `https://webrtc.googlesource.com/src.git`
- Default ref: `branch-heads/7151`
- Working checkout used by setup scripts: `../webrtc_build/src`

No other upstream (`webrtc-sdk`, GitHub mirrors, legacy submodules) is allowed
as a source of truth.

## Last Verified Sync

- Verification date: `2026-02-08`
- Remote: `https://webrtc.googlesource.com/src.git`
- Branch: `branch-heads/7151`
- Commit: `cec4daea7ed5da94fc38d790bd12694c86865447`

## Sync Procedure

1. Configure `.gclient` in `../webrtc_build`:

```python
solutions = [
  {
    "name": "src",
    "url": "https://webrtc.googlesource.com/src.git@branch-heads/7151",
    "deps_file": "DEPS",
    "managed": False,
    "custom_deps": {},
    "custom_vars": {},
  },
]
target_os = ["linux"]
```

2. Ensure the checkout remote/branch:

```bash
git -C ../webrtc_build/src remote set-url origin https://webrtc.googlesource.com/src.git
git -C ../webrtc_build/src fetch origin
git -C ../webrtc_build/src fetch origin refs/branch-heads/7151:refs/remotes/origin/branch-heads/7151
git -C ../webrtc_build/src checkout -B branch-heads/7151 origin/branch-heads/7151
```

3. Sync dependencies:

```bash
PATH=../depot_tools:$PATH gclient sync --nohooks --ignore-dep-type=gcs
```

`--ignore-dep-type=gcs` is used when GCS artifact fetches are blocked in the
environment. Git/CIPD dependencies still sync.

## Verification Checklist

1. Upstream identity:

```bash
git -C ../webrtc_build/src remote -v
git -C ../webrtc_build/src branch --show-current
git -C ../webrtc_build/src rev-parse HEAD
git -C ../webrtc_build/src status --short
```

2. `.gclient` source:

```bash
sed -n '1,120p' ../webrtc_build/.gclient
```

3. ABI framework regeneration after upstream sync:

```bash
scripts/abi.sh release-prepare --skip-binary --update-baselines --release-tag <tag>
```

## Notes

- If local changes exist inside `../webrtc_build/src`, stash or commit them
  before branch moves.
- If strict binary ABI verification is required, ensure `liblumenrtc` is built
  with controlled symbol exports before running ABI checks without
  `--skip-binary`.
