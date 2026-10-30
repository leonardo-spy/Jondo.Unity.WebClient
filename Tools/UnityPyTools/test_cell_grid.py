#!/usr/bin/env python3
"""
Unit tests for generate_cell_grid.py.
"""

import json
import os
import unittest

from generate_cell_grid import (
    generate_cell_table,
    coords_to_cell_id,
    cell_to_world_pos,
    world_pos_to_coords,
    world_pos_to_cell_id,
    find_path,
    build_cell_grid_spec,
    main,
)


class CellGridTests(unittest.TestCase):
    def test_cell_table_has_exactly_560_cells(self):
        table = generate_cell_table()
        self.assertEqual(len(table), 560)
        self.assertEqual(table[0], (0, 0))
        self.assertEqual(table[559], (33, -6))

    def test_100_percent_round_trip_for_all_cells(self):
        table = generate_cell_table()
        for cell_id, (x, y) in enumerate(table):
            rec_id = coords_to_cell_id(x, y)
            self.assertEqual(rec_id, cell_id, f"Cell {cell_id} failed round-trip: ({x}, {y}) -> {rec_id}")

    def test_out_of_bounds_coords_return_negative_one(self):
        out_of_bounds = [
            (-1, 0),
            (0, 1),
            (35, 0),
            (10, 20),
            (-5, -5),
            (100, 100),
        ]
        for x, y in out_of_bounds:
            rec_id = coords_to_cell_id(x, y)
            self.assertEqual(rec_id, -1, f"Coords ({x}, {y}) should be out of bounds, got {rec_id}")

    def test_cell_to_world_diamond_dimensions(self):
        wx, wy = cell_to_world_pos(0, 0)
        self.assertEqual(wx, 0.0)
        self.assertEqual(wy, 0.0)

        # Cell (1, 1) has world_x = (1+1)*43 = 86, world_y = (1-1)*21.5 = 0
        wx1, wy1 = cell_to_world_pos(1, 1)
        self.assertEqual(wx1, 86.0)
        self.assertEqual(wy1, 0.0)

    def test_cell_grid_spec_builds_and_deploys(self):
        out_path = main(deploy=True)
        self.assertTrue(os.path.exists(out_path))

        with open(out_path, "r", encoding="utf-8") as f:
            data = json.load(f)

        self.assertEqual(data["schemaVersion"], 1)
        self.assertEqual(data["totalCells"], 560)
        self.assertEqual(len(data["cells"]), 560)
        self.assertEqual(data["constants"]["tileFullWidth"], 86)
        self.assertEqual(data["constants"]["tileHalfWidth"], 43)

        res_path = os.path.join(
            os.path.dirname(os.path.dirname(os.path.dirname(out_path))),
            "UnityProject", "Assets", "Resources", "CellGrid560.json"
        )
        self.assertTrue(os.path.exists(res_path))

    def test_world_to_cell_picking_across_all_560_cells(self):
        table = generate_cell_table()
        for cell_id, (x, y) in enumerate(table):
            wx, wy = cell_to_world_pos(x, y)
            picked_id = world_pos_to_cell_id(wx, wy)
            self.assertEqual(picked_id, cell_id, f"Cell {cell_id} picking failed: pos ({wx}, {wy}) -> {picked_id}")

    def test_pathfinding_respects_obstacles_and_reaches_target(self):
        # Create a 560-cell grid with an obstacle wall
        walkable = [True] * 560
        # Place obstacle at cell 159 (11, 0)
        obstacle_id = coords_to_cell_id(11, 0)
        self.assertGreaterEqual(obstacle_id, 0)
        walkable[obstacle_id] = False

        start_id = coords_to_cell_id(10, 0)
        target_id = coords_to_cell_id(12, 0)
        path = find_path(start_id, target_id, walkable)

        self.assertIsNotNone(path)
        self.assertEqual(path[0], start_id)
        self.assertEqual(path[-1], target_id)
        self.assertNotIn(obstacle_id, path)

    def test_pathfinding_returns_none_for_unwalkable_target(self):
        walkable = [True] * 560
        walkable[100] = False
        path = find_path(0, 100, walkable)
        self.assertIsNone(path)


if __name__ == "__main__":
    unittest.main()
