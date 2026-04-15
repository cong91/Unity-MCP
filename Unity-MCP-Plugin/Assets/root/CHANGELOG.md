# Changelog

## [0.17.1] - 2025-01-XX

### Added

- Practical 2D/tilemap MCP tools for sprite-sheet import, grid slicing, tile asset generation, tilemap creation, and tile painting
- EditMode workflow tests covering the new 2D toolchain end to end

### Fixed

- **Play Mode Reconnection**: Fixed Unity-MCP-Plugin not reconnecting after exiting Play mode. The plugin now automatically re-establishes connection when returning to Edit mode if "Keep Connected" is enabled.
- Added proper handling for Unity's Play mode state changes (`EditorApplication.playModeStateChanged`)
- Enhanced logging for connection lifecycle debugging

### Added

- Comprehensive test coverage for Play mode reconnection scenarios
- Debug logging for Play mode transitions to help troubleshooting connection issues

## [0.1.0] - 2025-04-01

### Added

- Initial release of the Unity package.
