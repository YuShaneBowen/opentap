using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using NuGet.Packaging;
using NUnit.Framework;
using OpenTap.Diagnostic;
using OpenTap.Plugins.BasicSteps;

namespace OpenTap.Package.UnitTests
{
    [Display("Some test step")]
    public class MyTestStep : TestStep
    {
        [FilePath(FilePathAttribute.BehaviorChoice.Open)]
        public string StringPath { get; set; }

        [FilePath(FilePathAttribute.BehaviorChoice.Open)]
        public MacroString MacroPath { get; set; }

        public string SomeString { get; set; }

        public override void Run()
        {
        }
    }

    [TestFixture]
    public class TestPlanDependencyTest
    {
        private string[] _files;
        private const string os = "Windows,Linux";
        private const string version = "3.4.5";
        private const string TestPackageName = "FakePackageReferencingFile";
        private const string ReferencedFile = "TestPlanFromPackage.TapPlan";
        private const string NotReferencedFile = "OtherFile.txt";
        private const string TestStepName = "Just a name for the step";
        private string ReferencedFile2 => $"Packages/{TestPackageName}/{ReferencedFile}";

        private string TestPackageXml => $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<Package Name=""{TestPackageName}"" xmlns=""http://opentap.io/schemas/package"" Version=""{version}"" OS=""{os}"" >
  <Files>
      <File Path=""{ReferencedFile}""/>
      <File Path=""{ReferencedFile2}""/>
      <File Path=""{Path.GetFileName(typeof(MyTestStep).Assembly.Location)}""/>
  </Files>
</Package>
";

        private string PackageXmlPath =>
            Path.Combine(ExecutorClient.ExeDir, "Packages", TestPackageName, "package.xml");

        [SetUp]
        public void Clear()
        {
            FileSystemHelper.EnsureDirectory(PackageXmlPath);
            if (File.Exists(PackageXmlPath))
                File.Delete(PackageXmlPath);

            _files = new string[] {ReferencedFile, ReferencedFile2};

            foreach (var file in _files)
            {
                if (File.Exists(file))
                    File.Delete(file);
            }
            
            // These plugins reference this UnitTest dll which causes some issues because the test
            // requires FakePackageReferencingFile to be detected as the owner of MyTestStep
            // We remove them while the test is running and restore them afterwards
            var interferingPlugins = new string[] {"test1", "test2", "test3"};
            var dir = Path.Combine(ExecutorClient.ExeDir, "Packages");

            foreach (var plugin in interferingPlugins)
            {
                var packagXml = Path.Combine(dir, plugin, "package.xml");
                var packageXmlBackup = packagXml + ".bad";
                if (File.Exists(packageXmlBackup))
                    File.Delete(packageXmlBackup);
                if (File.Exists(packagXml))
                    File.Move(packagXml, packageXmlBackup);
            }
            // Invalidate the current package cache
            Installation.Current.Invalidate();
        }


        /// <summary>
        /// Restore the plugins that were disabled in the setup
        /// </summary>
        [TearDown]
        public void RestoreConflictingPackages()
        {
            var interferingPlugins = new string[] {"test1", "test2", "test3"};
            var dir = Path.Combine(ExecutorClient.ExeDir, "Packages");

            foreach (var plugin in interferingPlugins)
            {
                var packageXml = Path.Combine(dir, plugin, "package.xml");
                var packageXmlBackup = packageXml + ".bad";
                if (File.Exists(packageXml))
                    File.Delete(packageXml);
                if (File.Exists(packageXmlBackup))
                    File.Move(packageXmlBackup, packageXml);
            }
        }

        [Test]
        public void TestPackageFileDependencies()
        {
            var plan = new TestPlan();
            plan.ChildTestSteps.Add(new MyTestStep()
            {
                StringPath = ReferencedFile, MacroPath = new MacroString() {Text = ReferencedFile2},
                SomeString = NotReferencedFile,
                Name = TestStepName
            });

            var planXml = plan.SerializeToString();
            var document = new XmlDocument();
            document.LoadXml(planXml);

            void VerifyDependency(string attr, string name, int count)
            {
                var nodes = document.SelectNodes(
                    $"/TestPlan/Package.Dependencies/Package[@Name='{TestPackageName}']/{attr}[@Name='{name}']");
                Assert.AreEqual(count, nodes.Count);
            }

            // Verify warnings when files used by test steps are missing
            {
                VerifyDependency("File", ReferencedFile, 0);
                VerifyDependency("File", ReferencedFile2, 0);
                VerifyDependency("Type", typeof(MyTestStep).FullName, 0);
                VerifyDependency("File", NotReferencedFile, 0);

                var ser = new TapSerializer();
                ser.DeserializeFromString(planXml);
                var errors = ser.Errors.ToArray();

                Assert.AreEqual(0, errors.Length, "Expected no errors.");
            }

            // Verify no serializer errors when files are present
            {
                InstallPackage();
                // OnSearch invalidates the cache of currently installed packages;
                // otherwise the serializer will not detect that we have added a package 
                // And add the files the package is expected to contain
                Installation.Current.Invalidate();

                planXml = plan.SerializeToString();
                document.LoadXml(planXml);

                VerifyDependency("File", ReferencedFile, 1);
                VerifyDependency("File", ReferencedFile2, 1);
                // Uncomment when <Type> attributes are added to Package.Dependencies child elements
                // VerifyDependency("Type", typeof(MyTestStep).FullName, 1);
                VerifyDependency("File", NotReferencedFile, 0);

                var ser = new TapSerializer();
                ser.DeserializeFromString(planXml);
                var errors = ser.Errors.ToArray();

                Assert.AreEqual(0, errors.Length, "Expected 0 errors.");
            }

            Installation.Current.Invalidate();

            // Verify serializer errors when required package and files are missing
            {
                // Remove the package again
                Clear();

                var ser = new TapSerializer();
                ser.DeserializeFromString(planXml);
                var errors = ser.Errors.ToArray();

                // Expect a warning for the two [FilePath] properties on the test step
                // Expect an error for the missing package and two errors for the missing files
                Assert.AreEqual(3, errors.Length, "Expected 0 errors.");
                Assert.IsTrue(errors.Any(e =>
                    e.Contains(
                        $"Package '{TestPackageName}' is required to load the test plan, but it is not installed.")));
                Assert.IsTrue(errors.Any(e =>
                    e.Contains(
                        $"File '{ReferencedFile}' from package '{TestPackageName}' is required by the test plan, but it could not be found.")));
                Assert.IsTrue(errors.Any(e =>
                    e.Contains(
                        $"File '{ReferencedFile2}' from package '{TestPackageName}' is required by the test plan, but it could not be found.")));
            }
        }

        // Create fake install of the package
        private void InstallPackage()
        {
            File.WriteAllText(PackageXmlPath, TestPackageXml);
            foreach (var file in _files)
            {
                File.WriteAllText(file, "test");
            }
        }

        [Test]
        public void TestFindPackageOf()
        {
            // Test FindPackageContainingType(TypeData)
            {
                var td = TypeData.FromType(typeof(MyTestStep));

                var p1 = Installation.Current.FindPackageContainingType(td);
                Assert.IsNull(p1);
                InstallPackage();
                var p2 = Installation.Current.FindPackageContainingType(td);
                // The package should be null because the cache is not invalidated
                Assert.IsNull(p2);
                Installation.Current.Invalidate();
                var p3 = Installation.Current.FindPackageContainingType(td);
                Assert.IsNotNull(p3);
                
                StringAssert.AreEqualIgnoringCase(p3.Name, TestPackageName);
                StringAssert.AreEqualIgnoringCase(p3.Version.ToString(), version);
            }

            Clear();

            // Test FindPackageContainingFile("File/Path")
            {
                var filename = ReferencedFile2;
                var p1 = Installation.Current.FindPackageContainingFile(filename);
                Assert.IsNull(p1);
                InstallPackage();
                var p2 = Installation.Current.FindPackageContainingFile(filename);
                // The package should be null because the cache is not invalidated
                Assert.IsNull(p2);
                Installation.Current.Invalidate();
                var p3 = Installation.Current.FindPackageContainingFile(filename);
                Assert.IsNotNull(p3);
                
                StringAssert.AreEqualIgnoringCase(p3.Name, TestPackageName);
                StringAssert.AreEqualIgnoringCase(p3.Version.ToString(), version);
            }
        }
        
        public class TestResultListener : ResultListener
        {
            public  TestPlanRun LatestRun { get; private set; }
            public bool WasRun { get; set; } = false;

            public void Clear()
            {
                WasRun = false;
                LatestRun = null;
            }

            public TestResultListener()
            {
            }

            public override void OnTestPlanRunStart(TestPlanRun planRun)
            {
                LatestRun = planRun;
                WasRun = true;
            }
            
        }
        [Test]
        public void EnsureParameterAttached()
        {
            var parameterName = "Test Plan Package";
            using (Session.Create())
            {
                var listener = new TestResultListener();
                ResultSettings.Current.Add(listener);

                var plan = new TestPlan();

                // Plan which is not from a package
                { 
                    Assert.IsFalse(listener.WasRun);
                    plan.ChildTestSteps.Add(new DelayStep());
                    plan.Execute();

                    var parameterValue = listener.LatestRun.Parameters[parameterName];
                    Assert.IsNull(parameterValue);
                    Assert.IsTrue(listener.WasRun);
                }
                
                listener.Clear();
                Assert.IsFalse(listener.WasRun);

                // Plan which is from a package
                {
                    Assert.IsFalse(listener.WasRun);
                    InstallPackage();
                    Installation.Current.Invalidate();

                    var packageDef = Installation.Current.FindPackageContainingFile(ReferencedFile);
                    
                    plan.Save(ReferencedFile);
                    plan.Execute();

                    Assert.IsTrue(listener.WasRun);

                    var parameterValue = listener.LatestRun.Parameters[parameterName];
                    Assert.AreEqual($"{TestPackageName}|{version}|{packageDef.ComputeHash()}", parameterValue);
                    Assert.IsTrue(listener.WasRun);
                }
            }
        }
    }
}
