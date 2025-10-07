# Road Tile Default Orientation

All diagrams show how each road prefab must be modeled at **0° rotation**. Rotations in code are clockwise around +Y (Unity axes: +Z forward/North, +X right/East).

````markdown
+Z (North)
   ↑
   |
   o----→ +X (East)
````

## Straight (0°)
````markdown
    │
    │
    │
````
Connects North ↔ South in its mesh.

## Corner (0°)
````markdown
    │
    └──→
````
Connects North → East in its mesh.

## T-Intersection (0°)
````markdown
   ┌─┐
   │ │
   └─┘
    │
````
Opening faces South (connected North, East, West).

## Four-Way / Roundabout (0°)
````markdown
   ┌─┐
   │ │
   └─┘
````
Symmetric; orientation not directionally biased.

## Dead End / Start / Exit (0°)
````markdown
    │
    ●
````
Single connection points North.

Model the meshes with these bases, keeping their pivot centered and aligned to the grid so runtime Y-rotations line up automatically.
