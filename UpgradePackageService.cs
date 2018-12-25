using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml;

namespace .Services.SystemServices.Upgrade
{
    public static class UpgradePackageService
    {
        /// <summary>
        ///     Get current software version which runs on local machine.
        /// </summary>
        /// <param name="revision">Send out the current version.</param>
        /// <param name="build">Send out the build number.</param>
        /// <returns></returns>
        internal static void GetCurrentVersion(out string revision, out int build)
        {
            revision = string.Empty;
            build = -1;

            var appConfigFile = @"C:\App\Shipped\bin\.MainMonitor.exe.config";

            try
            {
                var document = new XmlDocument();
                document.Load(appConfigFile);
                var node = document.SelectSingleNode("./configuration/appSettings");
                if (node != null)
                {
                    var buildNumberAttribute =
                        node.SelectSingleNode(string.Format("add[@key='{0}']/@value", "BuildNumber")) as XmlAttribute;

                    var revisionAttribute =
                        node.SelectSingleNode(string.Format("add[@key='{0}']/@value", "Revision")) as XmlAttribute;

                    if (buildNumberAttribute != null && !string.IsNullOrEmpty(buildNumberAttribute.Value))
                        int.TryParse(buildNumberAttribute.Value, out build);

                    if (revisionAttribute != null) revision = revisionAttribute.Value;
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine(string.Format("Failed to get software revision.ex:{0}", ex));
            }
        }

        //Validate if upgrade package is correct.
        internal static bool CheckUpgradeDetail(string upgradeFolder)
        {
            var releaseNotes = Path.Combine(upgradeFolder, UpgradePackageUltility.ReleaseNoteFile);
            if (File.Exists(releaseNotes))
            {
                UpgradePackage activePackage;
                var packages = UpgradePackageUltility.LoadUpgradePackagesReleaseNotes(releaseNotes, out activePackage);

                if (packages != null && packages.Count > 0)
                {
                    if (activePackage != null && !string.IsNullOrEmpty(activePackage.PackageName))
                    {
                        var packageFile = Path.Combine(upgradeFolder,
                            string.Format("{0}{1}", activePackage.PackageName,
                                UpgradePackageUltility.PackageFileExtension));

                        if (File.Exists(packageFile))
                            return true;
                    }
                } 
            }
            else
            {
                return false;
            }

            return false;
        }

        internal static void ReleaseToLocal(string zipFilePath)
        {
            //Copy and release new upgrade zip file.
            var localZipFilePath = Path.Combine(@"D:\", Path.GetFileName(zipFilePath));
            File.Copy(zipFilePath, localZipFilePath, true);

            CleanAndRelease(localZipFilePath, @"D:\");
            File.Delete(localZipFilePath);
        }

        /// <summary>
        ///     Figure out if there is any suitable upgrade package zip inside USB. If so, send out the package path.
        /// </summary>
        /// <param name="usbPath">The usb root path.</param>
        /// <param name="currentVersion">Version of current system.</param>
        /// <param name="zipFilePath">Send out the upgrade package zip file path.</param>
        /// <returns></returns>
        internal static bool IsUpgradePackageExists(string usbPath, string currentVersion, out string zipFilePath)
        {
            //Find all zip files in USB, only search top directory.
            var zipFileList = Directory.GetFiles(usbPath, "*.zip").ToList();
            if (zipFileList.Count == 0)
            {
                zipFilePath = string.Empty;
                return false;
            }

            //Remove zip which is not upgrade package.
            foreach (var zipFile in zipFileList)
            {
                var version = GetVersion(zipFile);
                if (string.IsNullOrEmpty(version) || version.Split('.', '(', ')').Length != 3 &&
                    version.Split('.', '(', ')').Length != 5)
                {
                    zipFileList.Remove(zipFile);
                }
            }

            if (zipFileList.Count == 0)
            {
                zipFilePath = string.Empty;
                return false;
            }

            //Loop zip file list.
            zipFileList.Sort((left, right) =>
                string.Compare(CreateStandardVersion(GetVersion(left)),
                    CreateStandardVersion(GetVersion(right)), StringComparison.OrdinalIgnoreCase));

            //Do for loop here, zipFileList is ascending.
            foreach (var zipFile in zipFileList)
            {
                if (string.Compare(CreateStandardVersion(GetVersion(zipFile)), CreateStandardVersion(currentVersion),
                        StringComparison.OrdinalIgnoreCase) <= 0) continue;

                if (!IsCorrectFormat(zipFile)) continue;

                zipFilePath = zipFile;
                return true;
            }

            zipFilePath = string.Empty;
            return false;
        }

        /// <summary>
        /// Check the content of upgrade zip without releasing it.
        /// </summary>
        /// <param name="zipFile">Zip file to be checked.</param>
        private static bool IsCorrectFormat(string zipFile)
        {
            var version = GetVersion(zipFile);
            using (var zip = ZipFile.OpenRead(zipFile))
            {
                //Check file name inside zip file.
                var isDatFileExists = zip.Entries.Any(e => e.FullName.Contains(@"\Upgrade\" + version + @".dat"));
                var isResourceNoteExists = zip.Entries.Any(e => e.FullName.Contains(@"\Upgrade\" + @"ReleaseNotes.xml"));

                if (!isDatFileExists || !isResourceNoteExists) return false;
            }

            return true;
        }

        internal static string GetVersion(string filePath)
        {
            return Path.GetFileNameWithoutExtension(filePath);
        }

        /// <summary>
        ///     Only support version format xx.xx.xx right now.
        /// </summary>
        /// <param name="normalVer">The version number it should be as usual.</param>
        /// <returns>Returns the version number which is convenient to make string comparison.</returns>
        internal static string CreateStandardVersion(string normalVer)
        {
            var splitVer = normalVer.Split('.', '(', ')');

            if (splitVer.Length != 3 && splitVer.Length != 5) return string.Empty;

            if (splitVer[0].Length == 1) splitVer[0] = "0" + splitVer[0];
            if (splitVer[1].Length == 1) splitVer[1] = "0" + splitVer[1];
            if (splitVer[2].Length == 1) splitVer[2] = "0" + splitVer[2];

            switch (splitVer.Length)
            {
                case 3:
                {
                    var result = splitVer[0] + "." + splitVer[1] + "." + splitVer[2];
                    return result;
                }
                case 5:
                {
                    var result = splitVer[0] + "." + splitVer[1] + "." + splitVer[2] +
                                 string.Format(@"({0})", splitVer[3]);
                    return result;
                }
            }

            return string.Empty;
        }

        //Clean up local folder, then release current upgrade package.
        private static void CleanAndRelease(string zipFilePath, string targetPath)
        {
            //Upgrade package not found, do nothing.
            if (string.IsNullOrEmpty(zipFilePath) || string.IsNullOrEmpty(targetPath)) return;

            CleanUpgradePackage(targetPath);

            //Unpack the upgrade package zip to USB.
            UpgradePackageUltility.ExtractToDirectory(zipFilePath, targetPath);
        }

        private static void CleanUpgradePackage(string localPath)
        {
            var targetPath = Path.Combine(localPath, "");
            Directory.Delete(targetPath, true);
        }
    }
}