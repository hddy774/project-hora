"""Fast screenshot-coordinate regressions; no emulator or image library needed."""
import unittest
from native_capture import display_projection, logical_crop_bounds, logical_display_size


class NativeCaptureTests(unittest.TestCase):
    def test_active_override_takes_precedence(self):
        self.assertEqual(logical_display_size("Physical size: 320x640\nOverride size: 320x408\n"), (320, 408))
        self.assertEqual(logical_display_size("Override size: 320x504\nPhysical size: 320x640\n"), (320, 504))
        self.assertEqual(logical_display_size("Physical size: 320x640\n"), (320, 640))

    def test_missing_or_invalid_display_size_fails(self):
        for value in ("", "Display unavailable", "Physical size: 0x640"):
            with self.subTest(value=value), self.assertRaises(ValueError):
                logical_display_size(value)

    def test_canvas_480_letterbox_matches_recorded_ci_screenshot(self):
        logical, physical = (320, 408), (320, 640)
        self.assertEqual(display_projection(logical, physical), (1, 0, 116))
        self.assertEqual(logical_crop_bounds((0, 0, 320, 408), logical, physical), (0, 116, 320, 524))
        # Canvas CTA [27,408,373,458], view top 24 and canvas scale 0.8.
        self.assertEqual(logical_crop_bounds((21.6, 350.4, 298.4, 390.4), logical, physical), (22, 466, 298, 506))
        self.assertEqual(logical_crop_bounds((268.8, 45.6, 305.6, 79.2), logical, physical), (269, 162, 306, 195))

    def test_canvas_600_letterbox(self):
        logical, physical = (320, 504), (320, 640)
        self.assertEqual(display_projection(logical, physical), (1, 0, 68))
        self.assertEqual(logical_crop_bounds((0, 0, 320, 504), logical, physical), (0, 68, 320, 572))
        self.assertEqual(logical_crop_bounds((21.6, 446.4, 298.4, 486.4), logical, physical), (22, 514, 298, 554))

    def test_canvas_800_downscales_and_pillarboxes(self):
        logical, physical = (320, 664), (320, 640)
        scale, x, y = display_projection(logical, physical)
        self.assertAlmostEqual(scale, 640 / 664)
        self.assertAlmostEqual(x, (320 - 320 * scale) / 2)
        self.assertEqual(y, 0)
        self.assertEqual(logical_crop_bounds((0, 0, 320, 664), logical, physical), (6, 0, 314, 640))
        self.assertEqual(logical_crop_bounds((21.6, 606.4, 298.4, 646.4), logical, physical), (27, 584, 293, 623))

    def test_all_requested_heights_keep_body_separate_from_pinned_cta(self):
        for height in (480, 600, 800):
            with self.subTest(height=height):
                logical, physical = (320, round(height * .8 + 24)), (320, 640)
                body_top = max(12, height - 654) + 58
                body = logical_crop_bounds((18 * .8, 24 + (body_top + 4) * .8, 382 * .8, 24 + (height - 88) * .8), logical, physical)
                cta = logical_crop_bounds((27 * .8, 24 + (height - 72) * .8, 373 * .8, 24 + (height - 22) * .8), logical, physical)
                self.assertLess(body[3], cta[1])
                self.assertGreater(body[3], body[1])
                self.assertLessEqual(cta[3], physical[1])

    def test_default_display_coordinates_remain_unchanged(self):
        self.assertEqual(logical_crop_bounds((21.6, 140.2, 298.4, 506.4), (320, 640), (320, 640)), (22, 140, 298, 506))

    def test_projection_scales_both_axes_and_clamps_edges(self):
        self.assertEqual(logical_crop_bounds((10, 20, 30, 40), (320, 640), (640, 1280)), (20, 40, 60, 80))
        self.assertEqual(logical_crop_bounds((-50, -100, 400, 800), (320, 640), (320, 640)), (0, 0, 320, 640))
        with self.assertRaises(ValueError):
            logical_crop_bounds((400, 800, 500, 900), (320, 640), (320, 640))

    def test_invalid_sizes_and_empty_crops_fail(self):
        for size in ((0, 640), (-1, 640), (float("inf"), 640)):
            with self.subTest(size=size), self.assertRaises(ValueError):
                display_projection(size, (320, 640))
        for rect in ((1, 1, 1, 2), (1, 2, 2, 1), (float("nan"), 1, 2, 3)):
            with self.subTest(rect=rect), self.assertRaises(ValueError):
                logical_crop_bounds(rect, (320, 640), (320, 640))


if __name__ == "__main__":
    unittest.main()
