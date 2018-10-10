using System;
using System.Diagnostics;
using System.IO;
using UtilityService;
using Vinno.Infrastructure;
using Vinno.Infrastructure.Enums;
using Vinno.Modules.ExamInfoModule;
using Vinno.Services.MessageService;

namespace Vinno.Modules.ArchiveModule
{
    //This helper is to do initialization at the start of patient old data update.
    public class PatientUpdateHelper
    {
        private const string ImageRepository = "d:\\UltrasoundImages";
        private const string OldImageRepository = "d:\\UltrasoundImages_Old";

        private const string VetImageRepository = "d:\\UltrasoundImages_Vet";
        private const string OldVetImageRepository = "d:\\UltrasoundImages_Vet_Old";

        public static void InitializeDatabase()
        {
            bool isVet = PatientUtilities.IsVetProduct;
            string initData = "InitCheckUpdateVersion";
            string rootPath = ServiceManager.RootPath;
            string filePath = Path.Combine(rootPath, "SqlUpgrade/Vinno.SqlRestore.exe");
            if (VinnoFile.Exists(filePath) && DigitalSignatureChecker.VerifySignature(filePath))
            {
                var startInfo = new ProcessStartInfo(filePath, $"\"{initData}\" {isVet}")
                {
                    WorkingDirectory = rootPath,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                };

                using (var proc = new Process { StartInfo = startInfo })
                {
                    proc.Start();
                    proc.BeginOutputReadLine();

                    //Wait for the data updating process to end.
                    while (!proc.HasExited)
                    {
                        proc.WaitForExit(1000);
                    }

                    Initialize(proc.ExitCode, isVet);
                }
            }
        }

        private static void Initialize(int versionState, bool isVet)
        {
            string newArchFlag = Path.Combine(ServiceManager.ImageRepositoryPath, "NewSqliteArch.dat");
            
            int isCorrectUpdate = versionState;

            //If sql data not exist, then do no initialize.
            if (isCorrectUpdate == 2) return;

            //Cancel updating if flag exists.
            if (VinnoFile.Exists(newArchFlag)) return;

            if (!isVet && isCorrectUpdate == 1)
            {
                //Human, same type, nothing to do.
                return;
            }

            if (isVet && isCorrectUpdate == 1)
            {
                //Vet, same type, at start, move all old vet files into clean vet folder.
                if (!VinnoDirectory.Exists(ImageRepository)) return;

                TryToMoveFiles(new VinnoDirectoryInfo(ImageRepository), new VinnoDirectoryInfo(VetImageRepository));

                try
                {
                    VinnoDirectory.Delete(ImageRepository, true);
                }
                catch (Exception e)
                {
                    Logger.ForceWriteLine("Delete UltrasoundImage file is failed, detailed log is {0}", e.InnerException?.Message);
                }
                
                return;
            }

            if (!isVet)
            {
                //SqlRestore can not show messageBox normally, so it is called outside in Main exe.
                UpdateNoVetNoCorrectInfo();
                return;
            }

            //SqlRestore can not show messageBox normally, so it is called outside in Main exe.
            UpdateIsVetNoCorrectInfo();
        }

        //For those source files one by one:
        //First，try to move files into new target folder. If failed, then try to copy.
        //If copy is still failed, try to delete files directly. If delete is also failed, then do nothing, just leave it. (Log is written)
        private static void TryToMoveFiles(VinnoDirectoryInfo source, VinnoDirectoryInfo target)
        {
            if (string.Equals(source.FullName, target.FullName, StringComparison.CurrentCultureIgnoreCase))
            {
                return;
            }

            // Check if the target directory exists, if not, create it.
            VinnoDirectory.CreateDirectory(target.FullName);

            // Copy each file into it's new directory.
            foreach (var fi in source.GetFiles())
            {
                string targetFilePath = Path.Combine(target.ToString(), fi.Name);
                try
                {
                    Logger.ForceWriteLine(@"Try to move {0}\{1}", target.FullName, fi.Name);

                    //Try to move file.
                    if (!VinnoFile.Exists(targetFilePath))
                    {
                        fi.MoveTo(targetFilePath);
                    }
                }
                catch (Exception e)
                {
                    //Log the move failed message.
                    Logger.ForceWriteLine(@"File {0} move is failed, detailed exception is: {1}", fi.Name,
                        e.InnerException?.Message);

                    try
                    {
                        Logger.ForceWriteLine(@"Try to copy {0}\{1}", target.FullName, fi.Name);

                        //Try to copy file if move is not working.
                        fi.CopyTo(targetFilePath, true);
                    }
                    catch (Exception exception)
                    {
                        //Log the copy failed message.
                        Logger.ForceWriteLine(@"File {0} copy is failed, detailed exception is: {1}", fi.Name,
                            exception.InnerException?.Message);
                    }
                }
                finally
                {
                    try
                    {
                        //Delete file if move & copy are both failed.
                        fi.Delete();
                    }
                    catch (Exception e)
                    {
                        Logger.ForceWriteLine(@"File {0} delete is failed, detailed exception is: {1}", fi.Name, 
                            e.InnerException?.Message);
                    }
                }
            }

            // Copy each subdirectory using recursion.
            foreach (VinnoDirectoryInfo diSourceSubDir in source.GetDirectories())
            {
                VinnoDirectoryInfo nextTargetSubDir = target.CreateSubdirectory(diSourceSubDir.Name);
                TryToMoveFiles(diSourceSubDir, nextTargetSubDir);
            }
        }

        private static void UpdateNoVetNoCorrectInfo()
        {
            var result = ServiceManager.Instance.GetService<IMessageBoxService>()
                .ShowMessageBox("Waring", "DeleteOrSaveOldDataVP", MessageBoxButtonEnum.YesNo);

            switch (result)
            {
                case MessageBoxResultExtend.No:
                    //Delete images.
                    if (VinnoDirectory.Exists(ImageRepository))
                    {
                        TryToMoveFiles(new VinnoDirectoryInfo(ImageRepository), new VinnoDirectoryInfo(OldVetImageRepository));
                        try
                        {
                            VinnoDirectory.Delete(ImageRepository, true);
                        }
                        catch (Exception e)
                        {
                            Logger.ForceWriteLine("Delete UltrasoundImage file is failed, detailed log is {0}", e.InnerException?.Message);
                        }
                    }

                    if (VinnoDirectory.Exists(VetImageRepository))
                    {
                        TryToMoveFiles(new VinnoDirectoryInfo(VetImageRepository), new VinnoDirectoryInfo(OldVetImageRepository));

                        try
                        {
                            VinnoDirectory.Delete(VetImageRepository, true);
                        }
                        catch (Exception e)
                        {
                            Logger.ForceWriteLine("Delete UltrasoundImage_Vet file is failed, detailed log is {0}", e.InnerException?.Message);
                        }
                    }

                    UpdateNoVetNoCorrectDatabase(false);
                    break;

                case MessageBoxResultExtend.Yes:
                    //Delete images.
                    try
                    {
                        VinnoDirectory.Delete(ImageRepository, true);
                    }
                    catch (Exception e)
                    {
                        Logger.ForceWriteLine("Delete UltrasoundImage file is failed, detailed log is {0}", e.InnerException?.Message);
                    }
                    
                    UpdateNoVetNoCorrectDatabase(true);
                    break;
            }
        }

        private static void UpdateIsVetNoCorrectInfo()
        {
            var result = ServiceManager.Instance.GetService<IMessageBoxService>()
                .ShowMessageBox("Waring", "DeleteOrSaveOldDataPV", MessageBoxButtonEnum.YesNo);
            switch (result)
            {
                case MessageBoxResultExtend.No:
                    if (Directory.Exists(ImageRepository))
                    {
                        TryToMoveFiles(new VinnoDirectoryInfo(ImageRepository), new VinnoDirectoryInfo(OldImageRepository));

                        try
                        {
                            VinnoDirectory.Delete(ImageRepository, true);
                        }
                        catch (Exception e)
                        {
                            Logger.ForceWriteLine("Delete UltrasoundImage file is failed, detailed log is {0}", e.InnerException?.Message);
                        }
                    }

                    UpdateIsVetNoCorrectDatabase(false);
                    break;
                case MessageBoxResultExtend.Yes:
                    try
                    {
                        VinnoDirectory.Delete(ImageRepository, true);
                    }
                    catch (Exception e)
                    {
                        Logger.ForceWriteLine("Delete UltrasoundImage file is failed, detailed log is {0}", e.InnerException?.Message);
                    }

                    UpdateIsVetNoCorrectDatabase(true);
                    break;
            }
        }

        private static void UpdateNoVetNoCorrectDatabase(bool isTrue)
        {
            bool isVet = PatientUtilities.IsVetProduct;
            string deleteDataBase = "DeleteDatabase";
            string rootPath = ServiceManager.RootPath;
            string filePath = Path.Combine(rootPath, "SqlUpgrade/Vinno.SqlRestore.exe");
            if (VinnoFile.Exists(filePath) && DigitalSignatureChecker.VerifySignature(filePath))
            {
                ProcessStartInfo startInfo;
                if (isTrue)
                {
                    startInfo = new ProcessStartInfo(filePath, $"\"{deleteDataBase}\" {isVet}")
                    {
                        WorkingDirectory = rootPath,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardError = true,
                        RedirectStandardOutput = true
                    };
                }
                else
                {
                    startInfo = new ProcessStartInfo(filePath, "NoVetSaveAndDeleteDatabase")
                    {
                        WorkingDirectory = rootPath,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardError = true,
                        RedirectStandardOutput = true
                    };
                }

                using (var proc = new Process { StartInfo = startInfo })
                {
                    proc.Start();
                    proc.BeginOutputReadLine();

                    //Wait for the data updating process to end.
                    while (!proc.HasExited)
                    {
                        proc.WaitForExit(1000);
                    }
                }
            }
        }

        private static void UpdateIsVetNoCorrectDatabase(bool isTrue)
        {
            bool isVet = PatientUtilities.IsVetProduct;
            string deleteDataBase = "DeleteDatabase";
            string rootPath = ServiceManager.RootPath;
            string filePath = Path.Combine(rootPath, "SqlUpgrade/Vinno.SqlRestore.exe");

            if (VinnoFile.Exists(filePath) && DigitalSignatureChecker.VerifySignature(filePath))
            {
                ProcessStartInfo startInfo;
                if (isTrue)
                {
                    startInfo = new ProcessStartInfo(filePath, $"\"{deleteDataBase}\" {isVet}")
                    {
                        WorkingDirectory = rootPath,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardError = true,
                        RedirectStandardOutput = true
                    };
                }
                else
                {
                    startInfo = new ProcessStartInfo(filePath, "IsVetSaveAndDeleteDatabase")
                    {
                        WorkingDirectory = rootPath,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardError = true,
                        RedirectStandardOutput = true
                    };
                }

                using (var proc = new Process { StartInfo = startInfo })
                {
                    proc.Start();
                    proc.BeginOutputReadLine();

                    //Wait for the data updating process to end.
                    while (!proc.HasExited)
                    {
                        proc.WaitForExit(1000);
                    }
                }
            }
        }
    }
}
