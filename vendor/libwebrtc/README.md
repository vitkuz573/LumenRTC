# libwebrtc-{windows,linux}-{amd64,x86,armv7,arm64}.{dll,so}

WebRTC C++ wrapper for LumenRTC.

## Upstream

Use official WebRTC source: `https://webrtc.googlesource.com/src`.
Default ref in LumenRTC setup scripts: `branch-heads/7151`.

## Usage

Create a checkout directory:

```bash
mkdir libwebrtc_build
cd libwebrtc_build
```

Create `.gclient`:

```bash
solutions = [
  {
    "name"        : 'src',
    "url"         : 'https://webrtc.googlesource.com/src.git@branch-heads/7151',
    "deps_file"   : 'DEPS',
    "managed"     : False,
    "custom_deps" : {},
    "custom_vars" : {},
  },
]
target_os  = ['win']
```

Synchronize sources:

```bash
gclient sync
```

Copy this folder into your WebRTC checkout as `src/libwebrtc` (or use
LumenRTC `scripts/setup.sh` / `scripts/setup.ps1`, which performs this
automatically).

Ensure `src/BUILD.gn` includes `//libwebrtc` in `group("default")`:

```patch
diff --git a/BUILD.gn b/BUILD.gn
@@ -29,7 +29,7 @@ if (!build_with_chromium) {
   group("default") {
     testonly = true
-    deps = [ ":webrtc" ]
+    deps = [ ":webrtc", "//libwebrtc", ]
   }
 }
```

## Windows

Install Visual Studio 2026 with C++ build tools.

```bash
set DEPOT_TOOLS_WIN_TOOLCHAIN=0
set GYP_MSVS_VERSION=2026
set GYP_GENERATORS=ninja,msvs-ninja
set GYP_MSVS_OVERRIDE_PATH=C:\Program Files (x86)\Microsoft Visual Studio\2026\Community
cd src
gn gen out-debug/Windows-x64 --args="target_os=\"win\" target_cpu=\"x64\" is_component_build=false is_clang=true is_debug=true rtc_use_h264=true ffmpeg_branding=\"Chrome\" rtc_include_tests=false rtc_build_examples=false libwebrtc_desktop_capture=true" --ide=vs2022
ninja -C out-debug/Windows-x64 libwebrtc
```

## Linux

Set `target_os = ['linux']` in `.gclient`, then:

```bash
export ARCH=x64 # x86, x64, arm, arm64
gn gen out-debug/Linux-$ARCH --args="target_os=\"linux\" target_cpu=\"$ARCH\" is_debug=true rtc_include_tests=false rtc_use_h264=true ffmpeg_branding=\"Chrome\" is_component_build=false use_rtti=true use_custom_libcxx=false rtc_enable_protobuf=false"
ninja -C out-debug/Linux-x64 libwebrtc
```
