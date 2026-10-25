extends SceneTree

func _init() -> void:
	var failures: Array = []
	var quick: bool = OS.get_cmdline_user_args().has("--quick")
	var suites: Array = ["res://tests/test_economy.gd", "res://tests/test_ledger_save.gd"]
	for suite_path in suites:
		if not ResourceLoader.exists(suite_path):
			failures.append("Required test suite is missing: " + suite_path)
			continue
		var suite = load(suite_path)
		if suite == null or not suite.has_method("run"):
			failures.append("Test suite does not expose run(): " + suite_path)
			continue
		var suite_result: Variant = suite.run_quick() if quick and suite.has_method("run_quick") else suite.run()
		if not suite_result is Array:
			failures.append("Test suite returned no result: " + suite_path)
			continue
		var suite_failures: Array = suite_result
		failures.append_array(suite_failures)
		print("Suite %s: %d failures" % [suite_path, suite_failures.size()])
	for failure in failures:
		push_error(str(failure))
	if failures.is_empty():
		print("PASS: quick headless verification suites completed (long observation skipped)" if quick else "PASS: all headless verification suites completed")
	else:
		print("FAIL: %d verification failures" % failures.size())
	quit(0 if failures.is_empty() else 1)
