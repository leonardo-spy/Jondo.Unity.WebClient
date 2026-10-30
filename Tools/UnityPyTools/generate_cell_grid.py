#!/usr/bin/env python3
"""
Precompute the exact 560-cell isometric grid and picking tables.

Replicates the decompiled logic from:
  - Core.PathFinding.WorldData.MapPoint (VA: 0x181d9d980 / 0x181d9da30)
  - Static grid math class `oh`:
      - oh..cctor (VA: 0x182ceeb50): generates the 560 (x, y) isometric cell table
      - oh.own (VA: 0x182cf36f0): exact (x, y) -> cellId formula with Math.Floor
      - oh.owm (VA: 0x182cf3570): exact cellId -> (x, y) table lookup
      - oh.bez (VA: 0x182ceed30): isometric coords -> screen/world coordinates

Constants:
  - 560 cells (cellId 0..559)
  - tileFullWidth = 86, tileHalfWidth = 43
  - tileFullHeight = 43, tileQuarter = 21.5
  - cellHalfWidth = 14, cellHalfHeight = 20
  - mapMinX = -580.5, mapMaxX = 623.5
  - mapMinY = -440.75, mapMaxY = 440.75

Output:
  - Generated/output/CellGrid560.json
  - UnityProject/Assets/Resources/CellGrid560.json (when --deploy is passed)
"""

import json
import math
import os
import sys

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
REPO_ROOT = os.path.dirname(os.path.dirname(SCRIPT_DIR))
OUTPUT_DIR = os.environ.get("GENERATED_OUTPUT_DIR", os.path.join(REPO_ROOT, "Generated", "output"))
RESOURCE_DIR = os.path.join(REPO_ROOT, "UnityProject", "Assets", "Resources")


def generate_cell_table():
    """Generates the exact 560 (x, y) coordinates replicating oh..cctor."""
    grid = [None] * 560
    iVar9 = 0
    uVar4 = 0
    iVar8 = 0
    iVar11 = 0
    iVar7 = 0

    while True:
        iVar10 = iVar7
        while iVar11 + iVar10 < 14:
            grid[uVar4] = ((iVar9 - iVar7) + iVar10, iVar10)
            uVar4 += 1
            iVar10 += 1

        iVar9 += 1
        iVar10 = iVar7
        while iVar11 + iVar10 < 14:
            grid[uVar4] = (iVar10 + (iVar9 - iVar7), iVar10)
            uVar4 += 1
            iVar10 += 1

        iVar7 -= 1
        iVar11 += 1
        iVar8 += 1
        if iVar8 > 19:
            break

    if uVar4 != 560:
        raise ValueError(f"Expected 560 cells, generated {uVar4}")
    return grid


def coords_to_cell_id(x, y):
    """Exact replication of oh.own(x, y). Returns cellId 0..559 or -1 if out of bounds."""
    if not ((-x <= y <= x) and (y <= 27 - x) and (x - 39 <= y)):
        return -1
    f = (x - y) * 0.5
    val = (x * 14 + y * -13) + f
    return int(math.floor(val))


def cell_to_world_pos(x, y):
    """
    Computes screen/world center for cell (x, y).
    Based on tileHalfWidth = 43 and tileQuarter = 21.5.
    """
    world_x = (x + y) * 43.0
    world_y = (x - y) * 21.5
    return world_x, world_y


def world_pos_to_coords(world_x, world_y):
    """Converts continuous world position to nearest grid coordinates (x, y)."""
    xf = (world_x / 86.0) + (world_y / 43.0)
    yf = (world_x / 86.0) - (world_y / 43.0)
    return round(xf), round(yf)


def world_pos_to_cell_id(world_x, world_y):
    """Picks cellId (0..559) containing continuous world position, or -1 if out of grid."""
    gx, gy = world_pos_to_coords(world_x, world_y)
    return coords_to_cell_id(gx, gy)


DIRECTIONS = [
    (1, 1),   # 0: Right (dwlo)
    (1, 0),   # 1: Down-Right (dwlp)
    (1, -1),  # 2: Down (dwlq)
    (0, -1),  # 3: Down-Left (dwlr)
    (-1, -1), # 4: Left (dwls)
    (-1, 0),  # 5: Up-Left (dwlt)
    (-1, 1),  # 6: Up (dwlu)
    (0, 1),   # 7: Up-Right (dwlv)
]


def find_path(start_cell_id, target_cell_id, walkable_mask, cell_table=None):
    """
    A* pathfinding on the 560-cell isometric grid respecting obstacles.
    walkable_mask: dict or list of 560 bools indicating walkable cells.
    Returns list of cell IDs from start to target, or None if unreachable.
    """
    import heapq
    if cell_table is None:
        cell_table = generate_cell_table()

    if not (0 <= target_cell_id < len(cell_table)):
        return None
    if not walkable_mask[target_cell_id]:
        return None
    if start_cell_id == target_cell_id:
        return [start_cell_id]

    target_x, target_y = cell_table[target_cell_id]
    open_set = [(0.0, start_cell_id)]
    came_from = {}
    g_score = {start_cell_id: 0.0}

    while open_set:
        _, current = heapq.heappop(open_set)
        if current == target_cell_id:
            path = []
            while current in came_from:
                path.append(current)
                current = came_from[current]
            path.append(start_cell_id)
            path.reverse()
            return path

        cur_x, cur_y = cell_table[current]
        for dx, dy in DIRECTIONS:
            nx, ny = cur_x + dx, cur_y + dy
            neighbor = coords_to_cell_id(nx, ny)
            if neighbor < 0 or neighbor >= len(cell_table) or not walkable_mask[neighbor]:
                continue

            step_cost = 1.414 if (dx != 0 and dy != 0) else 1.0
            tentative_g = g_score[current] + step_cost
            if tentative_g < g_score.get(neighbor, float('inf')):
                came_from[neighbor] = current
                g_score[neighbor] = tentative_g
                h = math.hypot(target_x - nx, target_y - ny)
                heapq.heappush(open_set, (tentative_g + h, neighbor))

    return None


def build_cell_grid_spec():
    table = generate_cell_table()
    cells = []

    # Verify 100% round-trip
    for cell_id, (x, y) in enumerate(table):
        recovered = coords_to_cell_id(x, y)
        if recovered != cell_id:
            raise ValueError(f"Round-trip mismatch for cell {cell_id}: ({x}, {y}) -> {recovered}")

        wx, wy = cell_to_world_pos(x, y)
        # 4 diamond corners for isometric picking / debug overlay
        # top, right, bottom, left
        corners = [
            [round(wx, 2), round(wy + 21.5, 2)],
            [round(wx + 43.0, 2), round(wy, 2)],
            [round(wx, 2), round(wy - 21.5, 2)],
            [round(wx - 43.0, 2), round(wy, 2)],
        ]

        cells.append({
            "cellId": cell_id,
            "x": x,
            "y": y,
            "worldX": round(wx, 2),
            "worldY": round(wy, 2),
            "corners": corners,
        })

    spec = {
        "schemaVersion": 1,
        "totalCells": 560,
        "constants": {
            "tileFullWidth": 86,
            "tileHalfWidth": 43,
            "tileFullHeight": 43,
            "tileQuarter": 21.5,
            "cellHalfWidth": 14,
            "cellHalfHeight": 20,
        },
        "bounds": {
            "minX": min(c["x"] for c in cells),
            "maxX": max(c["x"] for c in cells),
            "minY": min(c["y"] for c in cells),
            "maxY": max(c["y"] for c in cells),
        },
        "cells": cells,
    }
    return spec


def main(deploy=False):
    spec = build_cell_grid_spec()
    os.makedirs(OUTPUT_DIR, exist_ok=True)
    out_path = os.path.join(OUTPUT_DIR, "CellGrid560.json")

    with open(out_path, "w", encoding="utf-8") as f:
        json.dump(spec, f, indent=2)
    print(f"Generated CellGrid560.json ({len(spec['cells'])} cells)")

    if deploy:
        os.makedirs(RESOURCE_DIR, exist_ok=True)
        res_path = os.path.join(RESOURCE_DIR, "CellGrid560.json")
        with open(res_path, "w", encoding="utf-8") as f:
            json.dump(spec, f, indent=2)
        print(f"Deployed to {res_path}")

    return out_path


if __name__ == "__main__":
    deploy_flag = "--deploy" in sys.argv
    main(deploy=deploy_flag)
