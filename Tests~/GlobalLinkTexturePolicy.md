# Global Link texture policy regression checks

Place GlobalLinkTexturePolicyTests.cs in the project's existing Unity Editor test assembly. It uses NUnit and the installed ThryEditor. The fixture targets Assets/_PoiyomiShaders/Shaders/10.0/Pro/Poiyomi Pro.shader; use Vulkan on Linux or Vulkan/DirectX on Windows.

Checks cover bidirectional shading synchronization with distinct AO maps, texture tiling/offset and animation tags, legacy defaults, JSON persistence, stale texture data, full-copy behavior when enabled, and policy undo/redo preserving unrelated link edits. Test assets and link state are restored by the fixture.

Backport validation: the exact backported GlobalLinker and Parser compiled in Unity 2022.3.22f1 in PoiDev. Direct MCP checks passed on Poiyomi 10 Pro / Vulkan (11 texture-policy checks, empty-array roundtrip, and five undo scenarios). Other editor infrastructure remained the active development version; a clean 10.0-only package import was not tested.
