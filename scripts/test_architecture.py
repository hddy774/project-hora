"""Exercise the rules that reject incorrect dependency and execution boundaries."""
import importlib.util
from pathlib import Path
import tempfile
import unittest

spec = importlib.util.spec_from_file_location("architecture", Path(__file__).with_name("check-architecture.py"))
architecture = importlib.util.module_from_spec(spec)
spec.loader.exec_module(architecture)


class ArchitectureChecks(unittest.TestCase):
    def setUp(self):
        temp = tempfile.TemporaryDirectory()
        self.addCleanup(temp.cleanup)
        self.root = Path(temp.name)
        for name in ["AlphaExchange.Core", "AlphaExchange.Client", "AlphaExchange.Android"]:
            self.write(f"src/{name}/{name}.csproj", "<Project />")
        self.write("tests/Checks/Checks.csproj", "<Project />")

    def write(self, name, text):
        path = self.root / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(text)

    def test_clean_layout(self):
        self.assertEqual(architecture.violations(self.root), [])

    def test_core_cannot_depend_on_client(self):
        self.write("src/AlphaExchange.Core/AlphaExchange.Core.csproj",
                   '<Project><ItemGroup><ProjectReference Include="../AlphaExchange.Client/AlphaExchange.Client.csproj" /></ItemGroup></Project>')
        self.assertTrue(any("disallowed project dependency" in error for error in architecture.violations(self.root)))

    def test_android_cannot_leak_into_shared_code(self):
        self.write("src/AlphaExchange.Client/Leak.cs", "using Android.Graphics;\nclass Leak {}")
        self.assertTrue(any("Android belongs" in error for error in architecture.violations(self.root)))

    def test_domain_cannot_depend_on_execution(self):
        self.write("src/AlphaExchange.Core/Domain/Leak.cs", "class Leak { GameEngine engine; }")
        self.assertTrue(any("domain state" in error for error in architecture.violations(self.root)))

    def test_tests_cannot_recompile_app_sources(self):
        self.write("tests/Checks/Checks.csproj", '<Project><ItemGroup><Compile Include="../../src/AlphaExchange.Android/Leak.cs" /></ItemGroup></Project>')
        self.assertTrue(any("tests cannot compile" in error for error in architecture.violations(self.root)))

    def test_android_cannot_wait_synchronously(self):
        self.write("src/AlphaExchange.Android/Leak.cs", "class Leak { void Run() { work.GetAwaiter().GetResult(); } }")
        self.assertTrue(any("blocking Android UI" in error for error in architecture.violations(self.root)))


if __name__ == "__main__":
    unittest.main()
