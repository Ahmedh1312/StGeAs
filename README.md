# StGeAs – STAAD Geometry Assistant

A STAAD.Pro plugin that speeds up plate modeling and geometry cleanup, built with C# and the OpenSTAAD API.

[Watch the demo](https://drive.google.com/file/d/1Ve9Zjs28IgKfNTb0ogVg9l6AwfIeLJhf/view?usp=drivesdk)

## Features

- Divide by size: min/max plate dimensions, respecting column locations
- Divide by number: clean NX × NZ grids in one step
- Divide along selected nodes or beams
- Split: cut through chosen nodes along X or Z
- Offset and shrink: adjust plate boundaries along X, Z, or both
- Node alignment: move selected nodes onto a target node, with direction control
- Built-in checks: overlapping plates and duplicate nodes within a set tolerance
- Merge plates: combine adjacent plates into single plates
- Reverse plates: flip the direction of selected plates
- Get count: count selected plates, beams, and nodes
- Undo: revert the last change

## Installation

1. Go to [Releases](https://github.com/Ahmedh1312/StGeAs/releases) and download `StGeAs.Setup.files.rar`.
2. Extract the archive and run the setup file.
3. Open STAAD.Pro and launch StGeAs.

## Requirements

- STAAD.Pro
- Windows

## Author

Ahmed, structural engineer working on computational design and BIM automation.
[LinkedIn](https://www.linkedin.com/in/ahmed-hamed-abdulrahman?utm_source=share_via&utm_content=profile&utm_medium=member_android)

## License

MIT
